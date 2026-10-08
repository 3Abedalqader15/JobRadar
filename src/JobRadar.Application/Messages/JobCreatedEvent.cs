using JobRadar.Domain.Enums;

namespace JobRadar.Application.Messages;

/// <summary>
/// Published after a <see cref="Domain.Entities.Job"/> has been successfully
/// created and persisted from a processed RawPost or API action.
/// </summary>
public record JobCreatedEvent(
    Guid JobId,
    Guid SourceId,
    Guid? RawPostId,
    string Title,
    string CompanyName,
    string? Location,
    bool IsRemote,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    ExperienceLevel ExperienceLevel = ExperienceLevel.MidLevel,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null,
    string? SalaryCurrency = "USD",
    IReadOnlyList<string>? Skills = null,
    string? ExternalApplyUrl = null,
    DateTime? PostedAt = null
);
