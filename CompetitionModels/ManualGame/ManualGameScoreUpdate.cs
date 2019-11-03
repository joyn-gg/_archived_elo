using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class ManualGameScoreUpdate
    {
        [ForeignKey("GuildId")]
        public Competition Comp { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        public int ManualGameId { get; set; }

        public int ModifyAmount { get; set; }

        public ManualGameResult Game { get; set; }
    }
}
