using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name), IsUnique = true)]
public class Command : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("permission")]
    public string Permission { get; set; } = "everyone";

    [JsonProperty("type")]
    public string Type { get; set; } = "command";

    [JsonProperty("response")]
    public string Response { get; set; } = null!;

    [JsonProperty("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonProperty("description")]
    public string? Description { get; set; }
}
