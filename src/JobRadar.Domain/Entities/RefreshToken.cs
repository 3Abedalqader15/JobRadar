using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

public sealed class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedByIp { get; private set; } = string.Empty;
    
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? ReasonRevoked { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    private RefreshToken(
        Guid id, 
        Guid userId, 
        string tokenHash, 
        DateTime expiresAt, 
        string createdByIp) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        CreatedByIp = createdByIp;
    }

    public static RefreshToken Create(
        Guid userId, 
        string tokenHash, 
        DateTime expiresAt, 
        string createdByIp)
        => new(Guid.NewGuid(), userId, tokenHash, expiresAt, createdByIp);

    public void Revoke(string revokedByIp, string reason, string? replacedByTokenHash)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReasonRevoked = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
