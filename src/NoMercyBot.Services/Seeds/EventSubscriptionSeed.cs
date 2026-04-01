using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Seeds;

public static class EventSubscriptionSeed
{
    public static async Task Init(this AppDbContext dbContext)
    {
        if (await dbContext.EventSubscriptions.AnyAsync())
            return;

        List<EventSubscription> subscriptions = [];

        // Add Twitch events
        AddTwitchEvents(subscriptions);

        // Add Discord events
        AddDiscordEvents(subscriptions);

        // Add OBS events
        AddObsEvents(subscriptions);

        // Add all subscriptions to database
        await dbContext
            .EventSubscriptions.UpsertRange(subscriptions)
            .On(s => new
            {
                s.Provider,
                s.EventType,
                s.Condition,
            })
            .WhenMatched(
                (db, src) =>
                    new()
                    {
                        Provider = db.Provider,
                        EventType = db.EventType,
                        Description = src.Description,
                        Version = src.Version,
                        Metadata = db.Metadata,
                        Condition = src.Condition,
                        UpdatedAt = DateTime.UtcNow,
                    }
            )
            .RunAsync();
    }

    private static void AddTwitchEvents(List<EventSubscription> subscriptions)
    {
        foreach (
            KeyValuePair<
                string,
                (string Description, string Version, string[][] Conditions)
            > eventItem in TwitchEventSubService.AvailableEventTypes
        )
        foreach (string[] valueCondition in eventItem.Value.Conditions)
        {
            subscriptions.Add(
                new("twitch", eventItem.Key, false, eventItem.Value.Version)
                {
                    Description = eventItem.Value.Description,
                    Condition = valueCondition,
                }
            );
        }
    }

    private static void AddDiscordEvents(List<EventSubscription> subscriptions)
    {
        foreach (
            KeyValuePair<string, string> eventItem in DiscordEventSubService.AvailableEventTypes
        )
            subscriptions.Add(
                new("discord", eventItem.Key, false) { Description = eventItem.Value }
            );
    }

    private static void AddObsEvents(List<EventSubscription> subscriptions)
    {
        foreach (KeyValuePair<string, string> eventItem in ObsEventSubService.AvailableEventTypes)
            subscriptions.Add(new("obs", eventItem.Key, false) { Description = eventItem.Value });
    }
}
