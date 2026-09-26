using System;
using System.Threading.Tasks;
using Npgsql;

namespace JobRadar.Tester;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("    Inserting LinkedIn Source into DB");
        Console.WriteLine("==================================================");

        var connectionString = "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.pzrxrjrqjitlqgxjhqyw;Password=152003AMAf@#;SSL Mode=Require;Trust Server Certificate=true";
        
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(@"
            UPDATE ""sources"" SET ""type"" = 'RssFeed' WHERE ""type"" = 'Rss';
        ");
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("\nUpdated Rss to RssFeed in DB!");
    }
}
