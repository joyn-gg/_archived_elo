using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public class GameManagement : ReactiveBase
    {
        public GameService GameService { get; }
        public UserService UserService { get; }
        public GameSubmissionService GSS { get; }

        public GameManagement(GameService gameService, UserService userService, GameSubmissionService gSS)
        {
            GameService = gameService;
            UserService = userService;
            GSS = gSS;
        }
        [Command("VoteStates", RunMode = RunMode.Async)]
        [Alias("Results", "VoteTypes")]
        [Summary("Shows possible vote options for the Result command")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task ShowResultsAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", RavenBOT.Common.Extensions.EnumNames<VoteState>()), Color.Blue);
        }


        [Command("Vote", RunMode = RunMode.Sync)]
        [Alias("GameResult", "Result")]
        [Summary("Vote on the specified game's outcome in the specified lobby")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task GameResultAsync(SocketTextChannel lobbyChannel, int gameNumber, string voteState)
        {
            await GameResultAsync(gameNumber, voteState, lobbyChannel);
        }

        [Command("Vote", RunMode = RunMode.Sync)]
        [Alias("GameResult", "Result")]
        [Summary("Vote on the specified game's outcome in the current (or specified) lobby")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task GameResultAsync(int gameNumber, string voteState, SocketTextChannel lobbyChannel = null)
        {
            await GSS.GameResultAsync(Context, gameNumber, voteState, lobbyChannel);
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the specified lobby")]
        [RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task UndoGameAsync(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await UndoGameAsync(gameNumber, lobbyChannel);
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the current (or specified) lobby")]
        [RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task UndoGameAsync(int gameNumber, ISocketMessageChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as ISocketMessageChannel;
            }

            using (var db = new Database())
            {
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.Where(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    await SimpleEmbedAsync($"Game not found. Most recent game is {db.GetLatestGame(lobby)?.GameId}", Color.DarkBlue);
                    return;
                }

                if (game.GameState != GameState.Decided)
                {
                    await SimpleEmbedAsync("Game result is not decided and therefore cannot be undone.", Color.Red);
                    return;
                }

                if (game.GameState == GameState.Draw)
                {
                    await SimpleEmbedAsync("Cannot undo a draw.", Color.Red);
                    return;
                }

                await UndoScoreUpdatesAsync(game, competition, db);
                await SimpleEmbedAsync($"Game #{gameNumber} in {MentionUtils.MentionChannel(lobbyChannel.Id)} Undone.");
            }
        }


        public virtual async Task UndoScoreUpdatesAsync(GameResult game, Competition competition, Database db)
        {
            var scoreUpdates = db.GetScoreUpdates(game.GuildId, game.LobbyId, game.GameId).ToArray();
            var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();
            foreach (var score in scoreUpdates)
            {
                var player = db.Players.Find(game.GuildId, score.UserId);
                if (player == null)
                {
                    //Skip if for whatever reason the player profile cannot be found.
                    continue;
                }

                var currentRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();

                if (score.ModifyAmount < 0)
                {
                    //Points lost, so add them back
                    player.Losses--;
                }
                else
                {
                    //Points gained so remove them
                    player.Wins--;
                }

                //Dont modify for undoing
                player.Points -= score.ModifyAmount;
                if (!competition.AllowNegativeScore && player.Points < 0) player.Points = 0;
                db.Update(player);
                db.Remove(score);

                var guildUser = Context.Guild.GetUser(player.UserId);
                if (guildUser == null)
                {
                    //The user cannot be found in the server so skip updating their name/profile
                    continue;
                }

                await UserService.UpdateUserAsync(competition, player, ranks, guildUser);
            }

            game.GameState = GameState.Undecided;
            db.Update(game);
            db.SaveChanges();
        }


        /*
        // If these commands are added back explain that this does not affect the users who were in the game if it had a result. this is only for removing the game log from the database
        
        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        
        public virtual async Task DelGame(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await DelGame(gameNumber, lobbyChannel);
        }

        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history, DOES NOT UPDATE USERS")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public virtual async Task DelGame(int gameNumber, SocketTextChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    //Reply error not a lobby.
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.FirstOrDefault(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobby.ChannelId && x.GameId == gameNumber);
                if (game == null)
                {
                    await SimpleEmbedAsync("Invalid Game number.", Color.Red);
                    return;
                }
                var info = GameService.GetGameEmbed(game);
                db.GameResults.Remove(game);
                db.SaveChanges();
                await ReplyAsync("Game deleted.", info.Build());
            }
        }*/

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task CancelAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await CancelAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task CancelAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            await GSS.CancelAsync(Context, gameNumber, lobbyChannel, comment);
        }


        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task DrawAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await DrawAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task DrawAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            await GSS.DrawAsync(Context, gameNumber, lobbyChannel, comment);
        }

        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and lobby with an optional comment")]
        [RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task GameAsync(SocketTextChannel lobbyChannel, int gameNumber, TeamSelection winning_team, [Remainder]string comment = null)
        {
            await GameAsync(gameNumber, winning_team, lobbyChannel, comment);
        }

        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and current (or specified) lobby with an optional comment")]
        [RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task GameAsync(int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            await GSS.GameAsync(Context, gameNumber, winning_team, lobbyChannel, comment);
        }
    }
}
