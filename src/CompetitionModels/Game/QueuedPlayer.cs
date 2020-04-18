using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class QueuedPlayer
    {
        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }

        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    }
}
