using JobRadar.Application.Features.JobApplications.Commands.ProcessCvAnalysis;
using JobRadar.Application.Messages;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that receives <see cref="CvAnalysisRequestedEvent"/> and delegates
/// execution to MediatR's <see cref="ProcessCvAnalysisCommand"/>.
/// </summary>
public sealed class CvAnalysisConsumer : IConsumer<CvAnalysisRequestedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CvAnalysisConsumer> _logger;

    public CvAnalysisConsumer(IMediator mediator, ILogger<CvAnalysisConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CvAnalysisRequestedEvent> context)
    {
        _logger.LogInformation(
            "Consuming CvAnalysisRequestedEvent for application {ApplicationId}",
            context.Message.ApplicationId);

        await _mediator.Send(
            new ProcessCvAnalysisCommand(context.Message.ApplicationId),
            context.CancellationToken);
    }
}
