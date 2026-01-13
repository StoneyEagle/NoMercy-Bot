using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardController : BaseController
{
    private readonly ILogger<RewardController> _logger;
    private readonly TwitchApiService _twitchApiService;
    private readonly TwitchRewardService _twitchRewardService;
    private readonly AppDbContext _dbContext;

    public RewardController(
        ILogger<RewardController> logger,
        TwitchApiService twitchApiService,
        TwitchRewardService twitchRewardService,
        AppDbContext dbContext)
    {
        _logger = logger;
        _twitchApiService = twitchApiService;
        _twitchRewardService = twitchRewardService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Get all custom rewards for the broadcaster
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomRewards([FromQuery] Guid? rewardId = null)
    {
        try
        {
            string broadcasterId = _twitchApiService.Service.UserId;
            if (string.IsNullOrEmpty(broadcasterId)) return BadRequestResponse("Broadcaster ID is not configured.");

            ChannelPointsCustomRewardsResponse? rewards =
                await _twitchApiService.GetCustomRewards(broadcasterId, rewardId);
            if (rewards?.Data == null) return NotFoundResponse("No custom rewards found.");

            return Ok(rewards.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch custom rewards");
            return InternalServerErrorResponse("Failed to fetch custom rewards.");
        }
    }

    /// <summary>
    /// Create a new custom reward
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCustomReward([FromBody] CreateRewardRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Title)) return BadRequestResponse("Reward title is required.");

            if (request.Cost < 1) return BadRequestResponse("Reward cost must be at least 1.");

            string broadcasterId = _twitchApiService.Service.UserId;
            if (string.IsNullOrEmpty(broadcasterId)) return BadRequestResponse("Broadcaster ID is not configured.");

            ChannelPointsCustomRewardsResponseData? reward = await _twitchApiService.CreateCustomReward(
                broadcasterId,
                request.Title,
                request.Cost,
                request.Prompt,
                request.IsUserInputRequired,
                request.IsEnabled,
                request.BackgroundColor,
                request.IsPaused,
                request.ShouldRedemptionsSkipRequestQueue,
                request.MaxPerStream,
                request.MaxPerUserPerStream,
                request.GlobalCooldownSeconds
            );

            if (reward == null) return InternalServerErrorResponse("Failed to create custom reward.");

            _logger.LogInformation("Created custom reward: {Title} with ID: {RewardId}", request.Title, reward.Id);
            return CreatedAtAction(nameof(GetCustomRewards), new { rewardId = reward.Id }, reward);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create custom reward: {Title}", request.Title);
            return InternalServerErrorResponse("Failed to create custom reward.");
        }
    }

    /// <summary>
    /// Update redemption status (fulfill or cancel)
    /// </summary>
    [HttpPatch("{rewardId}/redemptions/{redemptionId}")]
    public async Task<IActionResult> UpdateRedemptionStatus(
        string rewardId,
        string redemptionId,
        [FromBody] UpdateRedemptionStatusRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Status) ||
                (request.Status != "FULFILLED" && request.Status != "CANCELED"))
                return BadRequestResponse("Status must be either 'FULFILLED' or 'CANCELED'.");

            string broadcasterId = _twitchApiService.Service.UserId;
            if (string.IsNullOrEmpty(broadcasterId)) return BadRequestResponse("Broadcaster ID is not configured.");

            await _twitchApiService.UpdateRedemptionStatus(broadcasterId, rewardId, redemptionId, request.Status);

            _logger.LogInformation("Updated redemption {RedemptionId} to status {Status}", redemptionId,
                request.Status);
            return Ok(new { message = $"Redemption status updated to {request.Status}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update redemption status for {RedemptionId}", redemptionId);
            return InternalServerErrorResponse("Failed to update redemption status.");
        }
    }

    /// <summary>
    /// Get all registered bot rewards (from database)
    /// </summary>
    [HttpGet("bot-rewards")]
    public async Task<IActionResult> GetBotRewards()
    {
        try
        {
            List<Reward> rewards = await _dbContext.Rewards.ToListAsync();
            return Ok(rewards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch bot rewards");
            return InternalServerErrorResponse("Failed to fetch bot rewards.");
        }
    }

    /// <summary>
    /// Add or update a bot reward
    /// </summary>
    [HttpPost("bot-rewards")]
    public async Task<IActionResult> AddOrUpdateBotReward([FromBody] BotRewardRequest request)
    {
        try
        {
            if (request.RewardId == Guid.Empty) return BadRequestResponse("Reward ID is required.");

            if (string.IsNullOrEmpty(request.Response)) return BadRequestResponse("Response is required.");

            await _twitchRewardService.AddOrUpdateUserRewardAsync(
                request.RewardId,
                request.RewardTitle,
                request.Response,
                request.Permission ?? "everyone",
                request.IsEnabled,
                request.Description
            );

            _logger.LogInformation("Added/updated bot reward: {RewardId}", request.RewardId);
            return Ok(new { message = "Bot reward added/updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add/update bot reward: {RewardId}", request.RewardId);
            return InternalServerErrorResponse("Failed to add/update bot reward.");
        }
    }

    /// <summary>
    /// Remove a bot reward
    /// </summary>
    [HttpDelete("bot-rewards/{identifier}")]
    public async Task<IActionResult> RemoveBotReward(string identifier)
    {
        try
        {
            bool removed = await _twitchRewardService.RemoveUserRewardAsync(identifier);
            if (!removed) return NotFoundResponse("Bot reward not found.");

            _logger.LogInformation("Removed bot reward: {Identifier}", identifier);
            return Ok(new { message = "Bot reward removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove bot reward: {Identifier}", identifier);
            return InternalServerErrorResponse("Failed to remove bot reward.");
        }
    }

    /// <summary>
    /// Get all registered script rewards
    /// </summary>
    [HttpGet("script-rewards")]
    public IActionResult GetScriptRewards()
    {
        try
        {
            IEnumerable<TwitchReward> rewards = _twitchRewardService.ListRewards();

            return Ok(rewards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch script rewards");
            return InternalServerErrorResponse("Failed to fetch script rewards.");
        }
    }
}

public class CreateRewardRequest
{
    public string Title { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string? Prompt { get; set; }
    public bool IsUserInputRequired { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public string? BackgroundColor { get; set; }
    public bool IsPaused { get; set; } = false;
    public bool ShouldRedemptionsSkipRequestQueue { get; set; } = false;
    public int? MaxPerStream { get; set; }
    public int? MaxPerUserPerStream { get; set; }
    public int? GlobalCooldownSeconds { get; set; }
}

public class UpdateRedemptionStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class BotRewardRequest
{
    public Guid RewardId { get; set; }
    public string? RewardTitle { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? Permission { get; set; } = "everyone";
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
}