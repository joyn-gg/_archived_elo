using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class ScoreUpdate
    {

        [ForeignKey("GuildId")]
        public virtual Competition Comp { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }


        [ForeignKey("ChannelId")]
        public virtual Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }

        public virtual GameResult Game { get; set; }

        public int GameNumber { get; set; }


        public int ModifyAmount { get; set; }
    }
}
