using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;
using JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;
using JobRadar.Application.Features.JobPostings.Commands.UpdateJobPosting;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;
using JobRadar.Application.Features.JobApplications.Queries.GetAllApplications;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Features.JobApplications;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class CompanyScopedAccessTests
{
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly Mock<IJobRepository> _mockJobRepo;
    private readonly Mock<ICompanyRepository> _mockCompanyRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IJobRealtimeNotifier> _mockNotifier;

    private readonly Guid _companyAId = Guid.NewGuid();
    private readonly Guid _companyBId = Guid.NewGuid();
    private readonly Job _jobA;
    private readonly Job _jobB;

    public CompanyScopedAccessTests()
    {
        _mockUserService = new Mock<ICurrentUserService>();
        _mockJobRepo = new Mock<IJobRepository>();
        _mockCompanyRepo = new Mock<ICompanyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockNotifier = new Mock<IJobRealtimeNotifier>();

        _jobA = Job.Create(Guid.NewGuid(), "Job A", "Company A", "Desc", "Loc", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, "url", DateTime.UtcNow, null, _companyAId);
        _jobB = Job.Create(Guid.NewGuid(), "Job B", "Company B", "Desc", "Loc", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, "url", DateTime.UtcNow, null, _companyBId);

        _mockJobRepo.Setup(r => r.GetByIdAsync(_jobA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_jobA);
        _mockJobRepo.Setup(r => r.GetByIdAsync(_jobB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_jobB);
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(_companyAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.Create("Company A", "comp-a", null));
    }

    [Fact]
    public async Task GetJobPostings_WhenHrUser_OnlyReturnsOwnCompanyJobs_EvenIfCompanyIdSpecifiedInQuery()
    {
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyAId);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        _mockJobRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { _jobA, _jobB });
        _mockJobRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var handler = new GetJobPostingsQueryHandler(_mockJobRepo.Object, _mockUserService.Object);
        var query = new GetJobPostingsQuery(CompanyId: _companyBId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("Job A");
    }

    [Fact]
    public async Task UpdateJobPosting_WhenHrUserUpdatesAnotherCompanyJob_ThrowsForbiddenException()
    {
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyAId);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        var handler = new UpdateJobPostingCommandHandler(_mockJobRepo.Object, _mockCompanyRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object, _mockNotifier.Object);
        var command = new UpdateJobPostingCommand(_jobB.Id, "New Title", "Loc", false, "url", null, null, null, EmploymentType.FullTime, ExperienceLevel.MidLevel, "Desc", true, null);

        await FluentActions.Invoking(() => handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteJobPosting_WhenHrUserDeletesAnotherCompanyJob_ThrowsForbiddenException()
    {
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyAId);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        var handler = new DeleteJobPostingCommandHandler(_mockJobRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object, _mockNotifier.Object);
        var command = new DeleteJobPostingCommand(_jobB.Id);

        await FluentActions.Invoking(() => handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateJobPosting_WhenHrUserCreatesJob_ForcesCompanyIdToUserCompanyId()
    {
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyAId);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        Job? capturedJob = null;
        _mockJobRepo.Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Callback<Job, CancellationToken>((j, _) => capturedJob = j)
            .Returns(Task.CompletedTask);

        var handler = new CreateJobPostingCommandHandler(_mockJobRepo.Object, _mockCompanyRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object, _mockEmbeddingService.Object, _mockNotifier.Object);
        var command = new CreateJobPostingCommand(Guid.NewGuid(), "New Job", "Some Company", "Desc", "Loc", false, "url", null, null, null, EmploymentType.FullTime, ExperienceLevel.MidLevel, DateTime.UtcNow, null, _companyBId);

        await handler.Handle(command, CancellationToken.None);

        capturedJob.Should().NotBeNull();
        capturedJob!.CompanyId.Should().Be(_companyAId);
    }

    [Fact]
    public async Task UnassignedHr_GetsZeroResultsOnQueries_AndForbiddenOnWrites()
    {
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns((Guid?)null);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        var queryHandler = new GetJobPostingsQueryHandler(_mockJobRepo.Object, _mockUserService.Object);
        var queryResult = await queryHandler.Handle(new GetJobPostingsQuery(), CancellationToken.None);
        queryResult.Items.Should().BeEmpty();
        queryResult.TotalCount.Should().Be(0);

        var createHandler = new CreateJobPostingCommandHandler(_mockJobRepo.Object, _mockCompanyRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object, _mockEmbeddingService.Object, _mockNotifier.Object);
        var command = new CreateJobPostingCommand(Guid.NewGuid(), "New Job", "Some Company", "Desc", "Loc", false, "url", null, null, null, EmploymentType.FullTime, ExperienceLevel.MidLevel, DateTime.UtcNow, null, null);

        await FluentActions.Invoking(() => createHandler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ApplicationsController_WhenHrFromCompanyAHitsGetAll_NeverReceivesCompanyBApplications()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(options);

        var companyA = Company.Create("Company A", "comp-a");
        var companyB = Company.Create("Company B", "comp-b");
        db.Companies.AddRange(companyA, companyB);

        var jobA = Job.Create(Guid.NewGuid(), "Job A", "Company A", "Desc", "Loc", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, "url", DateTime.UtcNow, null, companyA.Id);
        var jobB = Job.Create(Guid.NewGuid(), "Job B", "Company B", "Desc", "Loc", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, "url", DateTime.UtcNow, null, companyB.Id);
        db.Jobs.AddRange(jobA, jobB);

        var userA = ApplicationUser.Create("a@test.com", "A");
        userA.PasswordHash = "hash_a";
        var userB = ApplicationUser.Create("b@test.com", "B");
        userB.PasswordHash = "hash_b";
        db.Users.AddRange(userA, userB);
        
        var appA = UserJobApplication.Create(userA.Id, jobA.Id);
        var appB = UserJobApplication.Create(userB.Id, jobB.Id);
        db.UserJobApplications.AddRange(appA, appB);

        await db.SaveChangesAsync();

        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(companyA.Id);
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);

        var handler = new GetAllApplicationsQueryHandler(db, _mockUserService.Object);

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllApplicationsQuery>(), It.IsAny<CancellationToken>()))
            .Returns<GetAllApplicationsQuery, CancellationToken>((query, ct) => handler.Handle(query, ct));

        var controller = new ApplicationsController(mediatorMock.Object);

        var actionResult = await controller.GetAll(page: 1, pageSize: 20);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GetAllApplicationsResponse>().Subject;

        response.TotalCount.Should().Be(1);
        response.Items.Should().ContainSingle();
        response.Items[0].JobId.Should().Be(jobA.Id);
    }
}
