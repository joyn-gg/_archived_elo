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

        public int GameId { get; set; }

        [ForeignKey("GuildId")]
        public virtual Competition Comp { get; set; }
        public ulong GuildId { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        public string Comment { get; set; } = null;

        public ulong Submitter { get; set; }

        public ManualGameState GameState { get; set; }

        public virtual ICollection<ManualGameScoreUpdate> ScoreUpdates { get; set; }

    }
}
