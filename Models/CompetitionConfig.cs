using System;
using System.Collections.Generic;
using System.Linq;
using RavenBOT.Common;

namespace RavenBOT.ELO.Modules.Models
{
    public class CompetitionConfig
    {
        public static string DocumentName(ulong guildId)
        {
            return $"CompetitionConfig-{guildId}";
        }

        public CompetitionConfig(){}
        public CompetitionConfig(ulong guildId)
        {
            this.GuildId = guildId;
            this.DefaultLossModifier = 5;
        }

        public ulong GuildId { get; set; }
        public ulong AdminRole { get; set; }
        public ulong ModeratorRole { get; set; }

        public List<Rank> Ranks { get; set; } = new List<Rank>();

        //TODO: Automatically generate registration role instead of requiring one to be set?
        public ulong RegisteredRankId { get; set; } = 0;

        public int ManualGameCounter { get; set; } = 0;

        public string RegisterMessageTemplate { get; set; } = "You have registered as `{name}`, all roles/name updates have been applied if applicable.";
        public string FormatRegisterMessage(Player player)
        {
            return RegisterMessageTemplate
                    .Replace("{score}", player.Points.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{name}", player.DisplayName, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{wins}", player.Wins.ToString(), StringComparison.InvariantCultureIgnoreCase)                    
                    .Replace("{losses}", player.Losses.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{draws}", player.Draws.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{games}", player.Games.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .FixLength(1024);
        }

        public string NameFormat { get; set; } = "[{score}] {name}";
        public bool UpdateNames { get; set; } = true;

        public string GetNickname(Player player)
        {
            return NameFormat
                    .Replace("{score}", player.Points.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{name}", player.DisplayName, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{wins}", player.Wins.ToString(), StringComparison.InvariantCultureIgnoreCase)                    
                    .Replace("{losses}", player.Losses.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{draws}", player.Draws.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("{games}", player.Games.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .FixLength(32);
        }

        public bool BlockMultiQueueing { get; set; } = false;
        public int RegistrationCount { get; set; } = 0;

        public bool AllowNegativeScore { get; set; } = false;
        
        public bool AllowReRegister { get; set; } = true;
        public bool AllowSelfRename { get; set; } = true;

        //TODO: Consider adding a setter to ensure value is always positive.
        public int DefaultWinModifier { get; set; } = 10;

        private int _DefaultLossModifier;
        
        public int DefaultLossModifier { 
        get
        {
            return _DefaultLossModifier;
        } set
        {
            //Ensure the value that gets set is positive as it will be subtracted from scores.
            _DefaultLossModifier = Math.Abs(value);
            
        } }

        /// <summary>
        /// Returns the highest available rank for the user or null
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="points"></param>
        /// <returns></returns>
        public Rank MaxRank(int points)
        {
            var maxRank = Ranks.Where(x => x.Points <= points).OrderByDescending(x => x.Points).FirstOrDefault();
            if (maxRank == null)
            {
                return null;
            }

            return maxRank;
        }
    }
}