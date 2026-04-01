using System.ComponentModel.DataAnnotations;

namespace NoMercyBot.Globals.Dto;

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
