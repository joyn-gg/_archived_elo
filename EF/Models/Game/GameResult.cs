using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RavenBOT.Common;
using Discord;

namespace ELO.EF.Models
{
    public class GameResult
    {
        public GameResult() { }

        public GameResult(int gameId, ulong lobbyId, ulong guildId, Lobby.PickMode lobbyPickMode)
        {
            GameId = gameId;
            LobbyId = lobbyId;
            GuildId = guildId;
            GamePickMode = lobbyPickMode;
        }
        public int GameId { get; set; }

        public enum TeamSelection
        {
            team1 = 1,
            team2 = 2,
            t1 = 1,
            t2 = 2
        }

        public ulong LobbyId { get; set; }

        public ulong GuildId { get; set; }

        public enum State
        {
            Picking,
            Undecided,
            Draw,
            Decided,
            Canceled
        }

        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        public State GameState { get; set; } = State.Undecided;

        public string Comment { get; set; } = null;

        public string MapName { get; set; } = null;

        public enum CaptainPickOrder
        {
            PickOne,
            PickTwo
        }

        public ulong Submitter { get; set; }

        public CaptainPickOrder PickOrder { get; set; } = CaptainPickOrder.PickTwo;

        public Lobby.PickMode GamePickMode { get; set; } = Lobby.PickMode.Random;

        public int WinningTeam { get; set; } = -1;

        /// <summary>
        /// Returns the channel that this game was created in
        /// Will return null if the channel is unavailable/deleted
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public SocketTextChannel GetChannel(SocketGuild guild)
        {
            if (guild.Id != GuildId)
            {
                throw new ArgumentException("Guild provided must be the same as the guild this game was created in.");
            }
            return guild.GetTextChannel(LobbyId);
        }
    }
}
