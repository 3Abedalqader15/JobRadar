using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.ManageJobApplicationQuestions;

public sealed record QuestionDto(
    Guid? Id,
    string QuestionText,
    QuestionType QuestionType,
    List<string>? Options,
    bool IsRequired,
    int DisplayOrder
);

public sealed record ManageJobApplicationQuestionsCommand(
    Guid JobId,
    List<QuestionDto> Questions
) : IRequest;
