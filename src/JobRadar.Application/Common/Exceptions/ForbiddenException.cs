namespace JobRadar.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts to access or modify a resource outside their authorized scope.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to access or modify this resource.")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
