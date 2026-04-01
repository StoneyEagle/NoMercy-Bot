using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Services.Other;

public class GracefulShutdownService : IHostedService
{
    private readonly IHubContext<WidgetHub> _widgetHubContext;
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public GracefulShutdownService(
        IHubContext<WidgetHub> widgetHubContext,
        ILogger<GracefulShutdownService> logger,
        IHostApplicationLifetime hostApplicationLifetime
    )
    {
        _widgetHubContext = widgetHubContext;
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register shutdown notification
        _hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // This service doesn't need cleanup
        return Task.CompletedTask;
    }

    private async void OnApplicationStopping()
    {
        try
        {
            _logger.LogInformation("Application is stopping - notifying all widgets");

            // Notify all connected widgets about server shutdown
            await _widgetHubContext.Clients.All.SendAsync("ServerShutdown");

            // Give widgets a moment to process the notification
            await Task.Delay(1000);

            _logger.LogInformation("Widget shutdown notifications sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during graceful shutdown notification");
        }
    }
}
