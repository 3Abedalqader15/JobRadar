using JobRadar.Api.Hubs;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common;
using JobRadar.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Api.Services;

public sealed class SignalRJobRealtimeNotifier : IJobRealtimeNotifier
{
    private readonly IHubContext<JobHub> _hubContext;
    private readonly ILogger<SignalRJobRealtimeNotifier> _logger;

    public SignalRJobRealtimeNotifier(IHubContext<JobHub> hubContext, ILogger<SignalRJobRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyJobCreatedAsync(
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
        try
        {
            var matchingGroups = JobRelevanceGroups.ComputeMatchingGroups(
                isRemote,
                location,
                employmentType,
                experienceLevel,
                skills,
                title);

            // Crucial: No fabricated relevanceScore. Authentic data only.
            var notificationPayload = new
            {
                id = jobId,
                title = title,
                companyName = companyName,
                location = location,
                isRemote = isRemote,
                employmentType = (int)employmentType,
                experienceLevel = (int)experienceLevel,
                salaryMin = salaryMin,
                salaryMax = salaryMax,
                salaryCurrency = salaryCurrency ?? "USD",
                skills = skills,
                externalApplyUrl = externalApplyUrl,
                postedAt = postedAt,
                isNew = true
            };

            await _hubContext.Clients.Groups(matchingGroups)
                .SendAsync("ReceiveRelevantJob", notificationPayload, cancellationToken);

            _logger.LogInformation("Real-time notification sent for Job {JobId} to {Count} relevance groups.", jobId, matchingGroups.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast real-time SignalR notification for Job {JobId}", jobId);
        }
    }

    public async Task NotifyJobDeactivatedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Broadcast deactivated job id to all connected clients so active feeds remove it immediately
            await _hubContext.Clients.All
                .SendAsync("JobDeactivated", new { id = jobId }, cancellationToken);

            _logger.LogInformation("Real-time JobDeactivated broadcast sent for Job {JobId}.", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast real-time JobDeactivated for Job {JobId}", jobId);
        }
    }
}
