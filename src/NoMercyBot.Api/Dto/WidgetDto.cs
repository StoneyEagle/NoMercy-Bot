using System.ComponentModel.DataAnnotations;

namespace NoMercyBot.Api.Dto;

public class WidgetDto
{
    public Ulid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<string> EventSubscriptions { get; set; } = [];
    public Dictionary<string, object> Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateWidgetRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(20)]
    public string Version { get; set; } = "1.0.0";

    [Required]
    [StringLength(20)]
    public string Framework { get; set; } = string.Empty; // vue, react, svelte, angular, vanilla

    public bool IsEnabled { get; set; } = true;

    public List<string> EventSubscriptions { get; set; } = [];

    public Dictionary<string, object> Settings { get; set; } = new();
}

public class UpdateWidgetRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string? Version { get; set; }

    [StringLength(20)]
    public string? Framework { get; set; }

    public bool? IsEnabled { get; set; }

    public List<string>? EventSubscriptions { get; set; }

    public Dictionary<string, object>? Settings { get; set; }
}

public class SubscribeEventsRequest
{
    [Required]
    public List<string> Events { get; set; } = [];
}
