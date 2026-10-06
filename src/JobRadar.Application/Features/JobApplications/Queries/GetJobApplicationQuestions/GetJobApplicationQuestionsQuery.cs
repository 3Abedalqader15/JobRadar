using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Queries.GetJobApplicationQuestions;

public sealed record JobQuestionDto(
    Guid Id,
    Guid JobId,
    string QuestionText,
    QuestionType QuestionType,
    List<string>? Options,
    bool IsRequired,
    int DisplayOrder
);

public sealed record GetJobApplicationQuestionsQuery(Guid JobId) : IRequest<IReadOnlyList<JobQuestionDto>>;
