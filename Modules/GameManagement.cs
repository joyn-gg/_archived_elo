using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RavenBOT.ELO.Modules.Models.GameResult;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public class GameManagement : ReactiveBase
    {
        public ELOService Service { get; }

        public GameManagement(ELOService service)
        {
            Service = service;
        }

        //TODO: Ensure correct commands require mod/admin perms

        [Command("Results", RunMode = RunMode.Async)]
        [Summary("Shows possible vote options for the Result command")]
        public async Task ShowResultsAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", Extensions.EnumNames<GameResult.Vote.VoteState>()), Color.Blue);
        }


        [Command("Result", RunMode = RunMode.Sync)]
        [Summary("Vote on the specified game's outcome in the specified lobby")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Registered)]
        public async Task GameResultAsync(SocketTextChannel lobbyChannel, int gameNumber, string voteState)
        {
            await GameResultAsync(gameNumber, voteState, lobbyChannel);
        }

        [Command("Result", RunMode = RunMode.Sync)]
        [Summary("Vote on the specified game's outcome in the current (or specified) lobby")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Registered)]
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

            if (!Enum.TryParse(voteState, true, out GameResult.Vote.VoteState vote))
            {
                await SimpleEmbedAsync("Your vote was invalid. Please choose a result relevant to you. ie. Win (if you won the game) or Lose (if you lost the game)\nYou can view all possible results using the `Results` command.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobbyChannel.Id, gameNumber);
            if (game == null)
            {
                await SimpleEmbedAsync("Game not found.", Color.Red);
                return;
            }

            if (game.GameState != GameResult.State.Undecided)
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

            if (!game.Team1.Players.Contains(Context.User.Id) && !game.Team2.Players.Contains(Context.User.Id))
            {
                await SimpleEmbedAsync("You are not a player in this game and cannot vote on it's result.", Color.Red);
                return;
            }

            if (game.Votes.ContainsKey(Context.User.Id))
            {
                await SimpleEmbedAsync("You already submitted your vote for this game.", Color.DarkBlue);
                return;
            }

            var userVote = new Vote()
            {
                UserId = Context.User.Id,
                UserVote = vote
            };

            game.Votes.Add(Context.User.Id, userVote);

            //Ensure votes is greater than half the amount of players.
            if (game.Votes.Count * 2 > game.Team1.Players.Count + game.Team2.Players.Count)
            {
                var drawCount = game.Votes.Count(x => x.Value.UserVote == Vote.VoteState.Draw);
                var cancelCount = game.Votes.Count(x => x.Value.UserVote == Vote.VoteState.Cancel);

                var team1WinCount = game.Votes
                                        //Get players in team 1 and count wins
                                        .Where(x => game.Team1.Players.Contains(x.Key))
                                        .Count(x => x.Value.UserVote == Vote.VoteState.Win)
                                    +
                                    game.Votes
                                        //Get players in team 2 and count losses
                                        .Where(x => game.Team2.Players.Contains(x.Key))
                                        .Count(x => x.Value.UserVote == Vote.VoteState.Lose);

                var team2WinCount = game.Votes
                                        //Get players in team 2 and count wins
                                        .Where(x => game.Team2.Players.Contains(x.Key))
                                        .Count(x => x.Value.UserVote == Vote.VoteState.Win)
                                    +
                                    game.Votes
                                        //Get players in team 1 and count losses
                                        .Where(x => game.Team1.Players.Contains(x.Key))
                                        .Count(x => x.Value.UserVote == Vote.VoteState.Lose);

                if (team1WinCount == game.Votes.Count)
                {
                    //team1 win
                    Service.SaveGame(game);
                    await GameVoteAsync(gameNumber, TeamSelection.team1, lobbyChannel, "Decided by vote.");
                }
                else if (team2WinCount == game.Votes.Count)
                {
                    //team2 win
                    Service.SaveGame(game);
                    await GameVoteAsync(gameNumber, TeamSelection.team2, lobbyChannel, "Decided by vote.");
                }
                else if (drawCount == game.Votes.Count)
                {
                    //draw
                    Service.SaveGame(game);
                    await DrawAsync(gameNumber, lobbyChannel, "Decided by vote.");
                }
                else if (cancelCount == game.Votes.Count)
                {
                    //cancel
                    Service.SaveGame(game);
                    await CancelAsync(gameNumber, lobbyChannel, "Decided by vote.");
                }
                else
                {
                    //Lock game votes and require admin to decide.
                    //TODO: Show votes by whoever
                    await SimpleEmbedAsync("Vote was not unanimous, game result must be decided by a moderator.", Color.DarkBlue);
                    game.VoteComplete = true;
                    Service.SaveGame(game);
                    return;
                }
            }
            else
            {
                Service.SaveGame(game);
                await SimpleEmbedAsync($"Vote counted as: {vote.ToString()}", Color.Green);
            }
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the specified lobby")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task UndoGameAsync(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await UndoGameAsync(gameNumber, lobbyChannel);
        }

        [Command("UndoGame", RunMode = RunMode.Sync)]
        [Alias("Undo Game")]
        [Summary("Undoes the specified game in the current (or specified) lobby")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task UndoGameAsync(int gameNumber, SocketTextChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobby.ChannelId, gameNumber);
            if (game == null)
            {
                await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                return;
            }

            if (game.LegacyGame)
            {
                await SimpleEmbedAsync("This is a Legacy game which cannot be undone.", Color.DarkBlue);
                return;
            }

            if (game.GameState != GameResult.State.Decided)
            {
                await SimpleEmbedAsync("Game result is not decided and therefore cannot be undone.", Color.Red);
                return;
            }

            await UndoScoreUpdatesAsync(game, competition);
            await SimpleEmbedAsync($"Game #{gameNumber} in {lobbyChannel.Mention} Undone.");
        }

        public async Task AnnounceResultAsync(Lobby lobby, EmbedBuilder builder)
        {
            if (lobby.GameResultAnnouncementChannel != 0 && lobby.GameResultAnnouncementChannel != Context.Channel.Id)
            {
                var channel = Context.Guild.GetTextChannel(lobby.GameResultAnnouncementChannel);
                if (channel != null)
                {
                    try
                    {
                        await channel.SendMessageAsync("", false, builder.Build());
                    }
                    catch
                    {
                        //
                    }
                }
            }

            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }

        public async Task AnnounceResultAsync(Lobby lobby, GameResult game)
        {
            var embed = Service.GetGameEmbed(Context, game);
            await AnnounceResultAsync(lobby, embed);
        }


        public async Task UndoScoreUpdatesAsync(GameResult game, CompetitionConfig competition)
        {
            foreach (var score in game.ScoreUpdates)
            {
                var player = Service.GetPlayer(Context.Guild.Id, score.Key);
                if (player == null)
                {
                    //Skip if for whatever reason the player profile cannot be found.
                    continue;
                }

                var currentRank = competition.MaxRank(player.Points);

                if (score.Value < 0)
                {
                    //Points lost, so add them back
                    player.Losses--;
                    player.SetPoints(competition, player.Points + Math.Abs(score.Value));
                }
                else
                {
                    //Points gained so remove them
                    player.Wins--;
                    player.SetPoints(competition, player.Points - Math.Abs(score.Value));
                }

                //Save the player profile after updating scores.
                Service.SavePlayer(player);


                var guildUser = Context.Guild.GetUser(player.UserId);
                if (guildUser == null)
                {
                    //The user cannot be found in the server so skip updating their name/profile
                    continue;
                }

                await Service.UpdateUserAsync(competition, player, guildUser);
            }

            game.GameState = State.Undecided;
            game.ScoreUpdates = new Dictionary<ulong, int>();
            Service.SaveGame(game);
        }


        /*
        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history")]
        [Preconditions.RequireAdmin]
        //TODO: Explain that this does not affect the users who were in the game if it had a result. this is only for removing the game log from the database
        public async Task DelGame(SocketTextChannel lobbyChannel, int gameNumber)
        {
            await DelGame(gameNumber, lobbyChannel);
        }

        [Command("DeleteGame", RunMode = RunMode.Sync)]
        [Alias("Delete Game", "DelGame")]
        [Summary("Deletes the specified game from history")]
        [Preconditions.RequireAdmin]
        //TODO: Explain that this does not affect the users who were in the game if it had a result. this is only for removing the game log from the database
        public async Task DelGame(int gameNumber, SocketTextChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                //Reply error not a lobby.
                await ReplyAsync("Channel is not a lobby.");
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobbyChannel.Id, gameNumber);
            if (game == null)
            {
                await ReplyAsync("Invalid GameID.");
                return;
            }

            Service.RemoveGame(game);
            await ReplyAsync("Game Deleted.", false, JsonConvert.SerializeObject(game, Formatting.Indented).FixLength(2047).QuickEmbed());
        }
        */

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task CancelAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await CancelAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Cancel", RunMode = RunMode.Sync)]
        [Alias("CancelGame")]
        [Summary("Cancels the specified game in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task CancelAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                //Reply error not a lobby.
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobby.ChannelId, gameNumber);
            if (game == null)
            {
                //Reply not valid game number.
                await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                return;
            }

            game.GameState = State.Canceled;
            game.Submitter = Context.User.Id;
            game.Comment = comment;
            game.ScoreUpdates = new Dictionary<ulong, int>();
            Service.SaveGame(game);

            await AnnounceResultAsync(lobby, game);
        }


        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified game in the specified lobby with an optional comment.")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task DrawAsync(SocketTextChannel lobbyChannel, int gameNumber, [Remainder]string comment = null)
        {
            await DrawAsync(gameNumber, lobbyChannel, comment);
        }

        [Command("Draw", RunMode = RunMode.Sync)]
        [Summary("Calls a draw for the specified in the current (or specified) lobby with an optional comment.")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task DrawAsync(int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                //Reply error not a lobby.
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobby.ChannelId, gameNumber);
            if (game == null)
            {
                //Reply not valid game number.
                await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                return;
            }

            game.GameState = State.Draw;
            game.Submitter = Context.User.Id;
            game.Comment = comment;
            game.ScoreUpdates = new Dictionary<ulong, int>();
            Service.SaveGame(game);

            await DrawPlayersAsync(game.Team1.Players);
            await DrawPlayersAsync(game.Team2.Players);
            await SimpleEmbedAsync($"Called draw on game #{game.GameId}, player's game and draw counts have been updated.", Color.Green);
            await AnnounceResultAsync(lobby, game);
        }

        public Task DrawPlayersAsync(HashSet<ulong> playerIds)
        {
            foreach (var id in playerIds)
            {
                var player = Service.GetPlayer(Context.Guild.Id, id);
                if (player == null) continue;

                player.Draws++;
                Service.SavePlayer(player);
            }

            return Task.CompletedTask;
        }

        private async Task GameVoteAsync(int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                //Reply error not a lobby.
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobby.ChannelId, gameNumber);
            if (game == null)
            {
                //Reply not valid game number.
                await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                return;
            }

            if (game.GameState == State.Decided || game.GameState == State.Draw)
            {
                await SimpleEmbedAsync("Game results cannot currently be overwritten without first running the `undogame` command.", Color.Red);
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            List<(Player, int, Rank, RankChangeState, Rank)> winList;
            List<(Player, int, Rank, RankChangeState, Rank)> loseList;
            if (winning_team == TeamSelection.team1)
            {
                winList = UpdateTeamScoresAsync(competition, true, game.Team1.Players);
                loseList = UpdateTeamScoresAsync(competition, false, game.Team2.Players);
            }
            else
            {
                loseList = UpdateTeamScoresAsync(competition, false, game.Team1.Players);
                winList = UpdateTeamScoresAsync(competition, true, game.Team2.Players);
            }

            var allUsers = new List<(Player, int, Rank, RankChangeState, Rank)>();
            allUsers.AddRange(winList);
            allUsers.AddRange(loseList);

            foreach (var user in allUsers)
            {
                //Ignore user updates if they aren't found in the server.
                var gUser = Context.Guild.GetUser(user.Item1.UserId);
                if (gUser == null) continue;

                await Service.UpdateUserAsync(competition, user.Item1, gUser);
            }

            game.GameState = GameResult.State.Decided;
            game.ScoreUpdates = allUsers.ToDictionary(x => x.Item1.UserId, y => y.Item2);
            game.WinningTeam = (int)winning_team;
            game.Comment = comment;
            game.Submitter = Context.User.Id;
            Service.SaveGame(game);

            var winField = new EmbedFieldBuilder
            {
                //TODO: Is this necessary to show which team the winning team was?
                Name = $"Winning Team, Team{(int)winning_team}",
                Value = GetResponseContent(winList).FixLength(1023)
            };
            var loseField = new EmbedFieldBuilder
            {
                Name = $"Losing Team",
                Value = GetResponseContent(loseList).FixLength(1023)
            };
            var response = new EmbedBuilder
            {
                Fields = new List<EmbedFieldBuilder> { winField, loseField },
                //TODO: Remove this if from the vote command
                Title = $"GameID: {gameNumber}"
            };

            if (!string.IsNullOrWhiteSpace(comment))
            {
                response.AddField("Comment", comment.FixLength(1023));
            }

            await AnnounceResultAsync(lobby, response);
        }


        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and lobby with an optional comment")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task GameAsync(SocketTextChannel lobbyChannel, int gameNumber, TeamSelection winning_team, [Remainder]string comment = null)
        {
            await GameAsync(gameNumber, winning_team, lobbyChannel, comment);
        }

        [Command("Game", RunMode = RunMode.Sync)]
        [Alias("g")]
        [Summary("Calls a win for the specified team in the specified game and current (or specified) lobby with an optional comment")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task GameAsync(int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = Context.Channel as SocketTextChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                //Reply error not a lobby.
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobby.ChannelId, gameNumber);
            if (game == null)
            {
                //Reply not valid game number.
                await SimpleEmbedAsync($"Game not found. Most recent game is {lobby.CurrentGameCount}", Color.DarkBlue);
                return;
            }

            if (game.GameState == GameResult.State.Decided || game.GameState == GameResult.State.Draw)
            {
                await SimpleEmbedAsync("Game results cannot currently be overwritten without first running the `undogame` command.", Color.Red);
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            List<(Player, int, Rank, RankChangeState, Rank)> winList;
            List<(Player, int, Rank, RankChangeState, Rank)> loseList;
            if (winning_team == TeamSelection.team1)
            {
                winList = UpdateTeamScoresAsync(competition, true, game.Team1.Players);
                loseList = UpdateTeamScoresAsync(competition, false, game.Team2.Players);
            }
            else
            {
                loseList = UpdateTeamScoresAsync(competition, false, game.Team1.Players);
                winList = UpdateTeamScoresAsync(competition, true, game.Team2.Players);
            }

            var allUsers = new List<(Player, int, Rank, RankChangeState, Rank)>();
            allUsers.AddRange(winList);
            allUsers.AddRange(loseList);

            foreach (var user in allUsers)
            {
                //Ignore user updates if they aren't found in the server.
                var gUser = Context.Guild.GetUser(user.Item1.UserId);
                if (gUser == null) continue;

                await Service.UpdateUserAsync(competition, user.Item1, gUser);
            }

            game.GameState = GameResult.State.Decided;
            game.ScoreUpdates = allUsers.ToDictionary(x => x.Item1.UserId, y => y.Item2);
            game.WinningTeam = (int)winning_team;
            game.Comment = comment;
            game.Submitter = Context.User.Id;
            Service.SaveGame(game);

            var winField = new EmbedFieldBuilder
            {
                //TODO: Is this necessary to show which team the winning team was?
                Name = $"Winning Team, Team{(int)winning_team}",
                Value = GetResponseContent(winList).FixLength(1023)
            };
            var loseField = new EmbedFieldBuilder
            {
                Name = $"Losing Team",
                Value = GetResponseContent(loseList).FixLength(1023)
            };
            var response = new EmbedBuilder
            {
                Fields = new List<EmbedFieldBuilder> { winField, loseField },
                //TODO: Remove this if from the vote command
                Title = $"{lobbyChannel.Name} Game: #{gameNumber} Result called by {Context.User.Username}#{Context.User.Discriminator}".FixLength(127)
            };

            if (!string.IsNullOrWhiteSpace(comment))
            {
                response.AddField("Comment", comment.FixLength(1023));
            }

            await AnnounceResultAsync(lobby, response);
        }

        public string GetResponseContent(List<(Player, int, Rank, RankChangeState, Rank)> players)
        {
            var sb = new StringBuilder();
            foreach (var player in players)
            {
                if (player.Item4 == RankChangeState.None)
                {
                    sb.AppendLine($"{player.Item1.GetDisplayNameSafe()} **Points:** {player.Item1.Points - player.Item2}{(player.Item2 >= 0 ? $" + {player.Item2}" : $" - {Math.Abs(player.Item2)}")} = {player.Item1.Points}");
                    continue;
                }

                string originalRole = null;
                string newRole = null;
                if (player.Item3 != null)
                {
                    originalRole = MentionUtils.MentionRole(player.Item3.RoleId);
                }

                if (player.Item5 != null)
                {
                    newRole = MentionUtils.MentionRole(player.Item5.RoleId);
                }

                sb.AppendLine($"{player.Item1.GetDisplayNameSafe()} **Points:** {player.Item1.Points - player.Item2}{(player.Item2 >= 0 ? $" + {player.Item2}" : $" - {Math.Abs(player.Item2)}")} = {player.Item1.Points} **Rank:** {originalRole ?? "N.A"} => {newRole ?? "N/A"}");

            }

            return sb.ToString();
        }

        public enum RankChangeState
        {
            DeRank,
            RankUp,
            None
        }

        //returns a list of userIds and the amount of points they received/lost for the win/loss, and if the user lost/gained a rank
        //UserId, Points added/removed, rank before, rank modify state, rank after
        /// <summary>
        /// Retrieves and updates player scores/wins
        /// </summary>
        /// <returns>
        /// A list containing a value tuple with the
        /// Player object
        /// Amount of points received/lost
        /// The player's current rank
        /// The player's rank change state (rank up, derank, none)
        /// The players new rank (if changed)
        /// </returns>
        public List<(Player, int, Rank, RankChangeState, Rank)> UpdateTeamScoresAsync(CompetitionConfig competition, bool win, HashSet<ulong> userIds)
        {
            var updates = new List<(Player, int, Rank, RankChangeState, Rank)>();
            foreach (var userId in userIds)
            {
                var player = Service.GetPlayer(Context.Guild.Id, userId);
                if (player == null) continue;

                //This represents the current user's rank
                var maxRank = competition.MaxRank(player.Points);

                int updateVal;
                RankChangeState state = RankChangeState.None;
                Rank newRank = null;

                if (win)
                {
                    updateVal = maxRank?.WinModifier ?? competition.DefaultWinModifier;
                    player.SetPoints(competition, player.Points + updateVal);
                    player.Wins++;
                    newRank = competition.MaxRank(player.Points);
                    if (newRank != null)
                    {
                        if (maxRank == null)
                        {
                            state = RankChangeState.RankUp;
                        }
                        else if (newRank.RoleId != maxRank.RoleId)
                        {
                            state = RankChangeState.RankUp;
                        }
                    }
                }
                else
                {
                    //Loss modifiers are always positive values that are to be subtracted
                    updateVal = maxRank?.LossModifier ?? competition.DefaultLossModifier;
                    player.SetPoints(competition, player.Points - updateVal);
                    player.Losses++;
                    //Set the update value to a negative value for returning purposes.
                    updateVal = -updateVal;

                    if (maxRank != null)
                    {
                        if (player.Points < maxRank.Points)
                        {
                            state = RankChangeState.DeRank;
                            newRank = competition.MaxRank(player.Points);
                        }
                    }
                }

                updates.Add((player, updateVal, maxRank, state, newRank));

                //TODO: Rank checking?
                //I forget what this means honestly
                Service.SavePlayer(player);
            }

            return updates;
        }
    }
}