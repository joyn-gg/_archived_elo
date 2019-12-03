using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class GameSubmissionService
    {
        public GameService GameService { get; }
        public UserService UserService { get; }

        public GameSubmissionService(GameService gameService, UserService userService)
        {
            GameService = gameService;
            UserService = userService;
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
        public List<(Player, int, Rank, RankChangeState, Rank)> UpdateTeamScoresAsync(Competition competition, Lobby lobby, GameResult game, Rank[] ranks, bool win, HashSet<ulong> userIds, Database db)
        {
            var updates = new List<(Player, int, Rank, RankChangeState, Rank)>();
            foreach (var userId in userIds)
            {
                var player = db.Players.Find(competition.GuildId, userId);
                if (player == null) continue;

                //This represents the current user's rank
                var maxRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();

                int updateVal;
                RankChangeState state = RankChangeState.None;
                Rank newRank = null;

                if (win)
                {
                    updateVal = (int)((maxRank?.WinModifier ?? competition.DefaultWinModifier) * lobby.LobbyMultiplier);
                    if (lobby.HighLimit != null)
                    {
                        if (player.Points > lobby.HighLimit)
                        {
                            updateVal = (int)(updateVal * lobby.ReductionPercent);
                        }
                    }
                    player.Points += updateVal;
                    player.Wins++;
                    newRank = ranks.Where(x => x.Points <= player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
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
                    if (lobby.MultiplyLossValue)
                    {
                        updateVal = (int)(updateVal * lobby.LobbyMultiplier);
                    }

                    player.Points -= updateVal;
                    if (!competition.AllowNegativeScore && player.Points < 0) player.Points = 0;
                    player.Losses++;
                    //Set the update value to a negative value for returning purposes.
                    updateVal = -Math.Abs(updateVal);

                    if (maxRank != null)
                    {
                        if (player.Points < maxRank.Points)
                        {
                            state = RankChangeState.DeRank;
                            newRank = ranks.Where(x => x.Points <= player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
                        }
                    }
                }

                updates.Add((player, updateVal, maxRank, state, newRank));
                var oldUpdate = db.ScoreUpdates.FirstOrDefault(x => x.ChannelId == lobby.ChannelId && x.GameNumber == game.GameId && x.UserId == userId);
                if (oldUpdate == null)
                {
                    var update = new ScoreUpdate
                    {
                        GuildId = competition.GuildId,
                        ChannelId = game.LobbyId,
                        UserId = userId,
                        GameNumber = game.GameId,
                        ModifyAmount = updateVal
                    };
                    db.ScoreUpdates.Add(update);
                }
                else
                {
                    oldUpdate.ModifyAmount = updateVal;
                    db.ScoreUpdates.Update(oldUpdate);
                }

                db.Update(player);
            }
            db.SaveChanges();
            return updates;
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

        public virtual async Task GameAsync(ShardedCommandContext context, int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = context.Channel as SocketTextChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    //Reply error not a lobby.
                    await context.Channel.SendMessageAsync("", false, "Channel is not a lobby.".QuickEmbed(Color.Red));
                    return;
                }

                var game = db.GameResults.Where(x => x.GuildId == context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    //Reply not valid game number.
                    await context.Channel.SendMessageAsync("", false, $"Game not found. Most recent game is {lobby.CurrentGameCount}".QuickEmbed(Color.DarkBlue));
                    return;
                }

                if (game.GameState == GameState.Decided || game.GameState == GameState.Draw)
                {
                    await context.Channel.SendMessageAsync("", false, "Game results cannot currently be overwritten without first running the `undogame` command.".QuickEmbed(Color.Red));
                    return;
                }

                var competition = db.GetOrCreateCompetition(context.Guild.Id);
                var ranks = db.Ranks.Where(x => x.GuildId == context.Guild.Id).ToArray();

                List<(Player, int, Rank, RankChangeState, Rank)> winList;
                List<(Player, int, Rank, RankChangeState, Rank)> loseList;
                var team1 = db.GetTeamFull(game, 1);
                var team2 = db.GetTeamFull(game, 2);
                if (winning_team == TeamSelection.team1)
                {
                    winList = UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team1, db);
                    loseList = UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team2, db);
                }
                else
                {
                    loseList = UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team1, db);
                    winList = UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team2, db);
                }

                var allUsers = new List<(Player, int, Rank, RankChangeState, Rank)>();
                allUsers.AddRange(winList);
                allUsers.AddRange(loseList);

                foreach (var user in allUsers)
                {
                    //Ignore user updates if they aren't found in the server.
                    var gUser = context.Guild.GetUser(user.Item1.UserId);
                    if (gUser == null) continue;

                    await UserService.UpdateUserAsync(competition, user.Item1, ranks, gUser);
                }

                game.GameState = GameState.Decided;

                game.WinningTeam = (int)winning_team;
                game.Comment = comment;
                game.Submitter = context.User.Id;
                db.Update(game);
                db.SaveChanges();

                var winField = new EmbedFieldBuilder
                {
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
                    Title = $"{lobbyChannel.Name} Game: #{gameNumber} Result called by {context.User.Username}#{context.User.Discriminator}".FixLength(127)
                };

                if (!string.IsNullOrWhiteSpace(comment))
                {
                    response.AddField("Comment", comment.FixLength(1023));
                }

                await AnnounceResultAsync(context, lobby, response);
            }
        }

        public virtual async Task AnnounceResultAsync(ShardedCommandContext context, Lobby lobby, EmbedBuilder builder)
        {
            if (lobby.GameResultAnnouncementChannel != null && lobby.GameResultAnnouncementChannel != context.Channel.Id)
            {
                var channel = context.Guild.GetTextChannel(lobby.GameResultAnnouncementChannel.Value);
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

            await context.Channel.SendMessageAsync("", false, builder.Build());
        }

        public virtual async Task AnnounceResultAsync(ShardedCommandContext context, Lobby lobby, GameResult game)
        {
            var embed = GameService.GetGameEmbed(game);
            await AnnounceResultAsync(context, lobby, embed);
        }


        public virtual async Task DrawAsync(ShardedCommandContext context, int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = context.Channel as SocketTextChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    //Reply error not a lobby.
                    await context.Channel.SendMessageAsync("", false, "Channel is not a lobby.".QuickEmbed(Color.Red));
                    return;
                }

                var game = db.GameResults.Where(x => x.GuildId == context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    //Reply not valid game number.
                    await context.Channel.SendMessageAsync("", false, $"Game not found. Most recent game is {lobby.CurrentGameCount}".QuickEmbed(Color.DarkBlue));
                    return;
                }

                if (game.GameState != GameState.Undecided)
                {
                    await context.Channel.SendMessageAsync("", false, "You can only call a draw on a game that hasn't been decided yet.".QuickEmbed(Color.Red));
                    return;
                }
                game.GameState = GameState.Draw;
                game.Submitter = context.User.Id;
                game.Comment = comment;

                db.Update(game);
                db.SaveChanges();

                await DrawPlayersAsync(context, db.GetTeamFull(game, 1), db);
                await DrawPlayersAsync(context, db.GetTeamFull(game, 2), db);
                await context.Channel.SendMessageAsync("", false, $"Called draw on game #{game.GameId}, player's game and draw counts have been updated.".QuickEmbed(Color.Green));
                await AnnounceResultAsync(context, lobby, game);
            }
        }

        public Task DrawPlayersAsync(ShardedCommandContext context, HashSet<ulong> playerIds, Database db)
        {
            foreach (var id in playerIds)
            {
                var player = db.Players.Find(context.Guild.Id, id);
                if (player == null) continue;

                player.Draws++;
                db.Update(player);
            }

            db.SaveChanges();
            return Task.CompletedTask;
        }


        public virtual async Task GameVoteAsync(ShardedCommandContext context, Database db, Lobby lobby, GameResult game, int gameNumber, TeamSelection winning_team, HashSet<ulong> team1, HashSet<ulong> team2, [Remainder]string comment = null)
        {
            if (game.GameState == GameState.Decided || game.GameState == GameState.Draw)
            {
                await context.Channel.SendMessageAsync("", false, "Game results cannot currently be overwritten without first running the `undogame` command.".QuickEmbed(Color.Red));
                return;
            }

            var competition = db.GetOrCreateCompetition(context.Guild.Id);
            var ranks = db.Ranks.Where(x => x.GuildId == context.Guild.Id).ToArray();

            List<(Player, int, Rank, RankChangeState, Rank)> winList;
            List<(Player, int, Rank, RankChangeState, Rank)> loseList;
            if (winning_team == TeamSelection.team1)
            {
                winList = UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team1, db);
                loseList = UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team2, db);
            }
            else
            {
                loseList = UpdateTeamScoresAsync(competition, lobby, game, ranks, false, team1, db);
                winList = UpdateTeamScoresAsync(competition, lobby, game, ranks, true, team2, db);
            }

            var allUsers = new List<(Player, int, Rank, RankChangeState, Rank)>();
            allUsers.AddRange(winList);
            allUsers.AddRange(loseList);

            foreach (var user in allUsers)
            {
                //Ignore user updates if they aren't found in the server.
                var gUser = context.Guild.GetUser(user.Item1.UserId);
                if (gUser == null) continue;

                await UserService.UpdateUserAsync(competition, user.Item1, ranks, gUser);
            }

            game.GameState = GameState.Decided;
            game.WinningTeam = (int)winning_team;
            game.Comment = comment;
            game.Submitter = context.User.Id;
            db.Update(game);
            db.SaveChanges();

            var winField = new EmbedFieldBuilder
            {
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
                Title = $"Game Id: {gameNumber}"
            };

            if (!string.IsNullOrWhiteSpace(comment))
            {
                response.AddField("Comment", comment.FixLength(1023));
            }

            await AnnounceResultAsync(context, lobby, response);
        }

        public virtual async Task CancelAsync(ShardedCommandContext context, int gameNumber, SocketTextChannel lobbyChannel = null, [Remainder]string comment = null)
        {
            if (lobbyChannel == null)
            {
                //If no lobby is provided, assume that it is the current channel.
                lobbyChannel = context.Channel as SocketTextChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobbyWithQueue(lobbyChannel);
                if (lobby == null)
                {
                    //Reply error not a lobby.
                    await context.Channel.SendMessageAsync("", false, "Channel is not a lobby.".QuickEmbed(Color.Red));
                    return;
                }

                var game = db.GameResults.Where(x => x.GuildId == context.Guild.Id && x.LobbyId == lobbyChannel.Id && x.GameId == gameNumber).FirstOrDefault();
                if (game == null)
                {
                    //Reply not valid game number.
                    await context.Channel.SendMessageAsync("", false, $"Game not found. Most recent game is {lobby.CurrentGameCount}".QuickEmbed(Color.DarkBlue));
                    return;
                }


                if (game.GameState != GameState.Undecided && game.GameState != GameState.Picking)
                {
                    await context.Channel.SendMessageAsync("", false, $"Only games that are undecided or being picked can be cancelled.".QuickEmbed());
                    return;
                }



                if (game.GameState == GameState.Picking)
                {
                    db.RemoveRange(lobby.Queue);
                }
                game.GameState = GameState.Canceled;
                game.Submitter = context.User.Id;
                game.Comment = comment;
                db.Update(game);
                db.SaveChanges();

                await AnnounceResultAsync(context, lobby, game);
            }
        }

        public virtual async Task GameResultAsync(ShardedCommandContext context, int gameNumber, string voteState, SocketTextChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = context.Channel as SocketTextChannel;
            }

            //Do vote conversion to ensure that the state is a string and not an int (to avoid confusion with team number from old elo version)
            if (int.TryParse(voteState, out var voteNumber))
            {
                await context.Channel.SendMessageAsync("", false, "Please supply a result relevant to you rather than the team number. Use the `Results` command to see a list of these.".QuickEmbed(Color.DarkBlue));
                return;
            }

            if (!Enum.TryParse(voteState, true, out VoteState vote))
            {
                await context.Channel.SendMessageAsync("", false, "Your vote was invalid. Please choose a result relevant to you. ie. Win (if you won the game) or Lose (if you lost the game)\nYou can view all possible results using the `Results` command.".QuickEmbed(Color.Red));
                return;
            }

            using (var db = new Database())
            {
                var comp = db.GetOrCreateCompetition(context.Guild.Id);
                if (!comp.AllowVoting)
                {
                    await context.Channel.SendMessageAsync("", false, "Voting is not enabled.".QuickEmbed(Color.Red));
                    return;
                }

                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    await context.Channel.SendMessageAsync("", false, "Current channel is not a lobby.".QuickEmbed(Color.Red));
                    return;
                }

                var game = db.GameResults.FirstOrDefault(x => x.LobbyId == lobby.ChannelId && x.GameId == gameNumber);
                if (game == null)
                {
                    await context.Channel.SendMessageAsync("", false, "Game not found.".QuickEmbed(Color.Red));
                    return;
                }

                if (game.GameState != GameState.Undecided)
                {
                    await context.Channel.SendMessageAsync("", false, "You can only vote on the result of undecided games.".QuickEmbed(Color.Red));
                    return;
                }
                else if (game.VoteComplete)
                {
                    //Result is undecided but vote has taken place, therefore it wasn't unanimous
                    await context.Channel.SendMessageAsync("", false, "Vote has already been taken on this game but wasn't unanimous, ask an admin to submit the result.".QuickEmbed(Color.DarkBlue));
                    return;
                }

                var team1 = db.GetTeamFull(game, 1);
                var team2 = db.GetTeamFull(game, 2);

                //TODO: Automatically submit if vote is from an admin.
                if (team1.All(x => x != context.User.Id) && team2.All(x => x != context.User.Id))
                {
                    await context.Channel.SendMessageAsync("", false, "You are not a player in this game and cannot vote on it's result.".QuickEmbed(Color.Red));
                    return;
                }

                var votes = db.Votes.Where(x => x.ChannelId == lobby.ChannelId && x.GameId == gameNumber).ToList();
                if (votes.Any(x => x.UserId == context.User.Id))
                {
                    await context.Channel.SendMessageAsync("", false, "You already submitted your vote for this game.".QuickEmbed(Color.DarkBlue));
                    return;
                }

                var userVote = new GameVote()
                {
                    UserId = context.User.Id,
                    GameId = gameNumber,
                    ChannelId = lobby.ChannelId,
                    GuildId = context.Guild.Id,
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
                        await GameVoteAsync(context, db, lobby, game, gameNumber, TeamSelection.team1, team1.ToHashSet(), team2.ToHashSet(), "Decided by vote.");
                    }
                    else if (team2WinCount == votes.Count)
                    {
                        //team2 win
                        await GameVoteAsync(context, db, lobby, game, gameNumber, TeamSelection.team2, team1.ToHashSet(), team2.ToHashSet(), "Decided by vote.");
                    }
                    else if (drawCount == votes.Count)
                    {
                        //draw
                        await DrawAsync(context, gameNumber, lobbyChannel, "Decided by vote.");
                    }
                    else if (cancelCount == votes.Count)
                    {
                        //cancel
                        await CancelAsync(context, gameNumber, lobbyChannel, "Decided by vote.");
                    }
                    else
                    {
                        //Lock game votes and require admin to decide.
                        var voteInfo = votes.Select(x =>
                        {
                            if (x.UserVote == VoteState.Win || x.UserVote == VoteState.Lose)
                            {
                                return $"{MentionUtils.MentionUser(x.UserId)} - Team {(team1.Contains(x.UserId) ? 1 : 2)} {x.UserVote}";
                            }
                            else
                            {
                                return $"{MentionUtils.MentionUser(x.UserId)} - {x.UserVote}";
                            }
                        });
                        
                        await context.Channel.SendMessageAsync("", false, $"Vote was not unanimous, game result must be decided by a moderator.\n{voteInfo}".QuickEmbed(Color.DarkBlue));
                        game.VoteComplete = true;
                        db.Update(game);
                        db.SaveChanges();
                        return;
                    }
                }
                else
                {
                    await context.Channel.SendMessageAsync("", false, $"Vote counted as: {vote.ToString()}".QuickEmbed(Color.Green));
                }
            }
        }
    }
}
