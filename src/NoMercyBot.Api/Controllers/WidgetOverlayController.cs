using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Route("overlay/widgets")]
public class WidgetOverlayController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WidgetOverlayController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{widgetId}")]
    public async Task<IActionResult> ServeWidget(Ulid widgetId)
    {
        // Verify widget exists and is enabled
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId);

        if (widget == null)
            return NotFound("Widget not found");

        if (!widget.IsEnabled)
            return BadRequest("Widget is disabled");

        // TODO: In future versions, check for managed dev server first
        // For now, serve from dist folder only

        string indexPath = WidgetFiles.GetWidgetIndexFile(widgetId);

        if (!System.IO.File.Exists(indexPath))
            return NotFound("Widget files not found. Please build the widget first.");

        try
        {
            string content = await System.IO.File.ReadAllTextAsync(indexPath);

            // Inject widget settings as global variables before any scripts load
            string widgetSettingsScript =
                $@"
<script>
    window.WIDGET_SETTINGS = {widget.SettingsJson};
    window.WIDGET_ID = '{widgetId}';
    window.WIDGET_NAME = '{widget.Name}';
    window.WIDGET_VERSION = '{widget.Version}';
    window.WIDGET_EVENT_SUBSCRIPTIONS = {System.Text.Json.JsonSerializer.Serialize(widget.EventSubscriptions)};
</script>";

            // Insert settings injection before closing </head> tag (or before first script if no head)
            if (content.Contains("</head>"))
                content = content.Replace("</head>", $"{widgetSettingsScript}</head>");
            else
                // Fallback: inject at the beginning of body
                content = content.Replace("<body>", $"<body>{widgetSettingsScript}");

            // Fix relative asset paths to include widget ID
            content = content.Replace("src=\"./", $"src=\"/overlay/widgets/{widgetId}/");
            content = content.Replace("href=\"./", $"href=\"/overlay/widgets/{widgetId}/");

            return Content(content, "text/html");
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to serve widget",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    [HttpGet("{widgetId}/{*filePath}")]
    public async Task<IActionResult> ServeWidgetAsset(Ulid widgetId, string filePath)
    {
        // Verify widget exists and is enabled
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId);

        if (widget == null)
            return NotFound("Widget not found");

        if (!widget.IsEnabled)
            return BadRequest("Widget is disabled");

        // Security: Prevent directory traversal
        if (filePath.Contains("..") || Path.IsPathRooted(filePath))
            return BadRequest("Invalid file path");

        // TODO: In future versions, check for managed dev server first
        // For now, serve from dist folder only

        string fullPath = Path.Combine(WidgetFiles.GetWidgetDistPath(widgetId), filePath);

        if (!System.IO.File.Exists(fullPath))
            return NotFound("Asset not found");

        try
        {
            // Get MIME type based on file extension
            string contentType = GetContentType(Path.GetExtension(fullPath));

            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Failed to serve asset",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };
    }
}
