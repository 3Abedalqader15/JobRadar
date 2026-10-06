using FluentValidation;

namespace JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication;

public sealed class SubmitJobApplicationCommandValidator : AbstractValidator<SubmitJobApplicationCommand>
{
    public SubmitJobApplicationCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.ApplicantFullName)
            .NotEmpty().WithMessage("Applicant full name is required.")
            .MaximumLength(200).WithMessage("Applicant full name cannot exceed 200 characters.");

        RuleFor(x => x.ApplicantEmail)
            .NotEmpty().WithMessage("Applicant email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(320).WithMessage("Applicant email cannot exceed 320 characters.");

        RuleFor(x => x.ApplicantPhone)
            .NotEmpty().WithMessage("Applicant phone number is required.")
            .MaximumLength(50).WithMessage("Applicant phone cannot exceed 50 characters.");

        RuleFor(x => x.CvStream)
            .NotNull().WithMessage("CV file stream is required.");

        RuleFor(x => x.CvFileName)
            .NotEmpty().WithMessage("CV file name is required.");

        RuleFor(x => x.CvFileSize)
            .GreaterThan(0).WithMessage("CV file cannot be empty.")
            .LessThanOrEqualTo(5 * 1024 * 1024).WithMessage("CV file size cannot exceed 5 MB.");
    }
}
