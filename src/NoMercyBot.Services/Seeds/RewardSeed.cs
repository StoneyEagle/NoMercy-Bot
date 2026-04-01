using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Seeds;

public static class RewardSeed
{
    public static async Task Init(this AppDbContext dbContext, IServiceScope scope)
    {
        try
        {
            TwitchApiService twitchApiService =
                scope.ServiceProvider.GetRequiredService<TwitchApiService>();

            string broadcasterId = TwitchConfig.Service().UserId;

            if (string.IsNullOrEmpty(broadcasterId))
            {
                Logger.Setup(
                    "No broadcaster ID found in Twitch configuration - skipping reward seeding"
                );
                return;
            }

            // Fetch custom rewards from Twitch API
            ChannelPointsCustomRewardsResponse? rewardsResponse =
                await twitchApiService.GetCustomRewards(broadcasterId);

            if (rewardsResponse?.Data == null || !rewardsResponse.Data.Any())
            {
                Logger.Setup("No custom rewards found on Twitch channel - skipping reward seeding");
                return;
            }

            List<Reward> rewards = [];

            foreach (ChannelPointsCustomRewardsResponseData twitchReward in rewardsResponse.Data)
                rewards.Add(
                    new()
                    {
                        Id = twitchReward.Id,
                        Title = twitchReward.Title,
                        Response = $"Thank you for redeeming {twitchReward.Title}! 🎉",
                        Permission = "everyone",
                        IsEnabled = twitchReward.IsEnabled,
                        Description = twitchReward.Prompt,
                    }
                );

            // Add fetched rewards to database
            await dbContext.Rewards.AddRangeAsync(rewards);
            await dbContext.SaveChangesAsync();

            Logger.Setup("Successfully seeded {rewards.Count} custom rewards from Twitch API");
        }
        catch (Exception ex)
        {
            Logger.Setup($"failed to fetch custom rewards from Twitch API during seeding: {ex}");
        }
    }
}
