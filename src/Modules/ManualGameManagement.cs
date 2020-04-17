using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [Preconditions.RequirePermission(PermissionLevel.Moderator)]
    [RavenRequireContext(ContextType.Guild)]
    public class ManualGameManagement : ReactiveBase
    {
        public UserService UserService { get; }

        public ManualGameManagement(UserService userService)
        {
            UserService = userService;
        }

        [Command("Win", RunMode = RunMode.Sync)]
        [Summary("Adds a win and updates points for the specified users.")]
        public virtual async Task WinAsync(params SocketGuildUser[] users)
        {
            await UpdateTeamScoresAsync(true, users.Select(x => x.Id).ToHashSet());
        }

        [Command("Lose", RunMode = RunMode.Sync)]
        [Summary("Adds a loss and updates points for the specified users.")]
        public virtual async Task LoseAsync(params SocketGuildUser[] users)
        {
            await UpdateTeamScoresAsync(false, users.Select(x => x.Id).ToHashSet());
        }

        //TODO: Display manual game info/stats

        [Command("UndoManualGame", RunMode = RunMode.Sync)]
        [Summary("Adds a win and updates points for the specified users.")]
        public virtual async Task UndoManualAsync(int gameId)
        {
            using (var db = new Database())
            {
                var game = db.ManualGameResults.Find(Context.Guild.Id, gameId);
                if (game == null)
                {
                    await SimpleEmbedAsync("Invalid game id.", Color.Red);
                    return;
                }
                var responseEmbed = new EmbedBuilder();
                responseEmbed.AddField("Game Info", $"State: {game.GameState}\n" +
                                                    $"Submitted by: {MentionUtils.MentionUser(game.Submitter)}\n" +
                                                    $"Submitted at: {game.CreationTime}");
                var updateChanges = new StringBuilder();
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                var scoreUpdates = db.ManualGameScoreUpdates.Where(x => x.GuildId == Context.Guild.Id && x.ManualGameId == gameId).ToArray();
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                foreach (var scoreUpdate in scoreUpdates)
                {
                    var player = db.Players.Find(Context.Guild.Id, scoreUpdate.UserId);
                    if (player == null) continue;
                    if (game.GameState == ManualGameState.Win)
                    {
                        player.Wins--;
                    }
                    else if (game.GameState == ManualGameState.Lose)
                    {
                        player.Losses--;
                    }
                    else if (game.GameState == ManualGameState.Draw)
                    {
                        player.Draws--;
                    }
                    player.Points -= scoreUpdate.ModifyAmount;
                    if (!competition.AllowNegativeScore && player.Points < 0) player.Points = 0;
                    db.ManualGameScoreUpdates.Remove(scoreUpdate);
                    db.Players.Update(player);

                    var gUser = Context.Guild.GetUser(player.UserId);
                    if (gUser == null) continue;

                    var _ = Task.Run(() => UserService.UpdateUserAsync(competition, player, ranks, gUser));
                }

                db.ManualGameResults.Remove(game);
                db.SaveChanges();
                await SimpleEmbedAsync("Manual game undone.");
            }
        }


        public virtual async Task UpdateTeamScoresAsync(bool win, HashSet<ulong> userIds)
        {
            using (var db = new Database())
            {
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                var updates = new List<(Player, int, Rank, RankChangeState, Rank)>();
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                var embed = new EmbedBuilder
                {
                    Title = (win ? "Win" : "Lose") + $" Manual Game: #{competition.ManualGameCounter + 1}",
                    Color = win ? Color.Green : Color.Red,
                };

                var sb = new StringBuilder();
                foreach (var userId in userIds)
                {
                    var player = db.Players.Find(Context.Guild.Id, userId);
                    if (player == null) continue;

                    //This represents the current user's rank
                    var maxRank = ranks.Where(x => x.Points <= player.Points).OrderByDescending(x => x.Points).FirstOrDefault();

                    int updateVal;
                    RankChangeState state = RankChangeState.None;
                    Rank newRank = null;

                    if (win)
                    {
                        updateVal = maxRank?.WinModifier ?? competition.DefaultWinModifier;
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
                        //Loss modifier is always positive so subtract it
                        updateVal = maxRank?.LossModifier ?? competition.DefaultLossModifier;

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
                    db.Update(player);

                    //Ignore user updates if they aren't found in the server.
                    var gUser = Context.Guild.GetUser(userId);
                    if (gUser == null) continue;

                    var _ = Task.Run(async () => await UserService.UpdateUserAsync(competition, player, ranks, gUser));

                    var rankUpdate = "";
                    if (maxRank != null || newRank != null)
                    {
                        var oldRoleMention = maxRank == null ? "N/A" : MentionUtils.MentionRole(maxRank.RoleId);
                        var newRoleMention = newRank == null ? "N/A" : MentionUtils.MentionRole(newRank.RoleId);
                        rankUpdate = $" Rank: {oldRoleMention} => {newRoleMention}";
                    }

                    sb.AppendLine($"{gUser.Mention} Points: {player.Points} {(win ? "Added:" : "Removed:")} {updateVal}{rankUpdate}");
                }

                //Update counter and save new competition config


                //Create new game info
                var vals = ((IQueryable<ManualGameResult>)db.ManualGameResults).Where(x => x.GuildId == Context.Guild.Id).ToArray();
                var count = vals.Length == 0 ? 0 : vals.Max(x => x.GameId);
                var game = new ManualGameResult
                {
                    GuildId = Context.Guild.Id,
                    GameId = count + 1 
                };

                competition.ManualGameCounter = game.GameId;
                db.Update(competition);
                game.Submitter = Context.User.Id;
                game.GameState = win ? ManualGameState.Win : ManualGameState.Lose;
                db.ManualGameResults.Add(game);
                db.SaveChanges();
                game = db.ManualGameResults.Where(x => x.GuildId == Context.Guild.Id).OrderByDescending(x => x.GameId).First();

                foreach (var upd in updates)
                {
                    db.ManualGameScoreUpdates.Add(new ManualGameScoreUpdate
                    {
                        GuildId = Context.Guild.Id,
                        ManualGameId = game.GameId,
                        ModifyAmount = upd.Item2,
                        UserId = upd.Item1.UserId
                    });
                }

                embed.Description = sb.ToString();
                db.SaveChanges();
                //save scores
                await ReplyAsync(embed);
            }
        }
    }
}
