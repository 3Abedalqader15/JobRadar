using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a registered user of the JobRadar platform.
/// </summary>
public sealed class User : Entity<Guid>, IAggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }

    /// <summary>Preferred job title keywords used for smart matching and notifications.</summary>
    public string[] PreferredJobTitles { get; private set; } = Array.Empty<string>();

    /// <summary>Preferred location keywords (city, country, "Remote", etc.).</summary>
    public string[] PreferredLocations { get; private set; } = Array.Empty<string>();

    /// <summary>Preferred skill names used for feed personalisation and notifications.</summary>
    public string[] PreferredSkills { get; private set; } = Array.Empty<string>();

    public ExperienceLevel ExperienceLevel { get; private set; }

    /// <summary>Telegram chat ID for the linked Telegram account — used by the notification bot.</summary>
    public string? TelegramChatId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigations
    public IReadOnlyCollection<Source> AddedSources => _addedSources.AsReadOnly();
    private readonly List<Source> _addedSources = new();

    public IReadOnlyCollection<Cv> Cvs => _cvs.AsReadOnly();
    private readonly List<Cv> _cvs = new();

    public IReadOnlyCollection<Notification> Notifications => _notifications.AsReadOnly();
    private readonly List<Notification> _notifications = new();

    public IReadOnlyCollection<UserSavedJob> SavedJobs => _savedJobs.AsReadOnly();
    private readonly List<UserSavedJob> _savedJobs = new();

    public IReadOnlyCollection<UserJobApplication> Applications => _applications.AsReadOnly();
    private readonly List<UserJobApplication> _applications = new();

    // EF Core constructor
    private User() { }

    private User(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        ExperienceLevel experienceLevel) : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Phone = phone;
        ExperienceLevel = experienceLevel;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static User Create(
        string email,
        string passwordHash,
        string fullName,
        string? phone = null,
        ExperienceLevel experienceLevel = ExperienceLevel.MidLevel)
        => new(Guid.NewGuid(), email, passwordHash, fullName, phone, experienceLevel);

    public void UpdateProfile(string fullName, string? phone, ExperienceLevel experienceLevel)
    {
        FullName = fullName;
        Phone = phone;
        ExperienceLevel = experienceLevel;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePreferences(
        string[] preferredJobTitles,
        string[] preferredLocations,
        string[] preferredSkills)
    {
        PreferredJobTitles = preferredJobTitles;
        PreferredLocations = preferredLocations;
        PreferredSkills = preferredSkills;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkTelegram(string telegramChatId)
    {
        TelegramChatId = telegramChatId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnlinkTelegram()
    {
        TelegramChatId = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
