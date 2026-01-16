using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Widgets;

/// <summary>
/// Adapter that wraps a loaded IWidgetScript and exposes it as an IWidgetConnectionHandler
/// </summary>
public class WidgetScriptConnectionHandler : IWidgetConnectionHandler
{
    private readonly IWidgetScript _script;
    private readonly WidgetScriptContext _context;
    private readonly ILogger<WidgetScriptConnectionHandler> _logger;

    public IReadOnlyList<string> EventTypes => _script.EventTypes;

    public WidgetScriptConnectionHandler(
        IWidgetScript script,
        WidgetScriptContext context,
        ILogger<WidgetScriptConnectionHandler> logger)
    {
        _script = script;
        _context = context;
        _logger = logger;
    }

    public async Task OnConnectedAsync(Ulid widgetId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _script.OnConnected(_context, widgetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Widget script {ScriptType} failed OnConnected for widget {WidgetId}",
                _script.GetType().Name, widgetId);
        }
    }

    public async Task OnDisconnectedAsync(Ulid widgetId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _script.OnDisconnected(_context, widgetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Widget script {ScriptType} failed OnDisconnected for widget {WidgetId}",
                _script.GetType().Name, widgetId);
        }
    }
}
