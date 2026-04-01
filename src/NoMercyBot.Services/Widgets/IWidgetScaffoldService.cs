using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Widgets;

public interface IWidgetScaffoldService
{
    Task<bool> CreateWidgetScaffoldAsync(
        Ulid widgetId,
        string widgetName,
        string framework,
        Dictionary<string, object> settings
    );

    Task<bool> ValidateFrameworkAsync(string framework);
    List<string> GetSupportedFrameworks();
    Task SaveConfigurationFileAsync(Widget widget);
}
