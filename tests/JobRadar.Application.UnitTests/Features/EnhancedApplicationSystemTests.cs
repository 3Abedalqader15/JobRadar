using System.Text;
using FluentAssertions;
using FluentValidation;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Application.Common.Validators;
using JobRadar.Application.Features.JobApplications.Commands.ManageJobApplicationQuestions;
using JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication;
using JobRadar.Application.Features.JobApplications.Queries.GetJobApplicationQuestions;
using JobRadar.Application.Messages;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using MassTransit;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class EnhancedApplicationSystemTests
{
    private readonly Mock<IJobRepository> _mockJobRepo = new();
    private readonly Mock<IUserJobApplicationRepository> _mockAppRepo = new();
    private readonly Mock<IJobApplicationQuestionRepository> _mockQuestionRepo = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<ICvUploadValidator> _mockCvValidator = new();
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
    private readonly Mock<ICurrentUserService> _mockUserService = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Job _companyJob;
    private readonly Job _aggregatedJob;

    public EnhancedApplicationSystemTests()
    {
        _companyJob = Job.Create(Guid.NewGuid(), "Backend Engineer", "TechCorp", "Description", "Amman", false, EmploymentType.FullTime, ExperienceLevel.MidLevel, null, DateTime.UtcNow, null, _companyId);
        _aggregatedJob = Job.Create(Guid.NewGuid(), "Aggregated Job", "Scraped Co", "Description", "Remote", true, EmploymentType.FullTime, ExperienceLevel.MidLevel, "https://apply.com", DateTime.UtcNow, null, null);

        _mockJobRepo.Setup(r => r.GetByIdAsync(_companyJob.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_companyJob);
        _mockJobRepo.Setup(r => r.GetByIdAsync(_aggregatedJob.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_aggregatedJob);
    }

    [Fact]
    public async Task ManageJobApplicationQuestions_WhenJobIsAggregated_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new ManageJobApplicationQuestionsCommandHandler(
            _mockJobRepo.Object, _mockQuestionRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object);
        var command = new ManageJobApplicationQuestionsCommand(_aggregatedJob.Id, new List<QuestionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot add screening questions to an aggregated job.");
    }

    [Fact]
    public async Task ManageJobApplicationQuestions_WhenHrFromDifferentCompany_ThrowsForbiddenException()
    {
        // Arrange
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(Guid.NewGuid()); // Different company

        var handler = new ManageJobApplicationQuestionsCommandHandler(
            _mockJobRepo.Object, _mockQuestionRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object);
        var command = new ManageJobApplicationQuestionsCommand(_companyJob.Id, new List<QuestionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*HR users can only manage screening questions*");
    }

    [Fact]
    public async Task ManageJobApplicationQuestions_WhenValid_SyncsQuestionsAndSaves()
    {
        // Arrange
        _mockUserService.Setup(u => u.IsHr).Returns(true);
        _mockUserService.Setup(u => u.CompanyId).Returns(_companyId); // Matching company

        _mockQuestionRepo.Setup(r => r.GetByJobIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobApplicationQuestion>());

        var handler = new ManageJobApplicationQuestionsCommandHandler(
            _mockJobRepo.Object, _mockQuestionRepo.Object, _mockUserService.Object, _mockUnitOfWork.Object);

        var command = new ManageJobApplicationQuestionsCommand(_companyJob.Id, new List<QuestionDto>
        {
            new(null, "Years of .NET experience?", QuestionType.Text, null, true, 1),
            new(null, "Comfortable with PostgreSQL?", QuestionType.YesNo, null, false, 2)
        });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockQuestionRepo.Verify(r => r.AddAsync(It.IsAny<JobApplicationQuestion>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitJobApplication_WhenJobIsAggregated_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication>());

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 sample"));
        var command = new SubmitJobApplicationCommand(
            _aggregatedJob.Id, Guid.NewGuid(), "Jane Doe", "jane@example.com", "+962791234567",
            stream, "cv.pdf", "application/pdf", stream.Length, new List<AnswerSubmissionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Enhanced applications with screening questions and CV upload are only supported for officially posted company jobs.*");
    }

    [Fact]
    public async Task SubmitJobApplication_WhenUserAlreadyApplied_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingApp = UserJobApplication.Create(userId, _companyJob.Id);
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication> { existingApp });

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 sample"));
        var command = new SubmitJobApplicationCommand(
            _companyJob.Id, userId, "Jane Doe", "jane@example.com", "+962791234567",
            stream, "cv.pdf", "application/pdf", stream.Length, new List<AnswerSubmissionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User has already applied for this job.");
    }

    [Fact]
    public async Task SubmitJobApplication_WhenRequiredQuestionNotAnswered_ThrowsValidationException_WithQuestionTitle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication>());

        var requiredQ = JobApplicationQuestion.Create(_companyJob.Id, "Do you have C# experience?", QuestionType.YesNo, null, true, 1);
        _mockQuestionRepo.Setup(r => r.GetByJobIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobApplicationQuestion> { requiredQ });

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 sample"));
        var command = new SubmitJobApplicationCommand(
            _companyJob.Id, userId, "Jane Doe", "jane@example.com", "+962791234567",
            stream, "cv.pdf", "application/pdf", stream.Length, new List<AnswerSubmissionDto>()); // Empty answers

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.ErrorMessage.Contains("Do you have C# experience?"));
    }

    [Fact]
    public async Task SubmitJobApplication_WhenValid_UploadsCv_IncrementsLiveApplicantCounter_PublishesOutboxEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication>());

        var q1 = JobApplicationQuestion.Create(_companyJob.Id, "Notice Period?", QuestionType.Text, null, true, 1);
        _mockQuestionRepo.Setup(r => r.GetByJobIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobApplicationQuestion> { q1 });

        _mockCvValidator.Setup(v => v.ValidateAsync(It.IsAny<CvUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CvUploadValidationResult.Success());

        _mockFileStorage.Setup(f => f.UploadAsync(It.IsAny<Stream>(), "cv.pdf", "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cvs/stored-guid-123.pdf");

        int initialClicks = _companyJob.ApplicantsClickCount;

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 sample"));
        var command = new SubmitJobApplicationCommand(
            _companyJob.Id, userId, "Jane Doe", "jane@example.com", "+962791234567",
            stream, "cv.pdf", "application/pdf", stream.Length,
            new List<AnswerSubmissionDto>
            {
                new(q1.Id, "1 Month")
            });

        // Act
        var appId = await handler.Handle(command, CancellationToken.None);

        // Assert
        appId.Should().NotBeEmpty();

        // 1. Live Applicant Counter (ApplicantsClickCount) increment verification
        _companyJob.ApplicantsClickCount.Should().Be(initialClicks + 1);

        // 2. Storage upload
        _mockFileStorage.Verify(f => f.UploadAsync(It.IsAny<Stream>(), "cv.pdf", "application/pdf", It.IsAny<CancellationToken>()), Times.Once);

        // 3. Staged entity
        _mockAppRepo.Verify(r => r.AddAsync(It.Is<UserJobApplication>(a => a.CvFilePath == "cvs/stored-guid-123.pdf" && a.ApplicantFullName == "Jane Doe"), It.IsAny<CancellationToken>()), Times.Once);

        // 4. Outbox event publication
        _mockPublishEndpoint.Verify(p => p.Publish(It.Is<CvAnalysisRequestedEvent>(e => e.ApplicationId == appId && e.CvFilePath == "cvs/stored-guid-123.pdf"), It.IsAny<CancellationToken>()), Times.Once);

        // 5. Atomic transaction
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitJobApplication_WhenInvalidExtensionOrMimeType_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication>());

        _mockQuestionRepo.Setup(r => r.GetByJobIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobApplicationQuestion>());

        _mockCvValidator.Setup(v => v.ValidateAsync(It.IsAny<CvUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CvUploadValidationResult.Failure("Only PDF (.pdf) and Word documents (.docx) are supported."));

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("binary executable payload"));
        var command = new SubmitJobApplicationCommand(
            _companyJob.Id, userId, "Jane Doe", "jane@example.com", "+962791234567",
            stream, "malware.exe", "application/x-msdownload", stream.Length,
            new List<AnswerSubmissionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == "CvFile" && e.ErrorMessage.Contains("Only PDF"));
    }

    [Fact]
    public async Task SubmitJobApplication_WhenFileSizeExceedsLimit_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockAppRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserJobApplication>());

        _mockQuestionRepo.Setup(r => r.GetByJobIdAsync(_companyJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobApplicationQuestion>());

        _mockCvValidator.Setup(v => v.ValidateAsync(It.IsAny<CvUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CvUploadValidationResult.Failure("File size (6.0 MB) exceeds maximum allowed limit of 5 MB."));

        var handler = new SubmitJobApplicationCommandHandler(
            _mockJobRepo.Object, _mockAppRepo.Object, _mockQuestionRepo.Object,
            _mockFileStorage.Object, _mockCvValidator.Object, _mockPublishEndpoint.Object, _mockUnitOfWork.Object);

        long sixMb = 6 * 1024 * 1024;
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 sample"));
        var command = new SubmitJobApplicationCommand(
            _companyJob.Id, userId, "Jane Doe", "jane@example.com", "+962791234567",
            stream, "huge.pdf", "application/pdf", sixMb,
            new List<AnswerSubmissionDto>());

        // Act & Assert
        var act = async () => await handler.Handle(command, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.PropertyName == "CvFile" && e.ErrorMessage.Contains("exceeds maximum allowed limit of 5 MB"));
    }
}
