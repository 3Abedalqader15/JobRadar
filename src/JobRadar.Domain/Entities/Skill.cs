using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Lookup entity representing a technical or professional skill (e.g. "C#", "React", "Kubernetes").
/// </summary>
public sealed class Skill : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>URL-friendly unique slug, e.g. "dotnet-csharp".</summary>
    public string Slug { get; private set; } = string.Empty;

    // Navigations
    public IReadOnlyCollection<JobSkillMap> JobSkills => _jobSkills.AsReadOnly();
    private readonly List<JobSkillMap> _jobSkills = new();

    // EF Core constructor
    private Skill() { }

    private Skill(Guid id, string name, string slug) : base(id)
    {
        Name = name;
        Slug = slug;
    }

    public static Skill Create(string name, string slug) => new(Guid.NewGuid(), name, slug);
}
