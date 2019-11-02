using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ELO.Models
{
    public class QueuedPlayer
    {
        [ForeignKey("GuildId")]
        public Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        [ForeignKey("ChannelId")]
        public Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }
    }
}
