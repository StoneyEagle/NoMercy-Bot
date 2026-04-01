using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class Record : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("record_type")]
    public string RecordType { get; set; } = null!;

    [JsonProperty("data")]
    public string Data { get; set; } = null!;

    [MaxLength(50)]
    [JsonProperty("user_id")]
    public string UserId { get; set; } = null!;

    [JsonProperty("user")]
    public User User { get; set; } = null!;
}
