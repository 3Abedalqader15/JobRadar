using System;
using System.Threading.Tasks;
using Npgsql;

namespace JobRadar.Tester;

class Program
{
    static async Task Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

        using var client = new HttpClient();
        var embedUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent?key={apiKey}";

        string[] testQueries = ["مطور واجهات عن بعد", "Senior .NET Engineer", "Affirm backend"];

        foreach (var query in testQueries)
        {
            Console.WriteLine($"\n=== Testing Semantic Search for: '{query}' ===");
            var text = $"title: none | text: {query}";
            var req = new { content = new { parts = new[] { new { text } } }, output_dimensionality = 1536 };
            using var httpContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(req), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(embedUrl, httpContent);
            var respJson = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(respJson);
            var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
            var floatList = new List<float>();
            foreach (var val in values.EnumerateArray()) floatList.Add(val.GetSingle());
            var vectorStr = "[" + string.Join(",", floatList) + "]";

            await using var ds = NpgsqlDataSource.Create(connectionString);
            await using var cmd = ds.CreateCommand(@"
                SELECT title, company_name, is_remote, (1 - (embedding <=> @vec::vector)) as relevance_score
                FROM jobs
                WHERE is_active = true AND embedding IS NOT NULL
                ORDER BY embedding <=> @vec::vector
                LIMIT 3;
            ");
            cmd.Parameters.AddWithValue("vec", vectorStr);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var title = reader.GetString(0);
                var company = reader.GetString(1);
                var remote = reader.GetBoolean(2);
                var score = reader.GetDouble(3);
                Console.WriteLine($"  -> Match: {score:P1} | {title} at {company} (Remote: {remote})");
            }
        }
    }
}
