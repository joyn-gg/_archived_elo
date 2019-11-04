using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class ManualGameScoreUpdate
    {
        [ForeignKey("GuildId")]
        public virtual Competition Comp { get; set; }
        public ulong GuildId { get; set; }

        public ulong UserId { get; set; }

        public int ManualGameId { get; set; }

        public int ModifyAmount { get; set; }

        public virtual ManualGameResult Game { get; set; }
    }
}
