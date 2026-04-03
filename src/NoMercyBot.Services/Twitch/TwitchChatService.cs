using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Twitch;

public class TwitchChatService : IDisposable
{
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger<TwitchChatService> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScope _scope;

    public static string _userId = string.Empty;
    public static string _userName = string.Empty;
    private static string _accessToken = string.Empty;

    public static string _botUserId = string.Empty;
    public static string _botUserName = string.Empty;
    private static string _botAccessToken = string.Empty;
    private static string _botAppAccessToken = string.Empty;

    /// <summary>
    /// Returns the app access token for bot badge if available, otherwise the user token.
    /// </summary>
    private static string BotChatToken =>
        !string.IsNullOrEmpty(_botAppAccessToken) ? _botAppAccessToken : _botAccessToken;

    public bool IsReady { get; private set; }

    public TwitchChatService(
        ILogger<TwitchChatService> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        TwitchApiService twitchApiService
    )
    {
        _logger = logger;
        _config = config;
        _twitchApiService = twitchApiService;
        _scope = scopeFactory.CreateScope();
        AppDbContext dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        BotAccount? botAccount = dbContext.BotAccounts.FirstOrDefault();
        Service? twitchService = dbContext.Services.FirstOrDefault(s => s.Name == "Twitch");

        if (twitchService == null || string.IsNullOrEmpty(twitchService.AccessToken))
        {
            _logger.LogWarning(
                "No Twitch service found or missing access token. Chat service not ready."
            );
            return;
        }

        if (botAccount == null || string.IsNullOrEmpty(botAccount.AccessToken))
        {
            _logger.LogWarning(
                "No bot account found or missing access token. Chat service not ready."
            );
            return;
        }

        User botUser = _twitchApiService.GetOrFetchUser(name: botAccount.Username).Result;

        _userId = twitchService.UserId;
        _userName = twitchService.UserName;
        _accessToken = twitchService.AccessToken;
        _botUserId = botUser.Id;
        _botUserName = botUser.Username;
        _botAccessToken = botAccount.AccessToken;
        _botAppAccessToken = botAccount.AppAccessToken;
        IsReady = true;
    }

    private void RefreshClients()
    {
        using IServiceScope scope = _scope
            .ServiceProvider.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        BotAccount? botAccount = dbContext.BotAccounts.FirstOrDefault();
        Service? twitchService = dbContext.Services.FirstOrDefault(s => s.Name == "Twitch");

        if (twitchService == null || string.IsNullOrEmpty(twitchService.AccessToken))
        {
            _logger.LogWarning(
                "No Twitch service found or missing access token. Chat service not ready."
            );
            IsReady = false;
            return;
        }

        if (botAccount == null || string.IsNullOrEmpty(botAccount.AccessToken))
        {
            _logger.LogWarning(
                "No bot account found or missing access token. Chat service not ready."
            );
            IsReady = false;
            return;
        }

        User botUser = _twitchApiService.GetOrFetchUser(name: botAccount.Username).Result;

        _userId = twitchService.UserId;
        _userName = twitchService.UserName;
        _accessToken = twitchService.AccessToken;
        _botUserId = botUser.Id;
        _botUserName = botUser.Username;
        _botAccessToken = botAccount.AccessToken;
        _botAppAccessToken = botAccount.AppAccessToken;
        IsReady = true;
    }

    public async Task SendMessageAsUser(string channel, string message)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send message.");
            return;
        }
        try
        {
            await _twitchApiService.SendMessage(_userId, message, _userId, _accessToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send message as user. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _userId, _accessToken);
        }
    }

    public async Task SendReplyAsUser(string channel, string message, string replyToMessageId)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send reply.");
            return;
        }
        try
        {
            await _twitchApiService.SendMessage(
                _userId,
                message,
                _userId,
                _accessToken,
                replyToMessageId
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as user. Falling back to non-reply message.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _userId, _accessToken);
        }
    }

    public async Task SendMessageAsBot(string channel, string message)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send message.");
            return;
        }
        try
        {
            foreach (string text in SplitMessageIntoChunks(message, 450))
            {
                await _twitchApiService.SendMessage(_userId, text, _botUserId, BotChatToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send message as bot. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _botUserId, BotChatToken);
        }
    }

    public async Task SendReplyAsBot(string channel, string message, string replyToMessageId)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send reply.");
            return;
        }
        try
        {
            foreach (string text in SplitMessageIntoChunks(message, 450))
            {
                await _twitchApiService.SendMessage(
                    _userId,
                    text,
                    _botUserId,
                    BotChatToken,
                    replyToMessageId
                );
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Falling back to non-reply message.");
            RefreshClients();
            foreach (string text in SplitMessageIntoChunks(message, 450))
            {
                await _twitchApiService.SendMessage(_userId, text, _botUserId, BotChatToken);
            }
        }
    }

    public async Task SendAnnouncementAsBot(
        string broadcasterId,
        string message,
        string? color = "primary"
    )
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send announcement.");
            return;
        }
        await _twitchApiService.SendAnnouncement(
            broadcasterId,
            _botUserId,
            message,
            color,
            _botAccessToken
        );
    }

    public async Task SendOneOffMessage(string channelId, string message)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send message.");
            return;
        }
        try
        {
            await _twitchApiService.SendMessage(
                channelId,
                message + " #NMBot",
                _userId,
                _accessToken
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(
                channelId,
                message + " #NMBot",
                _userId,
                _accessToken
            );
        }
    }

    public async Task SendOneOffMessageAsBot(string channel, string message)
    {
        if (!IsReady)
            RefreshClients();
        if (!IsReady)
        {
            _logger.LogWarning("Chat service not ready. Cannot send message.");
            return;
        }
        try
        {
            User channelUser = await _twitchApiService.GetOrFetchUser(name: channel);

            await _twitchApiService.SendMessage(
                channelUser.Id,
                message + " #NMBot",
                _botUserId,
                BotChatToken
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Attempting to refresh clients.");
            RefreshClients();

            User channelUser = await _twitchApiService.GetOrFetchUser(name: channel);

            await _twitchApiService.SendMessage(
                channelUser.Id,
                message + " #NMBot",
                _botUserId,
                BotChatToken
            );
        }
    }

    public async Task<string[]> GetChatters(string channel)
    {
        RefreshClients();
        // await _twitchApiService.OnExistingUsersDetected
        //     += (sender, e) => _logger.LogInformation($"Existing users detected in channel {channel}: {string.Join(", ", e.Users)}");
        return [];
    }

    public List<string> SplitMessageIntoChunks(string message, int chunkLength)
    {
        List<string> chunks = [];
        if (string.IsNullOrEmpty(message) || chunkLength <= 0)
            return chunks;

        string[] sentences = Regex.Split(message, @"(?<=[.!?])\s+");
        StringBuilder currentChunk = new();

        foreach (string? sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length + 1 <= chunkLength)
            {
                if (0 < currentChunk.Length)
                    currentChunk.Append(" ");
                currentChunk.Append(sentence);
            }
            else
            {
                if (0 < currentChunk.Length)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                if (sentence.Length <= chunkLength)
                    currentChunk.Append(sentence);
                else
                    SplitSentenceIntoChunks(sentence, chunkLength, chunks);
            }
        }

        if (0 < currentChunk.Length)
            chunks.Add(currentChunk.ToString().Trim());

        return chunks;
    }

    private void SplitSentenceIntoChunks(string sentence, int chunkLength, List<string> chunks)
    {
        string[] words = sentence.Split(' ');
        StringBuilder currentChunk = new();

        foreach (string? word in words)
        {
            if (chunkLength < word.Length)
            {
                if (0 < currentChunk.Length)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                SplitWordIntoChunks(word, chunkLength, chunks);
            }
            else
            {
                if (currentChunk.Length + word.Length + 1 <= chunkLength)
                {
                    if (0 < currentChunk.Length)
                        currentChunk.Append(" ");
                    currentChunk.Append(word);
                }
                else
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                    currentChunk.Append(word);
                }
            }
        }

        if (0 < currentChunk.Length)
            chunks.Add(currentChunk.ToString().Trim());
    }

    private void SplitWordIntoChunks(string word, int chunkLength, List<string> chunks)
    {
        for (int i = 0; i < word.Length; i += chunkLength)
        {
            int length = Math.Min(chunkLength, word.Length - i);
            chunks.Add(word.Substring(i, length));
        }
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
