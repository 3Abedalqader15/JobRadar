using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using WTelegram;
using TL;

namespace JobRadar.Workers.Ingestion.Services;

public class TelegramFetcher : IDisposable
{
    private readonly ILogger<TelegramFetcher> _logger;
    private readonly IConfiguration _config;
    private readonly AsyncRetryPolicy _retryPolicy;
    private Client? _client;

    public TelegramFetcher(ILogger<TelegramFetcher> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Error fetching from Telegram. Retrying {RetryCount} in {DelaySeconds}s", retryCount, timeSpan.TotalSeconds);
                });
    }

    private async Task<Client> GetClientAsync()
    {
        if (_client != null) return _client;

        var apiId = _config.GetValue<int>("Telegram:ApiId");
        var apiHash = _config.GetValue<string>("Telegram:ApiHash");
        
        if (apiId == 0 || string.IsNullOrEmpty(apiHash))
        {
            throw new InvalidOperationException("Telegram API ID or Hash is missing in configuration.");
        }

        // Redirect WTelegram logs to Serilog
        WTelegram.Helpers.Log = (level, message) =>
        {
            switch (level)
            {
                case 1: _logger.LogTrace(message); break;
                case 2: _logger.LogDebug(message); break;
                case 3: _logger.LogInformation(message); break;
                case 4: _logger.LogWarning(message); break;
                case 5: _logger.LogError(message); break;
                default: _logger.LogInformation(message); break;
            }
        };

        _client = new Client(config => config switch
        {
            "api_id" => apiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "api_hash" => apiHash,
            "phone_number" => _config["Telegram:PhoneNumber"],
            "verification_code" => throw new InvalidOperationException("Interactive authentication is required but this is running as a worker. Please provide a valid session file."),
            _ => null
        });

        await _client.LoginUserIfNeeded();
        return _client;
    }

    public async Task<List<ParsedTelegramMessage>> FetchRecentMessagesAsync(string channelUsername, int limit = 50)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var client = await GetClientAsync();
            var resolvedPeer = await client.Contacts_ResolveUsername(channelUsername);
            if (resolvedPeer.UserOrChat is not TL.Channel channel)
            {
                throw new InvalidOperationException($"Username {channelUsername} is not a channel.");
            }

            var messages = await client.Messages_GetHistory(channel, limit: limit);
            var result = new List<ParsedTelegramMessage>();

            foreach (var msgBase in messages.Messages)
            {
                if (msgBase is TL.Message msg && !string.IsNullOrWhiteSpace(msg.message))
                {
                    result.Add(new ParsedTelegramMessage(
                        MessageId: msg.id,
                        Content: msg.message,
                        Date: msg.Date
                    ));
                }
            }

            return result;
        });
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record ParsedTelegramMessage(int MessageId, string Content, DateTime Date);
