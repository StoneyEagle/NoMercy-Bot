using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(ChannelId), nameof(UserId), IsUnique = true)]
public class ChatPresence : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MaxLength(50)]
    public int Id { get; set; }

    public bool IsPresent { get; set; }

    [MaxLength(50)]
    public string ChannelId { get; set; } = null!;
    public virtual User Channel { get; set; } = null!;

    [MaxLength(50)]
    public string UserId { get; set; } = null!;
    public User User { get; set; } = null!;
}
