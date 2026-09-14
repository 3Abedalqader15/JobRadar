using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;

public sealed record DeleteJobPostingCommand(Guid Id) : IRequest;
