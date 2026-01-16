using NoMercyBot.Database;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Widgets;

public class WidgetScriptContext
{
    public required AppDbContext DatabaseContext { get; init; }
    public required IServiceProvider ServiceProvider { get; init; }
    public required IWidgetEventService WidgetEventService { get; init; }
    public required TwitchApiService TwitchApiService { get; init; }
    public required TwitchChatService TwitchChatService { get; init; }
}
