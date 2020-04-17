using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class GameResult
    {
        public GameResult() { }

        /*
        public GameResult(int gameId, ulong lobbyId, ulong guildId, PickMode lobbyPickMode)
        {
            GameId = gameId;
            LobbyId = lobbyId;
            GuildId = guildId;
            GamePickMode = lobbyPickMode;
        }
        */

        public int GameId { get; set; }


        [ForeignKey("LobbyId")]
        public virtual Lobby Lobby { get; set; }
        public ulong LobbyId { get; set; }



        [ForeignKey("GuildId")]
        public virtual Competition Competition { get; set; }
        public ulong GuildId { get; set; }



        public DateTime CreationTime { get; set; } = DateTime.UtcNow;

        public GameState GameState { get; set; } = GameState.Undecided;

        public string Comment { get; set; } = null;

        public string MapName { get; set; } = null;


        public ulong Submitter { get; set; }

        public CaptainPickOrder PickOrder { get; set; } = CaptainPickOrder.PickTwo;

        public PickMode GamePickMode { get; set; } = PickMode.Random;

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
        public int Picks { get; set; } = 0;

        public bool VoteComplete { get; set; } = false;

        public virtual ICollection<TeamCaptain> Captains { get; set; }
        public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
        public virtual ICollection<ScoreUpdate> ScoreUpdates { get; set; }
        public virtual ICollection<GameVote> Votes { get; set; }

        /*

        public virtual ICollection<QueuedPlayer> Queue { get; set; }
        public virtual ICollection<TeamPlayer> Team1 { get; set; }
        public virtual ICollection<TeamPlayer> Team2 { get; set; }
        public virtual TeamCaptain TeamCaptain1 { get; set; }
        public virtual TeamCaptain TeamCaptain2 { get; set; }

        public ICollection<TeamPlayer> GetTeam()
        {
            return Picks % 2 == 0 ? Team1 : Team2;
        }

        public ICollection<TeamPlayer> GetOffTeam()
        {
            return Picks % 2 == 0 ? Team2 : Team1;
        }*/
    }
}
