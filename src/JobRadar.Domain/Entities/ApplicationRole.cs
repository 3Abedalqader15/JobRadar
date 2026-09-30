using Microsoft.AspNetCore.Identity;

namespace JobRadar.Domain.Entities;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
