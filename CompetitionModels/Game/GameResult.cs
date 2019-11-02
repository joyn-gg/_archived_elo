using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RavenBOT.Common;
using Discord;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELO.Models
{
    public class GameResult
    {
        public GameResult() { }

        /*
        public GameResult(int gameId, ulong lobbyId, ulong guildId, Lobby.PickMode lobbyPickMode)
        {
            GameId = gameId;
            LobbyId = lobbyId;
            GuildId = guildId;
            GamePickMode = lobbyPickMode;
        }
        */

        //NOTE : Needs to be set based on auto-incrementing game ID for the entire bot.
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GameId { get; set; }


        [ForeignKey("LobbyId")]
        public Lobby Lobby { get; set; }        
        public ulong LobbyId { get; set; }



        [ForeignKey("GuildId")]
        public Competition Competition { get; set; }
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
    }
}
