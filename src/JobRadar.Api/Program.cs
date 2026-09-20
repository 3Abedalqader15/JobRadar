using JobRadar.Api.Middleware;
using JobRadar.Application;
using JobRadar.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "JobRadar API",
        Version = "v1",
        Description = "Job aggregation and tracking API powered by pgvector semantic search."
    });
});

// ── Caching & Rate Limiting ─────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "JobRadar_";
});

var searchJobsPolicy = builder.Configuration.GetSection("RateLimiting:SearchJobsPolicy");
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("SearchJobsPolicy", opt =>
    {
        opt.PermitLimit = searchJobsPolicy.GetValue<int>("PermitLimit", 20);
        opt.Window = TimeSpan.FromSeconds(searchJobsPolicy.GetValue<int>("WindowSeconds", 60));
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = searchJobsPolicy.GetValue<int>("QueueLimit", 5);
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
// 1. Correlation ID (outermost — applies to all requests including Swagger)
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Global exception handler — converts exceptions to RFC 7807 Problem Details
app.UseMiddleware<GlobalExceptionMiddleware>();

// 3. Serilog request logging
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobRadar API v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
