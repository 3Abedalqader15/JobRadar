using JobRadar.Api.Middleware;
using JobRadar.Application;
using JobRadar.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddSignalR();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobRadar.Application.Abstractions.IJobRealtimeNotifier, JobRadar.Api.Services.SignalRJobRealtimeNotifier>();

builder.Services.AddIdentityCore<JobRadar.Domain.Entities.ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
    
    options.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("Identity:RequireConfirmedEmail", false);
})
.AddRoles<JobRadar.Domain.Entities.ApplicationRole>()
.AddEntityFrameworkStores<JobRadar.Infrastructure.Persistence.AppDbContext>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
.AddJwtBearer("Bearer", options =>
{
    var secret = builder.Configuration["Jwt:Secret"] ?? "fallback_secret_for_dev_only_must_change";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory();
});

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

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Caching & Rate Limiting ─────────────────────────────────────────────────
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration = builder.Configuration.GetConnectionString("Redis");
//     options.InstanceName = "JobRadar_";
// });

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
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<JobRadar.Api.Hubs.JobHub>(JobRadar.Api.Hubs.JobHub.HubUrl);

// ── Seed Data ──────────────────────────────────────────────────────────────
await JobRadar.Infrastructure.Persistence.IdentityDataSeeder.SeedAsync(app.Services);

await app.RunAsync();

// Make Program accessible for integration tests
public partial class Program { }
