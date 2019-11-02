using ELO.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class ScoreUpdate
    {

        [ForeignKey("GuildId")]
        public Competition Comp { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }


        [ForeignKey("ChannelId")]
        public Lobby Lobby { get; set; }
        public ulong ChannelId { get; set; }

        
        public GameResult Game { get; set; }
        public int GameNumber { get; set; }


        public int ModifyAmount { get; set; }
    }
}
