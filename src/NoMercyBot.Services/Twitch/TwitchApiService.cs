// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeMadeStatic.Global

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime.TimeZones;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Dto;
using RestSharp;
using TwitchLib.Api.Helix.Models.Chat.GetUserChatColor;

namespace NoMercyBot.Services.Twitch;

public class TwitchApiService
{
    private readonly IConfiguration _conf;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TwitchApiService> _logger;
    private readonly PronounService _pronounService;

    public Service Service => TwitchConfig.Service();

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("Twitch ClientId is not set.");

    public TwitchApiService(IServiceScopeFactory serviceScopeFactory, IConfiguration conf,
        ILogger<TwitchApiService> logger, PronounService pronounService)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _pronounService = pronounService;
    }

    public async Task<List<UserInfo>?> GetUsers(string[]? userIds = null, string? userId = null, string? login = null)
    {
        if (userIds is not null && userIds.Length == 0) throw new("userIds must contain at least 1 userId");
        if (userIds is not null && userIds.Length > 100) throw new("Too many user ids provided.");

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("users");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        foreach (string id in userIds ?? []) request.AddQueryParameter("id", id);

        if (userId != null) request.AddQueryParameter("id", userId);
        if (login != null) request.AddQueryParameter("login", login);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch user information.");

        UserInfoResponse? userInfoResponse = response.Content?.FromJson<UserInfoResponse>();
        if (userInfoResponse?.Data is null) throw new("Failed to parse user information.");

        return userInfoResponse.Data;
    }

    public async Task<User> FetchUser(string? countryCode = null, string? id = null, string? login = null)
    {
        List<UserInfo>? users = await GetUsers(userId: id, login: login);
        if (users is null || users.Count == 0) throw new("Failed to fetch user information.");

        UserInfo userInfo = users.First();

        IEnumerable<string>? zoneIds = TzdbDateTimeZoneSource.Default.ZoneLocations?
            .Where(x => x.CountryCode == countryCode)
            .Select(x => x.ZoneId)
            .ToList();

        GetUserChatColorResponse? colors = await GetUserChatColors([userInfo.Id]);
        Pronoun? pronoun = await _pronounService.GetUserPronoun(userInfo.Login);

        User user = new()
        {
            Id = userInfo.Id,
            Username = userInfo.Login,
            DisplayName = userInfo.DisplayName,
            Description = userInfo.Description,
            ProfileImageUrl = userInfo.ProfileImageUrl,
            OfflineImageUrl = userInfo.OfflineImageUrl,
            BroadcasterType = userInfo.BroadcasterType,
            Timezone = zoneIds?.FirstOrDefault(),
            Pronoun = pronoun
        };

        string? color = colors?.Data.First().Color;

        user.Color = string.IsNullOrEmpty(color)
            ? "#9146FF"
            : color;

        AppDbContext dbContext = new();
        await dbContext.Users.Upsert(user)
            .On(u => u.Id)
            .WhenMatched((oldUser, newUser) => new()
            {
                Username = newUser.Username,
                DisplayName = newUser.DisplayName,
                ProfileImageUrl = newUser.ProfileImageUrl,
                OfflineImageUrl = newUser.OfflineImageUrl,
                Color = newUser.Color,
                BroadcasterType = newUser.BroadcasterType,
                UpdatedAt = DateTime.UtcNow
            })
            .RunAsync();

        ChannelInfo? channelInfo = await GetChannelInfo(userInfo.Id);
        if (channelInfo is not null)
            await dbContext.ChannelInfo.Upsert(channelInfo)
                .On(c => c.Id)
                .WhenMatched((oldChannel, newChannel) => new()
                {
                    Language = newChannel.Language,
                    GameId = newChannel.GameId,
                    GameName = newChannel.GameName,
                    Title = newChannel.Title,
                    Delay = newChannel.Delay,
                    Tags = newChannel.Tags,
                    ContentLabels = newChannel.ContentLabels,
                    IsBrandedContent = newChannel.IsBrandedContent,
                    UpdatedAt = DateTime.UtcNow
                })
                .RunAsync();

        Channel channel = new()
        {
            Id = user.Id,
            Name = user.Username
        };

        await dbContext.Channels.Upsert(channel)
            .On(c => c.Id)
            .WhenMatched((oldChannel, newChannel) => new()
            {
                Name = newChannel.Name,
                UpdatedAt = DateTime.UtcNow
            })
            .RunAsync();

        return user;
    }

    public async Task<GetUserChatColorResponse?> GetUserChatColors(string[] userIds)
    {
        if (userIds.Any(string.IsNullOrEmpty)) throw new("Invalid user id provided.");
        if (userIds.Length == 0) throw new("userIds must contain at least 1 userId");
        if (userIds.Length > 100) throw new("Too many user ids provided.");

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new($"chat/color");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        foreach (string id in userIds) request.AddQueryParameter("user_id", id);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch user color.");

        GetUserChatColorResponse? colors = response.Content?.FromJson<GetUserChatColorResponse>();
        if (colors is null) throw new("Failed to parse user chat color.");

        return colors;
    }

    public async Task<ChannelResponse> GetUserModeration(string userId)
    {
        if (string.IsNullOrEmpty(userId)) throw new("No user id provided.");

        RestClient client = new(TwitchConfig.ApiUrl);

        RestRequest request = new("moderation/channels");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("client-id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddParameter("user_id", userId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch user information.");

        ChannelResponse? channelResponse = response.Content.FromJson<ChannelResponse>();
        if (channelResponse == null) throw new("Invalid response from Twitch.");

        return channelResponse;
    }

    public async Task<ChannelInfo?> GetChannelInfo(string broadcasterId)
    {
        RestClient client = new(TwitchConfig.ApiUrl);

        RestRequest request = new($"channels");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch channel information.");

        ChannelInfoResponse? channelInfoResponse = response.Content.FromJson<ChannelInfoResponse>();
        if (channelInfoResponse == null || channelInfoResponse.Data.Count == 0)
            throw new("Invalid response from Twitch or no channel information found.");

        ChannelInfoDto? dto = channelInfoResponse?.Data.FirstOrDefault();
        if (dto == null) return null;

        return new()
        {
            Id = dto.BroadcasterId,
            Language = dto.Language,
            GameId = dto.GameId,
            GameName = dto.GameName,
            Title = dto.Title,
            Delay = dto.Delay,
            Tags = dto.Tags,
            ContentLabels = dto.ContentLabels,
            IsBrandedContent = dto.IsBrandedContent
        };
    }

    public async Task<StreamInfo?> GetStreamInfo(string? broadcasterId = null, string? broadcasterLogin = null)
    {
        if (string.IsNullOrEmpty(broadcasterId) && string.IsNullOrEmpty(broadcasterLogin))
            throw new("Either broadcasterId or broadcasterLogin must be provided.");

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("streams");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(broadcasterId))
            request.AddQueryParameter("user_id", broadcasterId);

        if (!string.IsNullOrEmpty(broadcasterLogin))
            request.AddQueryParameter("user_login", broadcasterLogin);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch stream information.");

        StreamInfoResponse? streamInfoResponse = response.Content.FromJson<StreamInfoResponse>();
        if (streamInfoResponse?.Data is null || streamInfoResponse.Data.Count == 0)
            return null;

        return streamInfoResponse.Data.First();
    }

    public async Task<string?> CreateEventSubSubscription(string eventType, string version,
        Dictionary<string, string> conditions, string callbackUrl, string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new("No access token provided.");

        try
        {
            RestClient client = new(TwitchConfig.ApiUrl);
            RestRequest request = new("eventsub/subscriptions", Method.Post);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
            request.AddHeader("Content-Type", "application/json");

            var subscription = new
            {
                type = eventType,
                version = version,
                condition = conditions,
                transport = new
                {
                    method = "webhook",
                    callback = callbackUrl,
                    secret = EventSubSecretStore.Secret
                }
            };

            _logger.LogInformation(
                "Creating EventSub subscription: Type={EventType}, Version={Version}, Callback={Callback}, Conditions={@Conditions}",
                eventType, version, callbackUrl, conditions);

            request.AddJsonBody(subscription);

            RestResponse response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content is null)
            {
                _logger.LogError("Failed to create EventSub subscription: Status={StatusCode}, Content={Content}",
                    (int)response.StatusCode, response.Content);
                return null;
            }

            _logger.LogInformation("EventSub subscription response: {Content}", response.Content);

            // Parse the response to get the subscription ID
            dynamic? responseObject = System.Text.Json.JsonSerializer.Deserialize<dynamic>(response.Content);
            string? subscriptionId = responseObject?.data?[0]?.id?.ToString();

            if (subscriptionId != null)
                _logger.LogInformation("Successfully created EventSub subscription: ID={SubscriptionId}",
                    subscriptionId);
            else
                _logger.LogWarning("Created EventSub subscription but couldn't extract ID from response");

            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EventSub subscription");
            return null;
        }
    }

    public async Task DeleteEventSubSubscription(string subscriptionId, string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new("No access token provided.");

        try
        {
            RestClient client = new(TwitchConfig.ApiUrl);
            RestRequest request = new($"eventsub/subscriptions?id={subscriptionId}", Method.Delete);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
            request.AddHeader("Content-Type", "application/json");

            RestResponse response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful) _logger.LogError($"Failed to delete EventSub subscription: {response.Content}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting EventSub subscription {subscriptionId}");
        }
    }

    public async Task DeleteAllEventSubSubscriptions(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new("No access token provided.");

        try
        {
            RestClient client = new(TwitchConfig.ApiUrl);
            RestRequest request = new("eventsub/subscriptions");
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
            request.AddHeader("Content-Type", "application/json");

            RestResponse response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content is null)
            {
                _logger.LogError($"Failed to fetch EventSub subscriptions: {response.Content}");
                return;
            }

            // Parse the response to get all subscription IDs
            dynamic? responseObject = System.Text.Json.JsonSerializer.Deserialize<dynamic>(response.Content);
            dynamic? subscriptions = responseObject?.data?.EnumerateArray();

            if (subscriptions != null)
                foreach (dynamic? subscription in subscriptions)
                {
                    string? id = subscription.GetProperty("id").ToString();
                    if (!string.IsNullOrEmpty(id)) await DeleteEventSubSubscription(id, accessToken);
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all EventSub subscriptions");
        }
    }

    public async Task<ChannelFollowersResponseData?> GetChannelFollower(string broadcasterId, string userId)
    {
        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new($"channels/followers");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);
        request.AddQueryParameter("user_id", userId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch channel followers.");

        ChannelFollowersResponse? followerResponse = response.Content.FromJson<ChannelFollowersResponse>();
        if (followerResponse?.Data is null) throw new("Failed to parse channel followers.");

        return followerResponse.Data.FirstOrDefault();
    }

    public async Task SendShoutoutAsync(string broadcasterId, string moderatorId, string userId)
    {
        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("chat/shoutouts", Method.Post);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("from_broadcaster_id", broadcasterId);
        request.AddQueryParameter("to_broadcaster_id", userId);
        request.AddQueryParameter("moderator_id", moderatorId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to send shoutout.");
    }

    public async Task SendAnnouncement(string broadcasterId, string moderatorId, string message,
        string? color = "primary")
    {
        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("chat/announcements", Method.Post);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);
        request.AddQueryParameter("moderator_id", moderatorId);
        request.AddBody(new
        {
            message = message,
            color = color
        });

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to send announcement.");
    }

    public async Task<ChannelPointsCustomRewardsResponseData?> CreateCustomReward(string broadcasterId, string title,
        int cost, string? prompt = null, bool isUserInputRequired = false, bool isEnabled = true,
        string? backgroundColor = null, bool isPaused = false, bool shouldRedemptionsSkipRequestQueue = false,
        int? maxPerStream = null, int? maxPerUserPerStream = null, int? globalCooldownSeconds = null)
    {
        if (string.IsNullOrEmpty(broadcasterId)) throw new ArgumentException("Broadcaster ID is required");
        if (string.IsNullOrEmpty(title)) throw new ArgumentException("Title is required");
        if (cost < 1) throw new ArgumentException("Cost must be at least 1");

        _logger.LogInformation("Creating custom reward: {Title} for broadcaster {BroadcasterId}", title, broadcasterId);

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("channel_points/custom_rewards", Method.Post);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);

        object body = new
        {
            title = title,
            cost = cost,
            prompt = prompt ?? "",
            is_user_input_required = isUserInputRequired,
            is_enabled = isEnabled,
            background_color = backgroundColor,
            is_paused = isPaused,
            should_redemptions_skip_request_queue = shouldRedemptionsSkipRequestQueue,
            max_per_stream_setting = maxPerStream.HasValue
                ? new { is_enabled = true, max_per_stream = maxPerStream.Value }
                : new { is_enabled = false, max_per_stream = 0 },
            max_per_user_per_stream_setting = maxPerUserPerStream.HasValue
                ? new { is_enabled = true, max_per_user_per_stream = maxPerUserPerStream.Value }
                : new { is_enabled = false, max_per_user_per_stream = 0 },
            global_cooldown_setting = globalCooldownSeconds.HasValue
                ? new { is_enabled = true, global_cooldown_seconds = globalCooldownSeconds.Value }
                : new { is_enabled = false, global_cooldown_seconds = 0 }
        };

        request.AddJsonBody(body);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to create custom reward. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, response.Content);
            throw new($"Failed to create custom reward: {response.Content}");
        }

        _logger.LogInformation("Successfully created custom reward: {Title}", title);

        ChannelPointsCustomRewardsResponse? rewardResponse =
            response.Content?.FromJson<ChannelPointsCustomRewardsResponse>();
        return rewardResponse?.Data?.FirstOrDefault();
    }

    public async Task UpdateRedemptionStatus(string broadcasterId, string rewardId, string redemptionId, string status)
    {
        if (string.IsNullOrEmpty(broadcasterId)) throw new ArgumentException("Broadcaster ID is required");
        if (string.IsNullOrEmpty(rewardId)) throw new ArgumentException("Reward ID is required");
        if (string.IsNullOrEmpty(redemptionId)) throw new ArgumentException("Redemption ID is required");
        if (string.IsNullOrEmpty(status)) throw new ArgumentException("Status is required");

        if (status != "FULFILLED" && status != "CANCELED")
            throw new ArgumentException("Status must be either 'FULFILLED' or 'CANCELED'");

        // Log the request details for debugging
        _logger.LogInformation(
            "Updating redemption status - Broadcaster: {BroadcasterId}, Reward: {RewardId}, Redemption: {RedemptionId}, Status: {Status}",
            broadcasterId, rewardId, redemptionId, status);
        _logger.LogInformation("Using Client-Id: {ClientId}", TwitchConfig.Service().ClientId);

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("channel_points/custom_rewards/redemptions", Method.Patch);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);
        request.AddQueryParameter("reward_id", rewardId);
        request.AddQueryParameter("id", redemptionId);

        object body = new { status };
        request.AddJsonBody(body);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to update redemption status. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, response.Content);
            _logger.LogError("Request URL: {Url}", client.BuildUri(request));
            _logger.LogError("Request Headers: Authorization: Bearer [REDACTED], Client-Id: {ClientId}",
                TwitchConfig.Service().ClientId);

            // Don't throw exception immediately, log more details first
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                _logger.LogError("403 Forbidden suggests either insufficient scopes or Client-Id mismatch. " +
                                 "Ensure the access token has 'channel:manage:redemptions' scope and was generated with the same Client-Id used to create this reward.");

            throw new($"Failed to update redemption status: {response.Content}");
        }

        _logger.LogInformation("Successfully updated redemption {RedemptionId} to status {Status}", redemptionId,
            status);
    }

    public async Task<ChannelPointsCustomRewardsResponse?> GetCustomRewards(string broadcasterId, Guid? rewardId = null)
    {
        if (string.IsNullOrEmpty(broadcasterId)) throw new ArgumentException("Broadcaster ID is required");

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("channel_points/custom_rewards");
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);

        if (!Guid.Empty.Equals(rewardId))
            request.AddQueryParameter("id", rewardId.ToString());

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch custom rewards.");

        ChannelPointsCustomRewardsResponse? rewardsResponse =
            response.Content.FromJson<ChannelPointsCustomRewardsResponse>();
        if (rewardsResponse?.Data is null) throw new("Failed to parse custom rewards.");
        
        return rewardsResponse;
    }
    
    public async Task<ChannelPointsCustomRewardsResponseData?> UpdateCustomReward(string broadcasterId, Guid rewardId,
        string? title = null, int? cost = null, string? prompt = null, bool? isUserInputRequired = null,
        bool? isEnabled = null, string? backgroundColor = null, bool? isPaused = null,
        bool? shouldRedemptionsSkipRequestQueue = null, int? maxPerStream = null, int? maxPerUserPerStream = null,
        int? globalCooldownSeconds = null)
    {
        if (string.IsNullOrEmpty(broadcasterId)) throw new ArgumentException("Broadcaster ID is required");
        if (Guid.Empty.Equals(rewardId)) throw new ArgumentException("Reward ID is required");

        _logger.LogInformation("Updating custom reward: {RewardId} for broadcaster {BroadcasterId}", rewardId,
            broadcasterId);

        RestRequest request = new("channel_points/custom_rewards", Method.Patch);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);
        request.AddQueryParameter("id", rewardId);
        
        object body = new
        {
            title = title,
            cost = cost,
            prompt = prompt,
            is_user_input_required = isUserInputRequired,
            is_enabled = isEnabled,
            background_color = backgroundColor,
            is_paused = isPaused,
            should_redemptions_skip_request_queue = shouldRedemptionsSkipRequestQueue,
            max_per_stream_setting = maxPerStream.HasValue
                ? new { is_enabled = true, max_per_stream = maxPerStream.Value }
                : null,
            max_per_user_per_stream_setting = maxPerUserPerStream.HasValue
                ? new { is_enabled = true, max_per_user_per_stream = maxPerUserPerStream.Value }
                : null,
            global_cooldown_setting = globalCooldownSeconds.HasValue
                ? new { is_enabled = true, global_cooldown_seconds = globalCooldownSeconds.Value }
                : null
        };
        
        request.AddJsonBody(body);
        RestClient client = new(TwitchConfig.ApiUrl);
        RestResponse response = await client.ExecuteAsync(request);
        
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to update custom reward. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, response.Content);
            throw new($"Failed to update custom reward: {response.Content}");

        }
        _logger.LogInformation("Successfully updated custom reward: {RewardId}", rewardId);
        
        ChannelPointsCustomRewardsResponse? rewardResponse =
            response.Content?.FromJson<ChannelPointsCustomRewardsResponse>();
        return rewardResponse?.Data?.FirstOrDefault();
    }

    public async Task<User> GetOrFetchUser(string? id = null, string? name = null)
    {
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
            throw new ArgumentException("Either id or login must be provided.");

        User? user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id || u.Username == name);

        // If user doesn't exist, fetch them
        if (user == null)
        {
            return await FetchUser(id: id, login: name);
        }

        // If user exists but username doesn't match, refresh their data
        if (!string.IsNullOrEmpty(name) && user.Username != name)
        {
            _logger.LogInformation(
                "Username mismatch for user {UserId}: stored='{StoredUsername}', current='{CurrentUsername}', refreshing",
                user.Id, user.Username, name);
            return await FetchUser(id: id, login: name);
        }

        return user;
    }

    public async Task<ChannelInfo> GetOrFetchChannelInfo(string id)
    {
        ChannelInfo? channelInfo = await _dbContext.ChannelInfo
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (channelInfo is not null) return channelInfo;

        await GetOrFetchUser(id);

        channelInfo = await _dbContext.ChannelInfo
            .AsNoTracking()
            .FirstAsync(c => c.Id == id);

        return channelInfo;
    }

    public async Task<Channel> GetOrFetchChannel(string? id = null, string? name = null)
    {
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
            throw new ArgumentException("Either id or name must be provided.");

        Channel? channel = await _dbContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id || c.Name == name);

        if (channel is not null) return channel;

        await GetOrFetchUser(id, name);

        channel = await _dbContext.Channels
            .AsNoTracking()
            .FirstAsync(c => c.Id == id || c.Name == name);

        return channel;
    }

    public async Task SendMessage(string broadcasterId, string message, string userId, string accessToken, string? replyId = null)
    {
        if (string.IsNullOrEmpty(broadcasterId)) throw new ArgumentException("Channel cannot be null or empty.");
        if (string.IsNullOrEmpty(message)) throw new ArgumentException("Message cannot be null or empty.");
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("Twitch username cannot be null or empty.");
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentException("Twitch access token cannot be null or empty.");

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("chat/messages", Method.Post);
        request.AddHeader("Authorization", $"Bearer {accessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("broadcaster_id", broadcasterId);
        request.AddQueryParameter("sender_id", userId);
        request.AddQueryParameter("message", message);
        request.AddQueryParameter("reply_parent_message_id", replyId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to send message.");
    }

    public async Task RaidAsync(string fromBroadcasterId, string toBroadcasterId)
    {
        if (string.IsNullOrEmpty(fromBroadcasterId)) throw new ArgumentException("From Broadcaster ID is required");
        if (string.IsNullOrEmpty(toBroadcasterId)) throw new ArgumentException("To Broadcaster ID is required");

        _logger.LogInformation("Raiding from {FromBroadcasterId} to {ToBroadcasterId}", fromBroadcasterId, toBroadcasterId);

        RestClient client = new(TwitchConfig.ApiUrl);
        RestRequest request = new("raids", Method.Post);
        request.AddHeader("Authorization", $"Bearer {TwitchConfig.Service().AccessToken}");
        request.AddHeader("Client-Id", TwitchConfig.Service().ClientId!);
        request.AddHeader("Content-Type", "application/json");

        request.AddQueryParameter("from_broadcaster_id", fromBroadcasterId);
        request.AddQueryParameter("to_broadcaster_id", toBroadcasterId);

        RestResponse response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
        {
            _logger.LogError("Failed to start raid. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, response.Content);
            throw new($"Failed to start raid: {response.Content}");
        }

        _logger.LogInformation("Successfully started raid to {ToBroadcasterId}", toBroadcasterId);
    }
}