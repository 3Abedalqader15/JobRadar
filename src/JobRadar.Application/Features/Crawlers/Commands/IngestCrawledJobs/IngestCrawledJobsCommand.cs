using MediatR;

namespace JobRadar.Application.Features.Crawlers.Commands.IngestCrawledJobs;

public sealed record IngestCrawledJobsCommand(string? ProviderName = null) : IRequest<int>;
