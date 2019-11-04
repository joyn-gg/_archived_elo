using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Preconditions;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    [Preconditions.RequirePermission(PermissionLevel.Moderator)]
    public class ScoreManagement : ReactiveBase
    {
        public ScoreManagement(UserService userService)
        {
            UserService = userService;
        }

        [Command("ModifyStates", RunMode = RunMode.Async)]
        [Summary("Shows modifier values for score management commands")]
        public async Task ModifyStatesAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", RavenBOT.Common.Extensions.EnumNames<ModifyState>()), Color.Blue);
        }

        //TODO: Consider whether it's necessary to have the single user command as multi user already is able to accept only one.
        [Command("Points", RunMode = RunMode.Sync)]
        [Summary("Modifies points for the specified user")]
        public async Task PointsAsync(SocketGuildUser user, ModifyState state, int amount)
        {
            await PointsAsync(state, amount, user);
        }

        [Command("Points", RunMode = RunMode.Sync)]
        [Summary("Modifies points for the specified users.")]
        public async Task PointsAsync(ModifyState state, int amount, params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var userIds = users.Select(x => x.Id).ToArray();
                var players = db.Players.Where(x => x.GuildId == Context.Guild.Id && userIds.Contains(x.UserId));
                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                var responseString = "";
                foreach (var player in players)
                {
                    var original = player.Points;
                    if (state == ModifyState.Set)
                    {
                        player.Points = amount;
                    }
                    else if (state == ModifyState.Modify)
                    {
                        player.Points += amount;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown modify state");
                    }

                    responseString += $"{player.GetDisplayNameSafe()}: {original} => {player.Points}\n";
                    var gUser = users.First(x => x.Id == player.UserId);
                    await UserService.UpdateUserAsync(comp, player, ranks, gUser);
                }
                db.UpdateRange(players);
                db.SaveChanges();
                await SimpleEmbedAsync(responseString, Color.Blue);
            }
        }

        [Command("Wins", RunMode = RunMode.Sync)]
        [Summary("Modifies wins for the specified user.")]
        public async Task WinsAsync(SocketGuildUser user, ModifyState state, int amount)
        {
            await WinsAsync(state, amount, user);
        }

        [Command("Wins", RunMode = RunMode.Sync)]
        [Summary("Modifies wins for the specified users.")]
        public async Task WinsAsync(ModifyState state, int amount, params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var userIds = users.Select(x => x.Id).ToArray();
                var players = db.Players.Where(x => x.GuildId == Context.Guild.Id && userIds.Contains(x.UserId));
                var responseString = "";
                foreach (var player in players)
                {
                    var original = player.Wins;
                    if (state == ModifyState.Set)
                    {
                        player.Wins = amount;
                    }
                    else if (state == ModifyState.Modify)
                    {
                        player.Wins += amount;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown modify state");
                    }

                    responseString += $"{player.GetDisplayNameSafe()}: {original} => {player.Wins}\n";
                }
                db.UpdateRange(players);
                db.SaveChanges();
                await SimpleEmbedAsync(responseString, Color.Blue);
            }
        }

        [Command("Losses", RunMode = RunMode.Sync)]
        [Summary("Modifies losses for the specified user.")]
        public async Task LossesAsync(SocketGuildUser user, ModifyState state, int amount)
        {
            await LossesAsync(state, amount, user);
        }

        [Command("Losses", RunMode = RunMode.Sync)]
        [Summary("Modifies losses for the specified users.")]
        public async Task LossesAsync(ModifyState state, int amount, params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var userIds = users.Select(x => x.Id).ToArray();
                var players = db.Players.Where(x => x.GuildId == Context.Guild.Id && userIds.Contains(x.UserId));
                var responseString = "";
                foreach (var player in players)
                {
                    var original = player.Losses;
                    if (state == ModifyState.Set)
                    {
                        player.Losses = amount;
                    }
                    else if (state == ModifyState.Modify)
                    {
                        player.Losses += amount;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown modify state");
                    }

                    responseString += $"{player.GetDisplayNameSafe()}: {original} => {player.Losses}\n";
                }
                db.UpdateRange(players);
                db.SaveChanges();
                await SimpleEmbedAsync(responseString, Color.Blue);
            }
        }

        [Command("Draws", RunMode = RunMode.Sync)]
        [Summary("Modifies draws for the specified user.")]
        public async Task DrawsAsync(SocketGuildUser user, ModifyState state, int amount)
        {
            await DrawsAsync(state, amount, user);
        }

        [Command("Draws", RunMode = RunMode.Sync)]
        [Summary("Modifies draws for the specified users.")]
        public async Task DrawsAsync(ModifyState state, int amount, params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var userIds = users.Select(x => x.Id).ToArray();
                var players = db.Players.Where(x => x.GuildId == Context.Guild.Id && userIds.Contains(x.UserId));
                var responseString = "";
                foreach (var player in players)
                {
                    var original = player.Draws;
                    if (state == ModifyState.Set)
                    {
                        player.Draws = amount;
                    }
                    else if (state == ModifyState.Modify)
                    {
                        player.Draws += amount;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown modify state");
                    }

                    responseString += $"{player.GetDisplayNameSafe()}: {original} => {player.Draws}\n";
                }
                db.UpdateRange(players);
                db.SaveChanges();
                await SimpleEmbedAsync(responseString, Color.Blue);
            }
        }

        public static List<ulong> RenameTasks { get; set; } = new List<ulong>();
        public UserService UserService { get; }

        [Command("ResetLeaderboard", RunMode = RunMode.Sync)]
        [Summary("Resets the leaderboard")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public async Task ResetLeaderboard()
        {
            using (var db = new Database())
            {
                await SimpleEmbedAsync($"Resetting leaderboard...", Color.Green);
                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                var players = db.Players.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                foreach (var player in players)
                {
                    player.Points = comp.DefaultRegisterScore;
                    player.Draws = 0;
                    player.Wins = 0;
                    player.Losses = 0;
                }
                db.UpdateRange(players);
                db.SaveChanges();
                await SimpleEmbedAsync($"Leaderboard reset complete.", Color.Green);
            }
        }

        [Command("RefreshUsers", RunMode = RunMode.Async)]
        [Summary("Refreshes all user names and roles")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        [RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task RefreshNamesAsync()
        {
            using (var db = new Database())
            {
                try
                {
                    if (RenameTasks.Contains(Context.Guild.Id))
                    {
                        await SimpleEmbedAsync($"There is currently a refresh task for this server running.", Color.Red);
                        return;
                    }
                    RenameTasks.Add(Context.Guild.Id);

                    await SimpleEmbedAsync($"Running refresh task... Estimated time: {TimeSpan.FromSeconds(Context.Guild.MemberCount * 2).GetReadableLength()}", Color.Green);
                    var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                    var players = db.Players.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                    var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();

                    foreach (var player in players)
                    {
                        var user = Context.Guild.GetUser(player.UserId);
                        if (user != null)
                        {
                            var _ = Task.Run(async () =>
                            {
                                await UserService.UpdateUserAsync(comp, player, ranks, user);
                            });
                            //2 sec per rename? should be fine
                            await Task.Delay(2000);
                        }
                    }
                    await SimpleEmbedAsync($"Refresh task complete.", Color.Green);
                }
                finally
                {
                    RenameTasks.Remove(Context.Guild.Id);
                }
            }
        }
    }
}
