using JobRadar.Application.Abstractions;
using JobRadar.Application.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JobRadar.Api.Consumers;

public sealed class JobDeactivatedConsumer : IConsumer<JobDeactivatedEvent>
{
    private readonly IJobRealtimeNotifier _notifier;
    private readonly ILogger<JobDeactivatedConsumer> _logger;

    public JobDeactivatedConsumer(IJobRealtimeNotifier notifier, ILogger<JobDeactivatedConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JobDeactivatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("JobDeactivatedConsumer received JobDeactivatedEvent for Job {JobId}", msg.JobId);

        await _notifier.NotifyJobDeactivatedAsync(msg.JobId, context.CancellationToken);
    }
}
