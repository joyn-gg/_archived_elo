using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class TeamCaptain
    {
        //[ForeignKey("GuildId")]
        //public Competition Comp { get; set; }
        public ulong GuildId { get; set; }

        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }


        public ulong UserId { get; set; }

        public virtual GameResult Game { get; set; }

        [ForeignKey("Game"), Column(Order = 0)]
        public int GameNumber { get; set; }
        public int TeamNumber { get; set; }
    }
}
