using MediatR;
using Microsoft.AspNetCore.Identity;
using JobRadar.Domain.Entities;
using JobRadar.Application.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IPublishEndpoint publishEndpoint,
        ILogger<RegisterCommandHandler> logger)
    {
        _userManager = userManager;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = ApplicationUser.Create(request.Email, request.FullName);
        user.EmailConfirmed = true; // TODO: Configuration flag "Identity:RequireConfirmedEmail" will dictate this when IEmailSender is ready

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return new RegisterResult(false, result.Errors.Select(e => e.Description).ToArray());
        }

        // Assign role: only "HR" is whitelisted beyond default "User"
        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "User", "HR" };
        var roleToAssign = allowedRoles.Contains(request.Role) ? request.Role : "User";
        await _userManager.AddToRoleAsync(user, roleToAssign);

        // Bypassing normal EF outbox transaction because UserManager.CreateAsync calls SaveChangesAsync internally.
        // This is safe since the user is successfully created in the DB at this point.
        await _publishEndpoint.Publish(new UserRegisteredEvent(user.Id, user.Email!, user.FullName, user.CreatedAt), cancellationToken);

        _logger.LogInformation("User {UserId} registered successfully with role {Role}.", user.Id, roleToAssign);

        return new RegisterResult(true, Array.Empty<string>());
    }
}
