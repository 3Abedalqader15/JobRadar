#nullable enable

using System.Security.Claims;
using JobRadar.Application.Abstractions;

namespace JobRadar.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public Guid? CompanyId
    {
        get
        {
            var companyClaim = User?.FindFirst("CompanyId")?.Value;
            return Guid.TryParse(companyClaim, out var id) ? id : null;
        }
    }

    public bool IsAdmin => Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

    public bool IsHr => Roles.Contains("HR", StringComparer.OrdinalIgnoreCase);
}
