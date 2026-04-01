using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Key), IsUnique = true)]
public class Storage : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string SecureValue { get; set; } = string.Empty;
}
