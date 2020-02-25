using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ELO.Models
{
    public class Competition
    {
        public Competition()
        {
        }

        public Competition(ulong guildId)
        {
            GuildId = guildId;
            DefaultLossModifier = 5;
        }

        [Key]
        public ulong GuildId { get; set; }

        public string Prefix { get; set; } = null;

        public ulong? AdminRole { get; set; }

        public ulong? ModeratorRole { get; set; }

        public TimeSpan? RequeueDelay { get; set; } = null;

        public ulong? RegisteredRankId { get; set; } = null;

        public int ManualGameCounter { get; set; } = 0;

        public bool DisplayErrors { get; set; } = true;

        public string RegisterMessageTemplate { get; set; } = "You have registered as `{name}`, all roles/name updates have been applied if applicable.";

        public string NameFormat { get; set; } = "[{score}] {name}";

        public bool UpdateNames { get; set; } = true;

        public string FormatRegisterMessage(Player player)
        {
            return (RegisterMessageTemplate ?? "You have registered as `{name}`, all roles/name updates have been applied if applicable.")
                    .Replace("{score}", player.Points.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{name}", player.DisplayName, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{wins}", player.Wins.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{losses}", player.Losses.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{draws}", player.Draws.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{games}", player.Games.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .FixLength(1023);
        }

        public string GetNickname(Player player)
        {
            return (NameFormat ?? "[{score}] {name}")
                    .Replace("{score}", player.Points.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{name}", player.DisplayName, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{wins}", player.Wins.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{losses}", player.Losses.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{draws}", player.Draws.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{games}", player.Games.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .FixLength(31);
        }

        public bool AllowMultiQueueing { get; set; } = true;

        public bool AllowNegativeScore { get; set; } = false;

        public bool AllowReRegister { get; set; } = true;

        public bool AllowSelfRename { get; set; } = true;

        public bool AllowVoting { get; set; } = true;

        public int DefaultRegisterScore { get; set; } = 0;

        public TimeSpan? QueueTimeout { get; set; } = null;

        public int DefaultWinModifier
        {
            get
            {
                return _DefaultWinModifier;
            }
            set
            {
                _DefaultWinModifier = Math.Abs(value);
            }
        }

        private int _DefaultWinModifier = 10;

        private int _DefaultLossModifier;

        public int DefaultLossModifier
        {
            get
            {
                return _DefaultLossModifier;
            }
            set
            {
                //Ensure the value that gets set is positive as it will be subtracted from scores.
                _DefaultLossModifier = Math.Abs(value);
            }
        }

        public ulong? PremiumRedeemer { get; set; }

        public DateTime? LegacyPremiumExpiry { get; set; }

        public DateTime? PremiumBuffer { get; set; } = null;

        public int? BufferedPremiumCount { get; set; } = null;

        public ulong? ReactiveMessage { get; set; }

        public virtual ICollection<Lobby> Lobbies { get; set; }

        public virtual ICollection<Player> Players { get; set; }

        public virtual ICollection<Rank> Ranks { get; set; }

        public virtual ICollection<ManualGameResult> ManualGames { get; set; }
    }
}