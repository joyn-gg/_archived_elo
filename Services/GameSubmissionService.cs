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
                    newRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
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
                            newRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
                        }
                    }
                }

                updates.Add((player, updateVal, maxRank, state, newRank));
                var update = new ScoreUpdate
                {
                    GuildId = competition.GuildId,
                    ChannelId = game.LobbyId,
                    UserId = player.UserId,
                    GameNumber = game.GameId,
                    ModifyAmount = updateVal
                };
                db.ScoreUpdates.Add(update);
                db.Entry(update).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
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

        public async Task GameAsync(ShardedCommandContext context, int gameNumber, TeamSelection winning_team, SocketTextChannel lobbyChannel = null, string comment = null)
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

                db.ScoreUpdates.AddRange(allUsers.Select(x => new ScoreUpdate
                {
                    UserId = x.Item1.UserId,
                    GuildId = context.Guild.Id,
                    ChannelId = game.LobbyId,
                    GameNumber = game.GameId,
                    ModifyAmount = x.Item2
                }));


                game.WinningTeam = (int)winning_team;
                game.Comment = comment;
                game.Submitter = context.User.Id;
                db.Update(game);
                db.SaveChanges();

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
                    Title = $"{lobbyChannel.Name} Game: #{gameNumber} Result called by {context.User.Username}#{context.User.Discriminator}".FixLength(127)
                };

                if (!string.IsNullOrWhiteSpace(comment))
                {
                    response.AddField("Comment", comment.FixLength(1023));
                }

                await AnnounceResultAsync(context, lobby, response);
            }
        }

        public async Task AnnounceResultAsync(ShardedCommandContext context, Lobby lobby, EmbedBuilder builder)
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

        public async Task AnnounceResultAsync(ShardedCommandContext context, Lobby lobby, GameResult game)
        {
            var embed = GameService.GetGameEmbed(game);
            await AnnounceResultAsync(context, lobby, embed);
        }
    }
}
