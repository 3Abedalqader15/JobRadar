using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a user's CV stored as structured JSON (personal info, experiences, education, skills, projects).
/// The CV can be rendered to PDF via QuestPDF and analysed against job descriptions via LLM.
/// </summary>
public sealed class Cv : Entity<Guid>, IAggregateRoot
{
    public Guid UserId { get; private set; }

    /// <summary>The selected layout template. Null means no template / plain export.</summary>
    public Guid? TemplateId { get; private set; }

    /// <summary>
    /// Structured CV data serialised as JSON.
    /// Schema: { personalInfo, experiences[], education[], skills[], projects[] }
    /// </summary>
    public string ContentJson { get; private set; } = "{}";

    /// <summary>URL to the most recently generated PDF file (e.g. Supabase Storage URL).</summary>
    public string? GeneratedPdfUrl { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigations
    public User? User { get; private set; }
    public CvTemplate? Template { get; private set; }

    public IReadOnlyCollection<CvJobMatchAnalysis> Analyses => _analyses.AsReadOnly();
    private readonly List<CvJobMatchAnalysis> _analyses = new();

    // EF Core constructor
    private Cv() { }

    private Cv(Guid id, Guid userId, Guid? templateId) : base(id)
    {
        UserId = userId;
        TemplateId = templateId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Cv Create(Guid userId, Guid? templateId = null)
        => new(Guid.NewGuid(), userId, templateId);

    public void UpdateContent(string contentJson)
    {
        ContentJson = contentJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGeneratedPdfUrl(string url)
    {
        GeneratedPdfUrl = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeTemplate(Guid? templateId)
    {
        TemplateId = templateId;
        UpdatedAt = DateTime.UtcNow;
    }
}
