using JobRadar.Application.Abstractions;
using JobRadar.Domain.Enums;

namespace JobRadar.Infrastructure.Services;

public sealed class NullJobRealtimeNotifier : IJobRealtimeNotifier
{
    public Task NotifyJobCreatedAsync(
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
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
