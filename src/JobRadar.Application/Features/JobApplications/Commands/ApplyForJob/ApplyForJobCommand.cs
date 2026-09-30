using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.ApplyForJob;

public sealed record ApplyForJobCommand(
    Guid JobId,
    Guid UserId,
    string? Notes = null) : IRequest<Guid>;
