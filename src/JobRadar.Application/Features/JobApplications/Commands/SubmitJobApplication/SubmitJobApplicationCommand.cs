using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication;

public sealed record AnswerSubmissionDto(
    Guid QuestionId,
    string AnswerText
);

public sealed record SubmitJobApplicationCommand(
    Guid JobId,
    Guid UserId,
    string ApplicantFullName,
    string ApplicantEmail,
    string ApplicantPhone,
    Stream CvStream,
    string CvFileName,
    string CvContentType,
    long CvFileSize,
    IReadOnlyList<AnswerSubmissionDto> Answers
) : IRequest<Guid>;
