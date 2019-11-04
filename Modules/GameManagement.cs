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
        //TODO: Ensure correct commands require mod/admin perms

        [Command("VoteTypes", RunMode = RunMode.Async)]
        [Alias("Results")]
        [Summary("Shows possible vote options for the Result command")]
        [RequirePermission(PermissionLevel.Registered)]
        public async Task ShowResultsAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", RavenBOT.Common.Extensions.EnumNames<VoteState>()), Color.Blue);
        }


        [Command("Vote", RunMode = RunMode.Sync)]
        [Alias("GameResult", "Result")]
        [Summary("Vote on the specified game's outcome in the specified lobby")]
        [RequirePermission(PermissionLevel.Registered)]
        public async Task GameResultAsync(SocketTextChannel lobbyChannel, int gameNumber, string voteState)
        {
            await GameResultAsync(gameNumber, voteState, lobbyChannel);
        }

        [Command("Vote", RunMode = RunMode.Sync)]
        [Alias("GameResult", "Result")]
        [Summary("Vote on the specified game's outcome in the current (or specified) lobby")]
        [RequirePermission(PermissionLevel.Registered)]
        public async Task GameResultAsync(int gameNumber, string voteState, SocketTextChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            //Do vote conversion to ensure that the state is a string and not an int (to avoid confusion with team number from old elo version)
            if (int.TryParse(voteState, out var voteNumber))
            {
                await SimpleEmbedAsync("Please supply a result relevant to you rather than the team number. Use the `Results` command to see a list of these.", Color.DarkBlue);
                return;
            }

            if (!Enum.TryParse(voteState, true, out VoteState vote))
            {
                await SimpleEmbedAsync("Your vote was invalid. Please choose a result relevant to you. ie. Win (if you won the game) or Lose (if you lost the game)\nYou can view all possible results using the `Results` command.", Color.Red);
                return;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.SingleOrDefault(x => x.LobbyId == lobby.ChannelId && x.GameId == gameNumber);
                if (game == null)
                {
                    await SimpleEmbedAsync("Game not found.", Color.Red);
                    return;
                }

                if (game.GameState != GameState.Undecided)
                {
                    await SimpleEmbedAsync("You can only vote on the result of undecided games.", Color.Red);
                    return;
                }
                else if (game.VoteComplete)
                {
                    //Result is undecided but vote has taken place, therefore it wasn't unanimous
                    await SimpleEmbedAsync("Vote has already been taken on this game but wasn't unanimous, ask an admin to submit the result.", Color.DarkBlue);
                    return;
                }

                var team1 = db.GetTeamFull(game, 1);
                var team2 = db.GetTeamFull(game, 2);

                //TODO: Automatically submit if vote is from an admin.
                if (team1.All(x => x != Context.User.Id) && team2.All(x => x != Context.User.Id))
                {
                    await SimpleEmbedAsync("You are not a player in this game and cannot vote on it's result.", Color.Red);
                    return;
                }

                var votes = db.Votes.Where(x => x.ChannelId == lobby.ChannelId && x.GameId == gameNumber).ToList();
                if (votes.Any(x => x.UserId == Context.User.Id))
                {
                    await SimpleEmbedAsync("You already submitted your vote for this game.", Color.DarkBlue);
                    return;
                }

                var userVote = new GameVote()
                {
                    UserId = Context.User.Id,
                    GameId = gameNumber,
                    ChannelId = lobby.ChannelId,
                    GuildId = Context.Guild.Id,
                    UserVote = vote
                };

                db.Votes.Add(userVote);
                votes.Add(userVote);
                db.SaveChanges();

                //Ensure votes is greater than half the amount of players.
                if (votes.Count * 2 > team1.Count + team2.Count)
                {
                    var drawCount = votes.Count(x => x.UserVote == VoteState.Draw);
                    var cancelCount = votes.Count(x => x.UserVote == VoteState.Cancel);

                    var team1WinCount = votes
                                            //Get players in team 1 and count wins
                                            .Where(x => team1.Contains(x.UserId))
                                            .Count(x => x.UserVote == VoteState.Win)
                                        +
                                        votes
                                            //Get players in team 2 and count losses
                                            .Where(x => team2.Contains(x.UserId))
                                            .Count(x => x.UserVote == VoteState.Lose);

                    var team2WinCount = votes
                                            //Get players in team 2 and count wins
                                            .Where(x => team2.Contains(x.UserId))
                                            .Count(x => x.UserVote == VoteState.Win)
                                        +
                                        votes
                                            //Get players in team 1 and count losses
                                            .Where(x => team1.Contains(x.UserId))
                                            .Count(x => x.UserVote == VoteState.Lose);

                    if (team1WinCount == votes.Count)
                    {
                        //team1 win
                        await GameVoteAsync(db, lobby, game, gameNumber, TeamSelection.team1, team1.ToHashSet(), team2.ToHashSet(), "Decided by vote.");
                    }
                    else if (team2WinCount == votes.Count)
                    {
                        //team2 win
                        await GameVoteAsync(db, lobby, game, gameNumber, TeamSelection.team2, team1.ToHashSet(), team2.ToHashSet(), "Decided by vote.");
                    }
                    else if (drawCount == votes.Count)
                    {
                        //draw
                        await DrawAsync(gameNumber, lobbyChannel, "Decided by vote.");
                    }
                    else if (cancelCount == votes.Count)
                    {
                        //cancel
                        await CancelAsync(gameNumber, lobbyChannel, "Decided by vote.");
                    }
                    else
                    {
                        //Lock game votes and require admin to decide.
                        //TODO: Show votes by whoever
                        await SimpleEmbedAsync("Vote was not unanimous, game result must be decided by a moderator.", Color.DarkBlue);
                        game.VoteComplete = true;
                        db.Update(game);
                        db.SaveChanges();
                        return;
                    }
                }
                else
                {
                    await SimpleEmbedAsync($"Vote counted as: {vote.ToString()}", Color.Green);
                }
            }
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the specified lobby")]
        [RequirePermission(PermissionLevel.Moderator)]
        public async Task UndoGameAsync(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await UndoGameAsync(gameNumber, lobbyChannel);
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the current (or specified) lobby")]
        [RequirePermission(PermissionLevel.Moderator)]
        public async Task UndoGameAsync(int gameNumber, ISocketMessageChannel lobbyChannel = null)
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


        public async Task UndoScoreUpdatesAsync(GameResult game, Competition competition, Database db)
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


        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        //TODO: Explain that this does not affect the users who were in the game if it had a result. this is only for removing the game log from the database
        public async Task DelGame(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await DelGame(gameNumber, lobbyChannel);
        }

        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        //TODO: Explain that this does not affect the users who were in the game if it had a result. this is only for removing the game log from the database
        public async Task DelGame(int gameNumber, SocketTextChannel lobbyChannel = null)
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

                var game = db.GameResults.SingleOrDefault(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobby.ChannelId && x.GameId == gameNumber);
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
        }

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task CancelAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await CancelAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task CancelAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobbyWithQueue(lobbyChannel);
                if (lobby == null)
                {
                    //Reply error not a lobby.
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.Where(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    //Reply not valid game number.
                    await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                    return;
                }


                if (game.GameState != GameState.Undecided && game.GameState != GameState.Picking)
                {
                    await SimpleEmbedAsync($"Only games that are undecided or being picked can be cancelled.");
                    return;
                }



                if (game.GameState == GameState.Picking)
                {
                    db.RemoveRange(lobby.Queue);
                }
                game.GameState = GameState.Canceled;
                game.Submitter = Context.User.Id;
                game.Comment = comment;
                db.Update(game);
                db.SaveChanges();

                await GSS.AnnounceResultAsync(Context, lobby, game);
            }
        }


        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task DrawAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await DrawAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task DrawAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
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

                var game = db.GameResults.Where(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    //Reply not valid game number.
                    await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                    return;
                }

                if (game.GameState != GameState.Undecided)
                {
                    await ReplyAsync($"You can only call a draw on a game that hasn't been decided yet.");
                    return;
                }
                game.GameState = GameState.Draw;
                game.Submitter = Context.User.Id;
                game.Comment = comment;

                db.Update(game);
                db.SaveChanges();

                await DrawPlayersAsync(db.GetTeamFull(game, 1), db);
                await DrawPlayersAsync(db.GetTeamFull(game, 2), db);
                await SimpleEmbedAsync($"Called draw on game #{game.GameId}, player's game and draw counts have been updated.", Color.Green);
                await GSS.AnnounceResultAsync(Context, lobby, game);
            }
        }

        public Task DrawPlayersAsync(HashSet<ulong> playerIds, Database db)
        {
            foreach (var id in playerIds)
            {
                var player = db.Players.Find(Context.Guild.Id, id);
                if (player == null) continue;

                player.Draws++;
                db.Update(player);
            }

            db.SaveChanges();
            return Task.CompletedTask;
        }


        private async Task GameVoteAsync(Database db, Lobby lobby, GameResult game, int gameNumber, TeamSelection winning_team, HashSet<ulong> team1, HashSet<ulong> team2, [Remainder]string comment = null)
        {
            if (game.GameState == GameState.Decided || game.GameState == GameState.Draw)
            {
                await SimpleEmbedAsync("Game results cannot currently be overwritten without first running the `undogame` command.", Color.Red);
                return;
            }

            var competition = db.GetOrCreateCompetition(Context.Guild.Id);
            var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();

            List<(Player, int, Rank, RankChangeState, Rank)> winList;
            List<(Player, int, Rank, RankChangeState, Rank)> loseList;
            if (winning_team == TeamSelection.team1)
            {
                winList = GSS.UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team1, db);
                loseList = GSS.UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team2, db);
            }
            else
            {
                loseList = GSS.UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team1, db);
                winList = GSS.UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team2, db);
            }

            var allUsers = new List<(Player, int, Rank, RankChangeState, Rank)>();
            allUsers.AddRange(winList);
            allUsers.AddRange(loseList);

            foreach (var user in allUsers)
            {
                //Ignore user updates if they aren't found in the server.
                var gUser = Context.Guild.GetUser(user.Item1.UserId);
                if (gUser == null) continue;

                await UserService.UpdateUserAsync(competition, user.Item1, ranks, gUser);
            }

            game.GameState = GameState.Decided;
            game.WinningTeam = (int)winning_team;
            game.Comment = comment;
            game.Submitter = Context.User.Id;
            db.Update(game);
            db.SaveChanges();

            var winField = new EmbedFieldBuilder
            {
                //TODO: Is this necessary to show which team the winning team was?
                Name = $"Winning Team, Team{(int)winning_team}",
                Value = GSS.GetResponseContent(winList).FixLength(1023)
            };
            var loseField = new EmbedFieldBuilder
            {
                Name = $"Losing Team",
                Value = GSS.GetResponseContent(loseList).FixLength(1023)
            };
            var response = new EmbedBuilder
            {
                Fields = new List<EmbedFieldBuilder> { winField, loseField },
                //TODO: Remove this if from the vote command
                Title = $"Game Id: {gameNumber}"
            };

            if (!string.IsNullOrWhiteSpace(comment))
            {
                response.AddField("Comment", comment.FixLength(1023));
            }

            await GSS.AnnounceResultAsync(Context, lobby, response);
        }


        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and lobby with an optional comment")]
        [RequirePermission(PermissionLevel.Moderator)]
        public async Task GameAsync(SocketTextChannel lobbyChannel, int gameNumber, TeamSelection winning_team, [Remainder]string comment = null)
        {
            await GameAsync(gameNumber, winning_team, lobbyChannel, comment);
        }

        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and current (or specified) lobby with an optional comment")]
        [RequirePermission(PermissionLevel.Moderator)]
        public async Task GameAsync(int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            await GSS.GameAsync(Context, gameNumber, winning_team, lobbyChannel, comment);
        }
    }
}
