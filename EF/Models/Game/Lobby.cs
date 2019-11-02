using System;
using System.Collections.Generic;
using System.Text;

namespace ELO.EF.Models
{
    public class Lobby
    {
        public Lobby(ulong guildId, ulong channelId)
        {
            this.GuildId = guildId;
            this.ChannelId = channelId;
        }

        public Lobby() { }

        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string Description { get; set; } = null;

        public ulong? GameReadyAnnouncementChannel { get; set; } = null;
        public bool MentionUsersInReadyAnnouncement { get; set; } = true;

        public ulong? GameResultAnnouncementChannel { get; set; } = null;

        public int? MinimumPoints { get; set; } = null;

        public bool DmUsersOnGameReady { get; set; } = false;
        public bool ReactOnJoinLeave { get; set; } = false;
        public bool HideQueue { get; set; } = false;

        public int PlayersPerTeam { get; set; } = 5;

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
    }
}
