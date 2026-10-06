using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a registered company/employer on the JobRadar platform.
/// Officially posted jobs and HR representatives are scoped to a Company.
/// </summary>
public sealed class Company : Entity<Guid>, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigations
    public IReadOnlyCollection<Job> Jobs => _jobs.AsReadOnly();
    private readonly List<Job> _jobs = new();

    public IReadOnlyCollection<ApplicationUser> Users => _users.AsReadOnly();
    private readonly List<ApplicationUser> _users = new();

    // EF Core constructor
    private Company() { }

    private Company(Guid id, string name, string slug, string? logoUrl) : base(id)
    {
        Name = name;
        Slug = slug;
        LogoUrl = logoUrl;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Company Create(string name, string slug, string? logoUrl = null)
        => new(Guid.NewGuid(), name.Trim(), slug.Trim().ToLowerInvariant(), logoUrl?.Trim());

    public void Update(string name, string slug, string? logoUrl = null)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        LogoUrl = logoUrl?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
