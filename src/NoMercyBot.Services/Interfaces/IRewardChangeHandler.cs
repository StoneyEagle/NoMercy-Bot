using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

namespace NoMercyBot.Services.Interfaces;

public enum RewardChangeType
{
    Enabled,
    Disabled,
    PriceChanged,
    TitleChanged,
    DescriptionChanged,
    PauseStatusChanged,
    ResumeStatusChanged,
    CooldownChanged,
    BackgroundColorChanged
}

public interface IRewardChangeHandler
{
    Guid RewardId { get; }
    string RewardTitle { get; }
    RewardPermission Permission { get; }
    
    Task Init(RewardChangeContext context);
    
    Task OnEnabled(RewardChangeContext context);
    Task OnDisabled(RewardChangeContext context);
    Task OnPriceChanged(RewardChangeContext context);
    Task OnTitleChanged(RewardChangeContext context);
    Task OnDescriptionChanged(RewardChangeContext context);
    Task OnPauseStatusChanged(RewardChangeContext context);
    Task OnResumeStatusChanged(RewardChangeContext context);
    Task OnCooldownChanged(RewardChangeContext context);
    Task OnBackgroundColorChanged(RewardChangeContext context);
}

