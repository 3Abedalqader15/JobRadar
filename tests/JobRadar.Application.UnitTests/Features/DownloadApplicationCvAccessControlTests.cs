using System.Text;
using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Application.Features.JobApplications.Queries.DownloadApplicationCv;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class DownloadApplicationCvAccessControlTests
{
    private readonly Mock<IUserJobApplicationRepository> _mockAppRepo = new();
    private readonly Mock<IJobRepository> _mockJobRepo = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<ICurrentUserService> _mockUserService = new();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _applicantUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Job _companyJob;
    private readonly UserJobApplication _application;

    public DownloadApplicationCvAccessControlTests()
    {
        _companyJob = Job.Create(Guid.NewGuid(), "Frontend Dev", "DevCorp", "Desc", "Amman", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, null, DateTime.UtcNow, null, _companyId);

        _application = UserJobApplication.CreateDetailed(
            _applicantUserId,
            _companyJob.Id,
            "Jane Candidate",
            "jane@example.com",
            "+962791234567",
            "cvs/guid-123.pdf",
            "jane_cv.pdf");

        _mockAppRepo.Setup(r => r.GetByIdAsync(_application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_application);
        _mockJobRepo.Setup(r => r.GetByIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_companyJob);

        var sampleStream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF sample content"));
        _mockFileStorage.Setup(f => f.DownloadAsync("cvs/guid-123.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleStream);
    }

    [Fact]
    public async Task DownloadCv_WhenUnauthorizedUserAttemptsDownload_ReturnsForbidden()
    {
        // Arrange: User C (not admin, not applicant, not HR for company)
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);
        _mockUserService.Setup(u => u.IsHr).Returns(false);
        _mockUserService.Setup(u => u.UserId).Returns(_otherUserId);

        var handler = new DownloadApplicationCvQueryHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object, _mockUserService.Object);

        // Act & Assert
        var act = async () => await handler.Handle(new DownloadApplicationCvQuery(_application.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not authorized to download this applicant's CV.");
    }

    [Fact]
    public async Task DownloadCv_WhenApplicantThemselvesDownloads_AllowsDownload()
    {
        // Arrange: The applicant themselves
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);
        _mockUserService.Setup(u => u.IsHr).Returns(false);
        _mockUserService.Setup(u => u.UserId).Returns(_applicantUserId);

        var handler = new DownloadApplicationCvQueryHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object, _mockUserService.Object);

        // Act
        var result = await handler.Handle(new DownloadApplicationCvQuery(_application.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("jane_cv.pdf");
        result.ContentType.Should().Be("application/pdf");
        result.FileStream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadCv_WhenCompanyHrDownloads_AllowsDownload()
    {
        // Arrange: HR user belonging to the job's company
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyId);
        _mockUserService.Setup(u => u.UserId).Returns(_otherUserId);

        var handler = new DownloadApplicationCvQueryHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object, _mockUserService.Object);

        // Act
        var result = await handler.Handle(new DownloadApplicationCvQuery(_application.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("jane_cv.pdf");
        result.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task DownloadCv_WhenAdminDownloads_AllowsDownload()
    {
        // Arrange: Platform Admin
        _mockUserService.Setup(u => u.IsAdmin).Returns(true);
        _mockUserService.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new DownloadApplicationCvQueryHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object, _mockUserService.Object);

        // Act
        var result = await handler.Handle(new DownloadApplicationCvQuery(_application.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("jane_cv.pdf");
    }

    [Fact]
    public async Task DownloadCv_WhenOtherCompanyHrAttemptsDownload_ReturnsForbidden()
    {
        // Arrange: HR for Company B attempting to access Company A's candidate
        _mockUserService.Setup(u => u.IsAdmin).Returns(false);
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(Guid.NewGuid()); // Different company
        _mockUserService.Setup(u => u.UserId).Returns(_otherUserId);

        var handler = new DownloadApplicationCvQueryHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object, _mockUserService.Object);

        // Act & Assert
        var act = async () => await handler.Handle(new DownloadApplicationCvQuery(_application.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not authorized to download this applicant's CV.");
    }
}
