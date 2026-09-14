namespace JobRadar.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource cannot be found.
/// Maps to HTTP 404 in <see cref="JobRadar.Api.Middleware.GlobalExceptionMiddleware"/>.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"'{name}' with key '{key}' was not found.") { }

    public NotFoundException(string message)
        : base(message) { }
}
