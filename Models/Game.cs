using Discord;
using Discord.WebSocket;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenBOT.ELO.Modules.Models
{
    public class GameResult
    {
        public static string DocumentName(int gameId, ulong lobbyId, ulong guildId)
        {
            return $"GameResult-{gameId}-{lobbyId}-{guildId}";
        }

        public GameResult() { }

        public GameResult(int gameId, ulong lobbyId, ulong guildId, Lobby.PickMode lobbyPickMode)
        {
            GameId = gameId;
            LobbyId = lobbyId;
            GuildId = guildId;
            GamePickMode = lobbyPickMode;
        }
        public int GameId { get; set; }

        /// <summary>
        /// Requires at least 50% of the palayers on each team to vote for auto result
        /// </summary>
        /// <typeparam name="ulong"></typeparam>
        /// <typeparam name="Vote"></typeparam>
        /// <returns></returns>
        public Dictionary<ulong, Vote> Votes { get; set; } = new Dictionary<ulong, Vote>();
        public bool VoteComplete { get; set; } = false;
        public bool LegacyGame { get; set; } = true;

        public class Vote
        {
            public ulong UserId { get; set; }
            public VoteState UserVote { get; set; }

            public enum VoteState
            {
                Cancel = 10,
                Cancelled = 10,
                Canceled = 10,
                Win = 20,
                Won = 20,
                Lose = 30,
                Lost = 30,
                Loss = 30,
                Draw = 40,
                Drew = 40
            }
        }

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

        public Team Team1 { get; set; } = new Team();
        public Team Team2 { get; set; } = new Team();
        public HashSet<ulong> Queue { get; set; } = new HashSet<ulong>();

        public int Picks { get; set; } = 0;
        public Team GetTeam()
        {
            return Picks % 2 == 0 ? Team1 : Team2;
        }

        public Team GetOffTeam()
        {
            return Picks % 2 == 0 ? Team2 : Team1;
        }

        public class Team
        {
            public ulong Captain { get; set; } = 0;
            public HashSet<ulong> Players { get; set; } = new HashSet<ulong>();

            public string GetTeamInfo()
            {
                var resStr = "";
                //Only show captain info if a captain has been set.
                if (Captain != 0)
                {
                    resStr += $"Captain: {MentionUtils.MentionUser(Captain)}\n";
                }

                if (Players.Any())
                {
                    resStr += $"Players: {string.Join("\n", Extensions.GetUserMentionList(Players.Where(x => x != Captain)))}";
                }
                else
                {
                    resStr += "Players: N/A";
                }

                return resStr;
            }
        }

        //Indicates user IDs and the amount of points added/removed from them when the game result was decided.
        //public HashSet<(ulong, int)> UpdatedScores { get; set; } = new HashSet<(ulong, int)>();
        public Dictionary<ulong, int> ScoreUpdates { get; set; } = new Dictionary<ulong, int>();

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

        public IEnumerable<ulong> GetQueueRemainingPlayers()
        {
            return Queue.Where(x => Team1.Captain != x && !Team1.Players.Contains(x) && Team2.Captain != x && !Team2.Players.Contains(x));
        }

        public string GetQueueRemainingPlayersString()
        {
            return string.Join("\n", Extensions.GetUserMentionList(GetQueueRemainingPlayers()));
        }

        public string GetQueueMentionList()
        {
            return string.Join("\n", Extensions.GetUserMentionList(Queue));
        }

        public (int, Team) GetWinningTeam()
        {
            if (WinningTeam == 1)
            {
                return (1, Team1);
            }
            else if (WinningTeam == 2)
            {
                return (2, Team2);
            }

            return (-1, null);
        }

        public (int, Team) GetLosingTeam()
        {
            if (WinningTeam == 1)
            {
                return (2, Team2);
            }
            else if (WinningTeam == 2)
            {
                return (1, Team1);
            }

            return (-1, null);
        }
    }
}