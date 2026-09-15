using JobRadar.Infrastructure.Persistence;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector;

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql("Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.pzrxrjrqjitlqgxjhqyw;Password=152003AMAf@#;SSL Mode=Require;Trust Server Certificate=true", o => o.UseVector());

using var dbContext = new AppDbContext(optionsBuilder.Options);

try
{
    Console.WriteLine("Connecting to the database...");
    var canConnect = await dbContext.Database.CanConnectAsync();
    Console.WriteLine($"Can connect: {canConnect}");

    if (canConnect)
    {
        Console.WriteLine("Fetching one job from the database...");
        var job = await dbContext.Jobs.FirstOrDefaultAsync();
        
        if (job != null)
        {
            Console.WriteLine($"Found job: {job.Title}");
        }
        else
        {
            Console.WriteLine("No jobs found in the database. Schema is ready!");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"EXCEPTION: {ex.GetType().Name}");
    Console.WriteLine($"MESSAGE: {ex.Message}");
}
