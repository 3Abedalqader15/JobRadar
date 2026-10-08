using JobRadar.Domain.Enums;

namespace JobRadar.Application.Abstractions;

public interface IJobRealtimeNotifier
{
    Task NotifyJobCreatedAsync(
        Guid jobId,
        string title,
        string companyName,
        string? location,
        bool isRemote,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        decimal? salaryMin,
        decimal? salaryMax,
        string? salaryCurrency,
        IReadOnlyList<string> skills,
        string? externalApplyUrl,
        DateTime postedAt,
        CancellationToken cancellationToken = default);

    Task NotifyJobDeactivatedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
