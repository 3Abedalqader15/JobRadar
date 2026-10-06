using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

public sealed class JobApplicationAnswer : Entity<Guid>
{
    public Guid UserJobApplicationId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string AnswerText { get; private set; } = string.Empty;

    // Navigations
    public UserJobApplication? UserJobApplication { get; private set; }
    public JobApplicationQuestion? Question { get; private set; }

    private JobApplicationAnswer() { }

    private JobApplicationAnswer(
        Guid id,
        Guid userJobApplicationId,
        Guid questionId,
        string answerText) : base(id)
    {
        UserJobApplicationId = userJobApplicationId;
        QuestionId = questionId;
        AnswerText = answerText;
    }

    public static JobApplicationAnswer Create(
        Guid userJobApplicationId,
        Guid questionId,
        string answerText)
    {
        return new JobApplicationAnswer(
            Guid.NewGuid(),
            userJobApplicationId,
            questionId,
            answerText ?? string.Empty);
    }

    public void UpdateAnswer(string answerText)
    {
        AnswerText = answerText ?? string.Empty;
    }
}
