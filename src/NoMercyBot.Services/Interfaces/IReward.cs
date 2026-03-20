using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

namespace NoMercyBot.Services.Interfaces;

public interface IReward
{
    Guid RewardId { get; }
    string RewardTitle { get; }
    RewardPermission Permission { get; }

    // Set to true to auto-create this reward on Twitch if it doesn't exist
    bool AutoCreate => false;

    // Twitch channel points reward properties used during auto-creation
    int Cost => 1000;
    string? Prompt => null;
    bool IsUserInputRequired => false;
    string? BackgroundColor => null;

    Task Init(RewardScriptContext context);
    Task Callback(RewardScriptContext context);
}