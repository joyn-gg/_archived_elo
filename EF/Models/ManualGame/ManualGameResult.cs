using System;
using System.Collections.Generic;
using System.Text;

namespace ELO.EF.Models
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
        public ulong GuildId { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        public string Comment { get; set; } = null;

        public ulong Submitter { get; set; }

        public ManualGameState GameState { get; set; } = ManualGameState.Legacy;

        public enum ManualGameState
        {
            Win,
            Lose,
            Draw,
            Legacy
        }

        public Dictionary<ulong, int> ScoreUpdates { get; set; } = new Dictionary<ulong, int>();
    }
}
