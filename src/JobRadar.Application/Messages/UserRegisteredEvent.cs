using System;

namespace JobRadar.Application.Messages;

public record UserRegisteredEvent(
    Guid UserId,
    string Email,
    string FullName,
    DateTime RegisteredAt
);
