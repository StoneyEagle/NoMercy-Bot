using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Api.Dto;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Widgets")]
[Route("api/widgets")]
public class WidgetController : BaseController
{
    private readonly AppDbContext _dbContext;
    private readonly IWidgetScaffoldService _scaffoldService;
    private readonly IWidgetEventService _widgetEventService;

    public WidgetController(
        AppDbContext dbContext,
        IWidgetEventService widgetEventService,
        IWidgetScaffoldService scaffoldService
    )
    {
        _dbContext = dbContext;
        _scaffoldService = scaffoldService;
        _widgetEventService = widgetEventService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWidgets()
    {
        List<Widget> widgets = await _dbContext.Widgets.OrderBy(w => w.Name).ToListAsync();

        List<WidgetDto> widgetDtos = widgets
            .Select(w => new WidgetDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                Version = w.Version,
                Framework = w.Framework,
                IsEnabled = w.IsEnabled,
                EventSubscriptions = w.EventSubscriptions,
                Settings = w.Settings,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
            })
            .ToList();
        return Ok(widgetDtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWidget(Ulid id)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        return Ok(
            new WidgetDto
            {
                Id = widget.Id,
                Name = widget.Name,
                Description = widget.Description,
                Version = widget.Version,
                Framework = widget.Framework,
                IsEnabled = widget.IsEnabled,
                EventSubscriptions = widget.EventSubscriptions,
                Settings = widget.Settings,
                CreatedAt = widget.CreatedAt,
                UpdatedAt = widget.UpdatedAt,
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> CreateWidget([FromBody] CreateWidgetRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate framework
        if (!await _scaffoldService.ValidateFrameworkAsync(request.Framework))
            return BadRequest(
                new
                {
                    message = "Unsupported framework",
                    framework = request.Framework,
                    supportedFrameworks = _scaffoldService.GetSupportedFrameworks(),
                }
            );

        Ulid widgetId = Ulid.NewUlid();

        Widget widget = new()
        {
            Id = widgetId,
            Name = request.Name,
            Description = request.Description,
            Version = request.Version,
            Framework = request.Framework,
            IsEnabled = request.IsEnabled,
            EventSubscriptions = request.EventSubscriptions,
            Settings = request.Settings,
        };

        try
        {
            // Create widget file system structure
            WidgetFiles.EnsureWidgetDirectoryExists(widgetId);

            // Use framework-specific scaffolding instead of basic HTML template
            bool scaffoldSuccess = await _scaffoldService.CreateWidgetScaffoldAsync(
                widgetId,
                request.Name,
                request.Framework,
                request.Settings
            );

            if (!scaffoldSuccess)
                throw new InvalidOperationException(
                    $"Failed to create {request.Framework} scaffold for widget"
                );

            await _scaffoldService.SaveConfigurationFileAsync(widget);

            // Add to database
            _dbContext.Widgets.Add(widget);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetWidget),
                new { id = widgetId },
                new WidgetDto
                {
                    Id = widget.Id,
                    Name = widget.Name,
                    Description = widget.Description,
                    Version = widget.Version,
                    Framework = widget.Framework,
                    IsEnabled = widget.IsEnabled,
                    EventSubscriptions = widget.EventSubscriptions,
                    Settings = widget.Settings,
                    CreatedAt = widget.CreatedAt,
                    UpdatedAt = widget.UpdatedAt,
                }
            );
        }
        catch (Exception ex)
        {
            // Clean up file system if database save failed
            WidgetFiles.DeleteWidgetDirectory(widgetId);
            return Problem(
                title: "Failed to create widget",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWidget(Ulid id, [FromBody] UpdateWidgetRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        // Update only provided properties
        if (request.Name != null)
            widget.Name = request.Name;
        if (request.Description != null)
            widget.Description = request.Description;
        if (request.Version != null)
            widget.Version = request.Version;
        if (request.Framework != null)
            widget.Framework = request.Framework;
        if (request.IsEnabled.HasValue)
            widget.IsEnabled = request.IsEnabled.Value;
        if (request.EventSubscriptions != null)
            widget.EventSubscriptions = request.EventSubscriptions;
        if (request.Settings != null)
            widget.Settings = request.Settings;

        try
        {
            await _dbContext.SaveChangesAsync();

            await _scaffoldService.SaveConfigurationFileAsync(widget);

            await _widgetEventService.NotifyWidgetReloadAsync(widget.Id);
            return Ok(
                new WidgetDto
                {
                    Id = widget.Id,
                    Name = widget.Name,
                    Description = widget.Description,
                    Version = widget.Version,
                    Framework = widget.Framework,
                    IsEnabled = widget.IsEnabled,
                    EventSubscriptions = widget.EventSubscriptions,
                    Settings = widget.Settings,
                    CreatedAt = widget.CreatedAt,
                    UpdatedAt = widget.UpdatedAt,
                }
            );
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to update widget",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWidget(Ulid id)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        try
        {
            // Remove from database
            _dbContext.Widgets.Remove(widget);
            await _dbContext.SaveChangesAsync();

            // Clean up file system
            WidgetFiles.DeleteWidgetDirectory(id);

            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to delete widget",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> ToggleWidget(Ulid id)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        widget.IsEnabled = !widget.IsEnabled;

        try
        {
            await _dbContext.SaveChangesAsync();
            return Ok(
                new WidgetDto
                {
                    Id = widget.Id,
                    Name = widget.Name,
                    Description = widget.Description,
                    Version = widget.Version,
                    Framework = widget.Framework,
                    IsEnabled = widget.IsEnabled,
                    EventSubscriptions = widget.EventSubscriptions,
                    Settings = widget.Settings,
                    CreatedAt = widget.CreatedAt,
                    UpdatedAt = widget.UpdatedAt,
                }
            );
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to toggle widget",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpPost("{id}/events/subscribe")]
    public async Task<IActionResult> SubscribeToEvents(
        Ulid id,
        [FromBody] SubscribeEventsRequest request
    )
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        List<string> currentSubscriptions = widget.EventSubscriptions;
        List<string> newSubscriptions = currentSubscriptions.Union(request.Events).ToList();

        widget.EventSubscriptions = newSubscriptions;

        try
        {
            await _dbContext.SaveChangesAsync();
            return Ok(
                new { message = "Events subscribed successfully", subscriptions = newSubscriptions }
            );
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to subscribe to events",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpPost("{id}/events/unsubscribe")]
    public async Task<IActionResult> UnsubscribeFromEvents(
        Ulid id,
        [FromBody] SubscribeEventsRequest request
    )
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        List<string> currentSubscriptions = widget.EventSubscriptions;
        List<string> newSubscriptions = currentSubscriptions.Except(request.Events).ToList();

        widget.EventSubscriptions = newSubscriptions;

        try
        {
            await _dbContext.SaveChangesAsync();
            return Ok(
                new
                {
                    message = "Events unsubscribed successfully",
                    subscriptions = newSubscriptions,
                }
            );
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to unsubscribe from events",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpGet("{id}/events")]
    public async Task<IActionResult> GetWidgetEvents(Ulid id)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == id);

        if (widget == null)
            return NotFound(new { message = "Widget not found" });

        return Ok(new { events = widget.EventSubscriptions });
    }
}
