using System.Text.Json;
using FluentValidation;
using JobRadar.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns RFC 7807 Problem Details responses.
///
/// Mapping:
///   <see cref="ValidationException"/>  → 422 Unprocessable Entity
///   <see cref="NotFoundException"/>    → 404 Not Found
///   Any other exception               → 500 Internal Server Error
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation failed.",
                Detail = "One or more validation errors occurred. See 'errors' for details.",
                Instance = context.Request.Path
            };

            await WriteProblemAsync(context, problem, StatusCodes.Status422UnprocessableEntity);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = ex.Message,
                Instance = context.Request.Path
            };

            await WriteProblemAsync(context, problem, StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "Please try again later or contact support.",
                Instance = context.Request.Path
            };

            await WriteProblemAsync(context, problem, StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        ProblemDetails problem,
        int statusCode)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
