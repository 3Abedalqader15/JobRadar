using System.Globalization;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

namespace JobRadar.Infrastructure.Fetchers;

internal sealed class TelegramFetcher : ISourceFetcher, IDisposable
{
    private readonly ILogger<TelegramFetcher> _logger;
    private readonly IConfiguration _configuration;
    private Client? _client;
    private readonly string _apiId;
    private readonly string _apiHash;
    private readonly string _sessionPath;

    public TelegramFetcher(ILogger<TelegramFetcher> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        _apiId = _configuration["Telegram:ApiId"] ?? "";
        _apiHash = _configuration["Telegram:ApiHash"] ?? "";
        _sessionPath = _configuration["Telegram:SessionPath"] ?? "telegram.session";
    }

    public bool CanHandle(SourceType type) => type == SourceType.TelegramChannel;

    private async Task EnsureConnectedAsync()
    {
        if (_client != null) return;

        // Note: WTelegramClient by default stores the session in a local file.
        // We configure it to use the explicit path from config.
        // It encrypts the session file based on the api_hash (default).
        // First-time manual login is required to generate this session file.
        // See documentation: WTelegramClient login
        WTelegram.Helpers.Log = (lvl, str) => _logger.LogTrace("WTelegramClient: {Message}", str);
        
        _client = new Client(Config, new FileStream(_sessionPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
        
        await _client.LoginUserIfNeeded();
    }

    private string? Config(string what)
    {
        return what switch
        {
            "api_id" => _apiId,
            "api_hash" => _apiHash,
            "phone_number" => _configuration["Telegram:PhoneNumber"] ?? throw new InvalidOperationException("Telegram:PhoneNumber config needed for first login."),
            "verification_code" => throw new InvalidOperationException("First-time authentication requires manual interaction to provide verification_code."),
            _ => null
        };
    }

    public async Task<IReadOnlyList<FetchedPostInfo>> FetchPostsAsync(Source source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiId) || string.IsNullOrWhiteSpace(_apiHash))
        {
            _logger.LogWarning("Telegram API ID or Hash is missing in configuration. Skipping Telegram fetch for source {Name}.", source.Name);
            return Array.Empty<FetchedPostInfo>();
        }

        await EnsureConnectedAsync();
        
        // We expect source.Url to be the channel username (e.g. @jobs_channel) or channel ID.
        var username = source.Url.TrimStart('@');
        
        var resolved = await _client!.Contacts_ResolveUsername(username);
        if (resolved.UserOrChat is not Channel channel)
        {
            throw new InvalidOperationException($"Could not resolve {username} to a Telegram channel.");
        }

        int offsetId = 0;
        if (int.TryParse(source.LastSyncIdentifier, out var lastId))
        {
            offsetId = lastId;
        }

        // Fetch recent messages. If offsetId is provided, we fetch newer messages.
        // WTelegramClient's GetHistoryAsync can fetch based on offset.
        var messages = await _client.Messages_GetHistory(channel, limit: 50, offset_id: 0); 
        
        var results = new List<FetchedPostInfo>();

        foreach (var msgBase in messages.Messages)
        {
            if (msgBase is Message msg)
            {
                // Ignore messages older than or equal to our last sync id
                if (msg.id <= offsetId) continue;
                
                results.Add(new FetchedPostInfo(
                    RawUrl: $"https://t.me/{username}/{msg.id}",
                    RawContent: msg.message,
                    SyncIdentifier: msg.id.ToString(CultureInfo.InvariantCulture),
                    FetchedAt: msg.date
                ));
            }
        }

        // Order from oldest to newest so sync identifier gets updated correctly
        return results.OrderBy(r => int.TryParse(r.SyncIdentifier, out var parsedId) ? parsedId : 0).ToList();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
