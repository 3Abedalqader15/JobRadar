using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

public sealed class JobApplicationQuestion : Entity<Guid>
{
    public Guid JobId { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public QuestionType QuestionType { get; private set; }
    public string? OptionsJson { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }

    // Navigations
    public Job? Job { get; private set; }
    public IReadOnlyCollection<JobApplicationAnswer> Answers => _answers.AsReadOnly();
    private readonly List<JobApplicationAnswer> _answers = new();

    private JobApplicationQuestion() { }

    private JobApplicationQuestion(
        Guid id,
        Guid jobId,
        string questionText,
        QuestionType questionType,
        string? optionsJson,
        bool isRequired,
        int displayOrder) : base(id)
    {
        JobId = jobId;
        QuestionText = questionText;
        QuestionType = questionType;
        OptionsJson = optionsJson;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }

    public static JobApplicationQuestion Create(
        Guid jobId,
        string questionText,
        QuestionType questionType,
        string? optionsJson = null,
        bool isRequired = false,
        int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            throw new ArgumentException("Question text cannot be empty.", nameof(questionText));

        return new JobApplicationQuestion(
            Guid.NewGuid(),
            jobId,
            questionText.Trim(),
            questionType,
            optionsJson,
            isRequired,
            displayOrder);
    }

    public void Update(
        string questionText,
        QuestionType questionType,
        string? optionsJson,
        bool isRequired,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            throw new ArgumentException("Question text cannot be empty.", nameof(questionText));

        QuestionText = questionText.Trim();
        QuestionType = questionType;
        OptionsJson = optionsJson;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }
}
