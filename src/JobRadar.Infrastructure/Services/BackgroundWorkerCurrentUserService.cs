using JobRadar.Application.Abstractions;

namespace JobRadar.Infrastructure.Services;

public sealed class BackgroundWorkerCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public string? Email => "system@jobradar.local";
    public IReadOnlyList<string> Roles => ["System", "Admin"];
    public Guid? CompanyId => null;
    public bool IsAdmin => true;
    public bool IsHr => false;
}
