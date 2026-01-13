using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercyBot.Services.Twitch;

public class TwitchChatService : IDisposable
{
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger<TwitchChatService> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceScope _scope;

    public static string _userId;
    public static string _userName;
    private static string _accessToken;

    public static string _botUserId;
    public static string _botUserName;
    private static string _botAccessToken;

    public TwitchChatService(ILogger<TwitchChatService> logger, IConfiguration config,
        IServiceScopeFactory scopeFactory, TwitchApiService twitchApiService)
    {
        _logger = logger;
        _config = config;
        _twitchApiService = twitchApiService;
        _scope = scopeFactory.CreateScope();
        AppDbContext dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        BotAccount? botAccount = dbContext.BotAccounts.FirstOrDefault();
        Service? twitchService = dbContext.Services.FirstOrDefault(s => s.Name == "Twitch");
        if (twitchService == null || string.IsNullOrEmpty(twitchService.AccessToken))
            throw new InvalidOperationException("No Twitch service found or missing access token.");

        if (botAccount == null || string.IsNullOrEmpty(botAccount.AccessToken))
            throw new InvalidOperationException("No bot account found or missing access token.");

        User botUser = _twitchApiService.GetOrFetchUser(name: botAccount.Username).Result;

        _userId = twitchService.UserId;
        _userName = twitchService.UserName;
        _accessToken = twitchService.AccessToken;
        _botUserId = botUser.Id;
        _botUserName = botUser.Username;
        _botAccessToken = botAccount.AccessToken;
    }

    private void RefreshClients()
    {
        using IServiceScope scope = _scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        BotAccount? botAccount = dbContext.BotAccounts.FirstOrDefault();
        Service? twitchService = dbContext.Services.FirstOrDefault(s => s.Name == "Twitch");
        if (twitchService == null || string.IsNullOrEmpty(twitchService.AccessToken))
            throw new InvalidOperationException("No Twitch service found or missing access token.");

        if (botAccount == null || string.IsNullOrEmpty(botAccount.AccessToken))
            throw new InvalidOperationException("No bot account found or missing access token.");

        User botUser = _twitchApiService.GetOrFetchUser(name: botAccount.Username).Result;

        _userId = twitchService.UserId;
        _userName = twitchService.UserName;
        _accessToken = twitchService.AccessToken;
        _botUserId = botUser.Id;
        _botUserName = botUser.Username;
        _botAccessToken = botAccount.AccessToken;
    }

    public async Task SendMessageAsUser(string channel, string message)
    {
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
        try
        {
            await _twitchApiService.SendMessage(_userId, message, _userId, _accessToken, replyToMessageId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as user. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _userId, _accessToken, replyToMessageId);
        }
    }

    public async Task SendMessageAsBot(string channel, string message)
    {
        try
        {
            foreach (string text in SplitMessageIntoChunks(message, 450))
            {
                await _twitchApiService.SendMessage(_userId, text, _botUserId, _botAccessToken);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send message as bot. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _botUserId, _botAccessToken);
        }
    }

    public async Task SendReplyAsBot(string channel, string message, string replyToMessageId)
    {
        try
        {
            foreach (string text in SplitMessageIntoChunks(message, 450))
            {
                await _twitchApiService.SendMessage(_userId, text, _botUserId, _botAccessToken, replyToMessageId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(_userId, message, _botUserId, _botAccessToken, replyToMessageId);
        }
    }

    public async Task SendOneOffMessage(string channelId, string message)
    {
        try
        {
            await _twitchApiService.SendMessage(channelId, message + " #NMBot", _userId, _accessToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Attempting to refresh clients.");
            RefreshClients();
            await _twitchApiService.SendMessage(channelId, message + " #NMBot", _userId, _accessToken);
        }
    }

    public async Task SendOneOffMessageAsBot(string channel, string message)
    {
        try
        {
            User channelUser = await _twitchApiService.GetOrFetchUser(name: channel);

            await _twitchApiService.SendMessage(channelUser.Id, message + " #NMBot", _botUserId, _botAccessToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send reply as bot. Attempting to refresh clients.");
            RefreshClients();

            User channelUser = await _twitchApiService.GetOrFetchUser(name: channel);

            await _twitchApiService.SendMessage(channelUser.Id, message + " #NMBot", _botUserId, _botAccessToken);
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