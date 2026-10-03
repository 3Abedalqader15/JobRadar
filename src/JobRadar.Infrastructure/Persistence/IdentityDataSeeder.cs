using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Persistence;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentityDataSeeder));
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            // 1. Seed standard roles (Admin, User, HR)
            string[] defaultRoles = ["Admin", "User", "HR"];
            foreach (var roleName in defaultRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    logger.LogInformation("Creating role: {RoleName}", roleName);
                    var roleResult = await roleManager.CreateAsync(new ApplicationRole(roleName));
                    if (!roleResult.Succeeded)
                    {
                        logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
            }

            // 2. Seed Default Admin User
            var adminEmail = configuration["AdminSeed:Email"];
            var adminPassword = configuration["AdminSeed:Password"];
            var adminFullName = configuration["AdminSeed:FullName"] ?? "JobRadar Administrator";

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("AdminSeed:Email or AdminSeed:Password configuration is missing or empty. Skipping admin user seeding entirely.");
            }
            else
            {
                var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
                if (existingAdmin == null)
                {
                    logger.LogInformation("Seeding default admin user: {AdminEmail}", adminEmail);
                    var admin = ApplicationUser.Create(
                        email: adminEmail,
                        fullName: adminFullName,
                        experienceLevel: ExperienceLevel.Lead
                    );

                    admin.EmailConfirmed = true;

                    var createResult = await userManager.CreateAsync(admin, adminPassword);
                    if (createResult.Succeeded)
                    {
                        var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
                        if (roleResult.Succeeded)
                        {
                            logger.LogInformation("Admin user {AdminEmail} created and assigned to 'Admin' role successfully.", adminEmail);
                        }
                        else
                        {
                            logger.LogError("Failed to assign 'Admin' role to {AdminEmail}: {Errors}", adminEmail, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        }
                    }
                    else
                    {
                        logger.LogError("Failed to create admin user {AdminEmail}: {Errors}", adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    // Ensure existing admin is in Admin role
                    if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
                    {
                        logger.LogInformation("Assigning 'Admin' role to existing user {AdminEmail}", adminEmail);
                        await userManager.AddToRoleAsync(existingAdmin, "Admin");
                    }
                }
            }

            // 3. Seed the "Manual" source (well-known ID used for Admin/HR job postings)
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var manualSourceId = new Guid("00000000-0000-0000-0000-000000000001");
            var manualSourceExists = await db.Sources.AnyAsync(s => s.Id == manualSourceId);
            if (!manualSourceExists)
            {
                logger.LogInformation("Seeding Manual source.");
                var source = Source.Create(
                    name: "Manual",
                    type: SourceType.ManualShare,
                    url: "https://jobradar.com",
                    fetchIntervalMinutes: 0
                );
                // Override auto-generated ID with the well-known manual source ID
                db.Entry(source).Property("Id").CurrentValue = manualSourceId;
                db.Sources.Add(source);
                await db.SaveChangesAsync();
                logger.LogInformation("Manual source seeded with ID {Id}.", manualSourceId);
            }

            // 4. Ensure PostgreSQL Full-Text, Trigram, and Composite Performance Indexes
            try
            {
                logger.LogInformation("Ensuring PostgreSQL performance indexes (pg_trgm, FTS, GIN)...");
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE EXTENSION IF NOT EXISTS pg_trgm;
                    CREATE INDEX IF NOT EXISTS ix_jobs_trgm_title ON jobs USING gin (title gin_trgm_ops);
                    CREATE INDEX IF NOT EXISTS ix_jobs_trgm_company ON jobs USING gin (company_name gin_trgm_ops);
                    CREATE INDEX IF NOT EXISTS ix_jobs_trgm_location ON jobs USING gin (location gin_trgm_ops);
                    CREATE INDEX IF NOT EXISTS ix_jobs_active_posted_desc ON jobs (is_active, posted_at DESC);
                    CREATE INDEX IF NOT EXISTS ix_jobs_active_salary ON jobs (is_active, salary_min, salary_max);
                    CREATE INDEX IF NOT EXISTS ix_jobs_fts ON jobs USING gin (to_tsvector('english', title || ' ' || company_name || ' ' || coalesce(location, '') || ' ' || description));
                ");
                logger.LogInformation("PostgreSQL performance indexes verified successfully.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not apply PostgreSQL performance indexes automatically. Skipping.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding Identity data.");
            throw;
        }
    }
}
