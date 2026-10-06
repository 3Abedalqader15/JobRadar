using System.Text;
using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.JobApplications.Commands.ProcessCvAnalysis;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class CvAnalysisPipelineTests
{
    private readonly Mock<IUserJobApplicationRepository> _mockAppRepo = new();
    private readonly Mock<IJobRepository> _mockJobRepo = new();
    private readonly Mock<IFileStorageService> _mockFileStorage = new();
    private readonly Mock<IDocumentTextExtractionService> _mockTextExtractor = new();
    private readonly Mock<ICvMatchAnalysisService> _mockCvMatchService = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ILogger<ProcessCvAnalysisCommandHandler>> _mockLogger = new();

    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Job _job;

    public CvAnalysisPipelineTests()
    {
        _job = Job.Create(Guid.NewGuid(), "Senior .NET Developer", "Contoso", "Build C# web APIs with PostgreSQL", "Remote", true, EmploymentType.FullTime, ExperienceLevel.Senior, null, DateTime.UtcNow, null, Guid.NewGuid());
        _mockJobRepo.Setup(r => r.GetByIdAsync(_jobId, It.IsAny<CancellationToken>())).ReturnsAsync(_job);
    }

    [Fact]
    public async Task ProcessCvAnalysis_WhenAlreadyCompleted_SkipsExecutionAndCallsGeminiZeroTimes()
    {
        // Arrange
        var application = UserJobApplication.CreateDetailed(
            _userId, _jobId, "John Doe", "john@example.com", "+123456789", "cvs/test.pdf", "test.pdf");
        application.CompleteAiAnalysis(85, new[] { "Docker" }, "Strong C# background.");

        _mockAppRepo.Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);

        var handler = new ProcessCvAnalysisCommandHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object,
            _mockTextExtractor.Object, _mockCvMatchService.Object, _mockUnitOfWork.Object, _mockLogger.Object);

        // Act
        await handler.Handle(new ProcessCvAnalysisCommand(application.Id), CancellationToken.None);

        // Assert: Gemini called zero times, no download performed
        _mockFileStorage.Verify(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCvMatchService.Verify(c => c.AnalyzeMatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCvAnalysis_WhenSuccessful_ExtractsTextCallsGeminiAndCompletesAnalysis()
    {
        // Arrange
        var application = UserJobApplication.CreateDetailed(
            _userId, _jobId, "John Doe", "john@example.com", "+123456789", "cvs/valid.pdf", "valid.pdf");

        _mockAppRepo.Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample cv stream content"));
        _mockFileStorage.Setup(f => f.DownloadAsync("cvs/valid.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        _mockTextExtractor.Setup(t => t.ExtractTextAsync(It.IsAny<Stream>(), "valid.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("John Doe, Senior C# backend engineer with 8 years of experience in .NET Core and PostgreSQL.");

        _mockCvMatchService.Setup(c => c.AnalyzeMatchAsync(
                It.IsAny<string>(), _job.Title, _job.Description, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CvMatchAnalysisResult(92, new[] { "Kubernetes" }, "Excellent match with strong C# experience."));

        var handler = new ProcessCvAnalysisCommandHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object,
            _mockTextExtractor.Object, _mockCvMatchService.Object, _mockUnitOfWork.Object, _mockLogger.Object);

        // Act
        await handler.Handle(new ProcessCvAnalysisCommand(application.Id), CancellationToken.None);

        // Assert
        application.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Completed);
        application.AiMatchScore.Should().Be(92);
        application.AiMissingKeywords.Should().Contain("Kubernetes");
        application.AiAnalysisSummary.Should().Contain("Excellent match");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ProcessCvAnalysis_WhenExceptionOccurs_FallsBackToFailAiAnalysisAndDoesNotHangInProcessing()
    {
        // Arrange
        var application = UserJobApplication.CreateDetailed(
            _userId, _jobId, "John Doe", "john@example.com", "+123456789", "cvs/corrupt.pdf", "corrupt.pdf");

        _mockAppRepo.Setup(r => r.GetByIdAsync(application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(application);

        // Simulate storage download failure
        _mockFileStorage.Setup(f => f.DownloadAsync("cvs/corrupt.pdf", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Cloudflare R2 timeout"));

        var handler = new ProcessCvAnalysisCommandHandler(
            _mockAppRepo.Object, _mockJobRepo.Object, _mockFileStorage.Object,
            _mockTextExtractor.Object, _mockCvMatchService.Object, _mockUnitOfWork.Object, _mockLogger.Object);

        // Act
        await handler.Handle(new ProcessCvAnalysisCommand(application.Id), CancellationToken.None);

        // Assert: Never stuck in Processing; status marked as Failed
        application.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Failed);
        application.AiAnalysisSummary.Should().Contain("Cloudflare R2 timeout");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }
}
