using FluentValidation;
using JobRadar.Domain.Enums;

namespace JobRadar.Application.Features.JobApplications.Commands.ManageJobApplicationQuestions;

public sealed class ManageJobApplicationQuestionsCommandValidator : AbstractValidator<ManageJobApplicationQuestionsCommand>
{
    public ManageJobApplicationQuestionsCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("Job ID is required.");

        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.QuestionText)
                .NotEmpty().WithMessage("Question text is required.")
                .MaximumLength(1000).WithMessage("Question text cannot exceed 1000 characters.");

            q.RuleFor(x => x.DisplayOrder)
                .GreaterThanOrEqualTo(0).WithMessage("Display order must be non-negative.");

            q.When(x => x.QuestionType == QuestionType.MultipleChoice, () =>
            {
                q.RuleFor(x => x.Options)
                    .NotNull().WithMessage("Options must be provided for multiple-choice questions.")
                    .Must(opts => opts != null && opts.Count >= 2).WithMessage("Multiple-choice questions must have at least 2 options.");

                q.RuleForEach(x => x.Options)
                    .NotEmpty().WithMessage("Option value cannot be empty.");
            });
        });
    }
}
