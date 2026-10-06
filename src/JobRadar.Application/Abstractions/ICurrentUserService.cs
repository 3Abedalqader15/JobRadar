namespace JobRadar.Application.Abstractions;

/// <summary>
/// Provides access to the current authenticated user's identity and claims.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    Guid? CompanyId { get; }
    bool IsAdmin { get; }
    bool IsHr { get; }
}
