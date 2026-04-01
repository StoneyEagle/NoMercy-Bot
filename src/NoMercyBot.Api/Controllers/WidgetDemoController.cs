using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Route("api/widgets/demo")]
public class WidgetDemoController : ControllerBase
{
    private readonly IWidgetEventService _widgetEventService;
    private readonly ILogger<WidgetDemoController> _logger;

    public WidgetDemoController(
        IWidgetEventService widgetEventService,
        ILogger<WidgetDemoController> logger
    )
    {
        _widgetEventService = widgetEventService;
        _logger = logger;
    }

    [HttpPost("events/test/{eventType}")]
    public async Task<IActionResult> SendTestEvent(
        string eventType,
        [FromBody] TestEventRequest? request = null
    )
    {
        object eventData;
        string logMessage;

        switch (eventType.ToLowerInvariant())
        {
            case "message":
            case "message.create":
                eventData = new
                {
                    Username = request?.Username ?? "TestUser",
                    Message = request?.Message ?? "Hello from the test endpoint!",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                    MessageId = Ulid.NewUlid().ToString(),
                };
                await _widgetEventService.PublishEventAsync("message.create", eventData);
                logMessage = $"Test message.create event published";
                break;

            case "delete":
            case "message.delete":
                eventData = new
                {
                    MessageId = request?.MessageId ?? Ulid.NewUlid().ToString(),
                    Username = request?.Username ?? "TestUser",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                    Reason = request?.Reason ?? "Test deletion",
                };
                await _widgetEventService.PublishEventAsync("message.delete", eventData);
                logMessage = $"Test message.delete event published";
                break;

            case "edit":
            case "message.edit":
                eventData = new
                {
                    MessageId = request?.MessageId ?? Ulid.NewUlid().ToString(),
                    Username = request?.Username ?? "TestUser",
                    OldMessage = request?.OldMessage ?? "Original message",
                    NewMessage = request?.NewMessage ?? "Edited message",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                };
                await _widgetEventService.PublishEventAsync("message.edit", eventData);
                logMessage = $"Test message.edit event published";
                break;

            case "follow":
            case "new_follower":
                eventData = new
                {
                    Username = request?.Username ?? "TestFollower",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                };
                await _widgetEventService.PublishEventAsync("new_follower", eventData);
                logMessage = $"Test new_follower event published";
                break;

            case "subscribe":
            case "new_subscriber":
                eventData = new
                {
                    Username = request?.Username ?? "TestSubscriber",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                    Tier = request?.SubscriptionTier ?? "1",
                    Months = request?.Months ?? 1,
                };
                await _widgetEventService.PublishEventAsync("new_subscriber", eventData);
                logMessage = $"Test new_subscriber event published";
                break;

            case "donation":
            case "new_donation":
                eventData = new
                {
                    Username = request?.Username ?? "TestDonor",
                    Amount = request?.Amount ?? 5.00m,
                    Currency = request?.Currency ?? "USD",
                    Message = request?.Message ?? "Keep up the great work!",
                    Timestamp = DateTimeOffset.UtcNow,
                    Platform = "demo",
                };
                await _widgetEventService.PublishEventAsync("new_donation", eventData);
                logMessage = $"Test new_donation event published";
                break;

            default:
                return BadRequest(
                    new
                    {
                        message = $"Unknown event type: {eventType}",
                        supportedTypes = new[]
                        {
                            "message",
                            "message.create",
                            "delete",
                            "message.delete",
                            "edit",
                            "message.edit",
                            "follow",
                            "new_follower",
                            "subscribe",
                            "new_subscriber",
                            "donation",
                            "new_donation",
                        },
                    }
                );
        }

        _logger.LogInformation(logMessage);
        return Ok(
            new
            {
                message = $"Test {eventType} event sent to subscribed widgets",
                eventType = eventType,
                data = eventData,
            }
        );
    }

    // Keep the old endpoints for backward compatibility but mark as obsolete
    [HttpPost("events/test")]
    [Obsolete("Use POST /events/test/message instead")]
    public async Task<IActionResult> SendTestEvent()
    {
        return await SendTestEvent("message");
    }

    [HttpPost("events/chat")]
    [Obsolete("Use POST /events/test/message instead")]
    public async Task<IActionResult> SendChatEvent([FromBody] ChatEventRequest request)
    {
        TestEventRequest testRequest = new()
        {
            Username = request.Username,
            Message = request.Message,
        };
        return await SendTestEvent("message", testRequest);
    }

    [HttpPost("events/chat/delete")]
    public async Task<IActionResult> SendChatDeleteEvent([FromBody] DeleteMessageRequest request)
    {
        var deleteData = new
        {
            MessageId = request.MessageId,
            Username = request.Username,
            Timestamp = DateTimeOffset.UtcNow,
            Platform = "demo",
            Reason = request.Reason ?? "User deleted",
        };

        await _widgetEventService.PublishEventAsync("message.delete", deleteData);

        _logger.LogInformation("Message delete event published: {MessageId}", request.MessageId);
        return Ok(new { message = "Message delete event sent", data = deleteData });
    }

    [HttpPost("events/chat/edit")]
    public async Task<IActionResult> SendChatEditEvent([FromBody] EditMessageRequest request)
    {
        var editData = new
        {
            MessageId = request.MessageId,
            Username = request.Username,
            OldMessage = request.OldMessage,
            NewMessage = request.NewMessage,
            Timestamp = DateTimeOffset.UtcNow,
            Platform = "demo",
        };

        await _widgetEventService.PublishEventAsync("message.edit", editData);

        _logger.LogInformation("Message edit event published: {MessageId}", request.MessageId);
        return Ok(new { message = "Message edit event sent", data = editData });
    }

    [HttpPost("reload/{widgetId}")]
    public async Task<IActionResult> ReloadWidget(Ulid widgetId)
    {
        await _widgetEventService.NotifyWidgetReloadAsync(widgetId);

        _logger.LogInformation("Reload notification sent to widget {WidgetId}", widgetId);
        return Ok(new { message = $"Reload notification sent to widget {widgetId}" });
    }
}

public class ChatEventRequest
{
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class FollowEventRequest
{
    public string Username { get; set; } = string.Empty;
}

public class DeleteMessageRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class EditMessageRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string OldMessage { get; set; } = string.Empty;
    public string NewMessage { get; set; } = string.Empty;
}

public class TestEventRequest
{
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string OldMessage { get; set; } = string.Empty;
    public string NewMessage { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
    public int Months { get; set; } = 1;
    public decimal Amount { get; set; } = 0;
    public string Currency { get; set; } = string.Empty;
}
