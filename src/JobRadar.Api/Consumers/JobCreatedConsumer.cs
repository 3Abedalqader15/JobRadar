using JobRadar.Application.Abstractions;
using JobRadar.Application.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JobRadar.Api.Consumers;

public sealed class JobCreatedConsumer : IConsumer<JobCreatedEvent>
{
    private readonly IJobRealtimeNotifier _notifier;
    private readonly ILogger<JobCreatedConsumer> _logger;

    public JobCreatedConsumer(IJobRealtimeNotifier notifier, ILogger<JobCreatedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JobCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("JobCreatedConsumer received JobCreatedEvent for Job {JobId} ('{Title}')", msg.JobId, msg.Title);

        await _notifier.NotifyJobCreatedAsync(
            msg.JobId,
            msg.Title,
            msg.CompanyName,
            msg.Location,
            msg.IsRemote,
            msg.EmploymentType,
            msg.ExperienceLevel,
            msg.SalaryMin,
            msg.SalaryMax,
            msg.SalaryCurrency,
            msg.Skills ?? Array.Empty<string>(),
            msg.ExternalApplyUrl,
            msg.PostedAt ?? DateTime.UtcNow,
            context.CancellationToken);
    }
}
