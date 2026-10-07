using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.ProcessCvAnalysis;

public sealed record ProcessCvAnalysisCommand(Guid ApplicationId, bool Force = false) : IRequest;
