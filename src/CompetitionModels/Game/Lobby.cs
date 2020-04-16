using ELO.Entities;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class Lobby
    {
        public Lobby(ulong guildId, ulong channelId)
        {
            GuildId = guildId;
            ChannelId = channelId;
        }

        public Lobby() { }

        [ForeignKey("GuildId")]
        public Competition Competition { get; set; }
        public ulong GuildId { get; set; }

        [Key]
        public ulong ChannelId { get; set; }
        public string Description { get; set; } = null;

        public ulong? GameReadyAnnouncementChannel { get; set; } = null;
        public bool MentionUsersInReadyAnnouncement { get; set; } = true;
        public ulong? GameResultAnnouncementChannel { get; set; } = null;


        public int? MinimumPoints { get; set; } = null;
        public double LobbyMultiplier { get; set; } = 1;
        public bool MultiplyLossValue { get; set; } = false;
        public int? HighLimit { get; set; } = null;
        public double ReductionPercent { get; set; } = 0.5;

        public bool DmUsersOnGameReady { get; set; } = false;
        //public bool ReactOnJoinLeave { get; set; } = false;
        public bool HideQueue { get; set; } = false;

        public int PlayersPerTeam { get; set; } = 5;

        public PickMode TeamPickMode { get; set; } = PickMode.Random;

        public int CurrentGameCount { get; set; } = 0;

        public CaptainPickOrder CaptainPickOrder { get; set; } = CaptainPickOrder.PickTwo;

        public HostSelection HostSelectionMode { get; set; } = HostSelection.None;

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

        public virtual ICollection<QueuedPlayer> Queue { get; set; }
        public virtual ICollection<GameResult> GameResults { get; set; }
        public virtual ICollection<Map> Maps { get; set; }
    }
}
