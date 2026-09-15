using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Lookup entity representing an ATS-friendly CV layout template.
/// </summary>
public sealed class CvTemplate : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? PreviewImageUrl { get; private set; }

    /// <summary>Whether this template uses ATS-compatible formatting (single column, no tables/images).</summary>
    public bool IsAtsFriendly { get; private set; }

    // Navigations
    public IReadOnlyCollection<Cv> Cvs => _cvs.AsReadOnly();
    private readonly List<Cv> _cvs = new();

    // EF Core constructor
    private CvTemplate() { }

    private CvTemplate(Guid id, string name, string? previewImageUrl, bool isAtsFriendly) : base(id)
    {
        Name = name;
        PreviewImageUrl = previewImageUrl;
        IsAtsFriendly = isAtsFriendly;
    }

    public static CvTemplate Create(string name, bool isAtsFriendly, string? previewImageUrl = null)
        => new(Guid.NewGuid(), name, previewImageUrl, isAtsFriendly);
}
