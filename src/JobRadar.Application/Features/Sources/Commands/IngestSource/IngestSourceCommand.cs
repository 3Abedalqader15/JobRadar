using MediatR;

namespace JobRadar.Application.Features.Sources.Commands.IngestSource;

public record IngestSourceCommand(Guid SourceId) : IRequest;
