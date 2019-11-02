using System;
using System.Collections.Generic;
using System.Text;

namespace ELO.EF.Models
{
    public class TeamPlayer
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
        public int GameNumber { get; set; }
        public int TeamNumber { get; set; }
    }
}
