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
        var source = await dbContext.Sources.FirstOrDefaultAsync();
        if (source == null)
        {
            source = Source.Create("LinkedIn Jobs", SourceType.RssFeed, "https://example.com/feed", null, 60);
            dbContext.Sources.Add(source);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Created new source: {source.Id} ({source.Name})");
        }
        else
        {
            Console.WriteLine($"Existing source found: {source.Id} ({source.Name})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"EXCEPTION: {ex.GetType().Name}");
    Console.WriteLine($"MESSAGE: {ex.Message}");
}
