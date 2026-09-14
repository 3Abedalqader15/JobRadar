using Serilog.Context;

namespace JobRadar.Api.Middleware;

/// <summary>
/// Reads the <c>X-Correlation-ID</c> request header (or generates a new GUID),
/// pushes it into Serilog's LogContext so every log line within the request
/// includes it, and writes it back to the response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push into Serilog's ambient context — all log events during this request
        // will automatically include CorrelationId in their properties.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
