using System;
using System.Collections.Generic;
using System.Linq;
using RavenBOT.ELO.Modules.Modules;

namespace RavenBOT.ELO.Modules.Models
{
    public class Lobby : EloLobby
    {
        public static string DocumentName(ulong guildId, ulong channelId)
        {
            return $"LobbyConfig-{guildId}-{channelId}";
        }

        public Lobby(ulong guildId, ulong channelId)
        {
            this.GuildId = guildId;
            this.ChannelId = channelId;
        }

        public Lobby() {}

        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string Description { get; set; }

        public ulong GameReadyAnnouncementChannel { get; set; }
        public bool MentionUsersInReadyAnnouncement { get; set; } = true;

        public ulong GameResultAnnouncementChannel { get; set; }

        public int? MinimumPoints { get; set; } = null;

        public bool DmUsersOnGameReady { get; set; } = false;
        public bool ReactOnJoinLeave { get; set; } = true;
        public bool HideQueue { get; set; } = false;

        public int PlayersPerTeam { get; set; } = 5;

        public HashSet<ulong> Queue { get; set; } = new HashSet<ulong>();

        public PickMode TeamPickMode { get; set; } = PickMode.Random;

        public int CurrentGameCount { get; set; } = 0;

        public GameResult.CaptainPickOrder CaptainPickOrder { get; set; } = GameResult.CaptainPickOrder.PickTwo;

        //TODO: Specific announcement channel per lobby
        public enum PickMode
        {
            Captains_HighestRanked,
            Captains_RandomHighestRanked,
            Captains_Random,
            Random,
            TryBalance
        }

        /// <summary>
        /// Checks whether the specified pickmode is captains
        /// </summary>
        /// <param name="pickMode"></param>
        /// <returns></returns>
        public static bool IsCaptains(PickMode pickMode)
        {
            if (pickMode == PickMode.Random || pickMode == PickMode.TryBalance)
            {
                return false;
            }

            return true;
        }

        //TODO: Allow for votes on maps, reduce chance of repeate games on the same map.
        //public HashSet<string> Maps { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public MapSelector MapSelector { get; set; } = null;
    }
}