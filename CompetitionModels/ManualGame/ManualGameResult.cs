using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class ManualGameResult
    {
        public ManualGameResult() { }

        public ManualGameResult(int gameId, ulong guildId)
        {
            GameId = gameId;
            GuildId = guildId;
        }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GameId { get; set; }

        [ForeignKey("GuildId")]
        public Competition Comp { get; set; }
        public ulong GuildId { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        public string Comment { get; set; } = null;

        public ulong Submitter { get; set; }

        public ManualGameState GameState { get; set; } = ManualGameState.Legacy;

        public ICollection<ManualGameScoreUpdate> ScoreUpdates { get; set; }
    }
}
