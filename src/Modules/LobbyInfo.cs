using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ELO.Extensions;
using ELO.Preconditions;
using ELO.Services.Reactive;

namespace ELO.Modules
{
    public partial class LobbyManagement
    {
        [Command("Maps", RunMode = RunMode.Async)]
        [Alias("MapList")]
        [Summary("Displays map information")]
        public virtual async Task MapsAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var maps = db.Maps.AsQueryable().Where(x => x.ChannelId == Context.Channel.Id).AsNoTracking().ToArray();
                if (maps.Length != 0)
                {
                    await Context.SimpleEmbedAsync($"**Maps:** {string.Join(", ", maps.Select(x => x.MapName))}");
                }
                else
                {
                    await Context.SimpleEmbedAsync("N/A");
                }
            }
        }

        [Command("Lobby", RunMode = RunMode.Async)]
        [Summary("Displays information about the current lobby.")]
        [RateLimit(1, 30, Measure.Seconds)]
        public virtual async Task LobbyInfoAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var embed = new EmbedBuilder
                {
                    Color = Color.Blue
                };

                embed.AddField("Settings",
                    $"**Players per team:** {lobby.PlayersPerTeam}\n" +
                    $"**Pick mode:** {lobby.TeamPickMode}\n" +
                    $"**Host selection mode:** {lobby.HostSelectionMode}\n" +
                    $"**Captain pick order:** {lobby.CaptainPickOrder}\n" +
                    $"**Hide queue:** {lobby.HideQueue}\n\n" +

                    $"**Minimum points to queue:** {(lobby.MinimumPoints.HasValue ? lobby.MinimumPoints.Value.ToString() : "N/A")}\n" +
                    $"**Max Points before reduction multiplier (high limit):** {(lobby.HighLimit.HasValue ? lobby.HighLimit.Value.ToString() : "N/A")}\n" +
                    $"**Reduction multiplier (high limit):** {lobby.ReductionPercent}\n\n" +

                    $"**Apply points multiplier to lost points:** {lobby.MultiplyLossValue}\n" +
                    $"**Points multiplier:** {lobby.LobbyMultiplier}\n\n" +

                    $"**DM users on game ready:** {lobby.DmUsersOnGameReady}\n" +
                    $"**Mention users in ready announcement:** {lobby.MentionUsersInReadyAnnouncement}\n" +
                    $"**Ready announcement channel:** " +
                    $"{(lobby.GameReadyAnnouncementChannel.HasValue ? MentionUtils.MentionChannel(lobby.GameReadyAnnouncementChannel.Value) : "N/A")}\n" +
                    $"**Result announcement channel:** " +
                    $"{(lobby.GameResultAnnouncementChannel.HasValue ? MentionUtils.MentionChannel(lobby.GameResultAnnouncementChannel.Value) : "N/A")}\n");

                var maps = db.Maps.AsQueryable().Where(x => x.ChannelId == Context.Channel.Id).AsNoTracking().ToArray();
                if (maps.Length != 0)
                {
                    embed.AddField("Maps", string.Join(", ", maps.Select(x => x.MapName)));
                }

                var maxGame = db.GetLatestGame(lobby);

                embed.AddField("Info", $"**Games Played:** {maxGame?.GameId}\n" +
                    "For Players in Queue use the `Queue` or `Q` Command.");
                await ReplyAsync("", false, embed.Build());
            }
        }

        [Command("Queue", RunMode = RunMode.Async)]
        [Alias("Q", "lps", "listplayers", "playerlist", "who")]
        [Summary("Displays information about the current queue or current game being picked.")]
        public virtual async Task ShowQueueAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GetLatestGame(lobby);
                if (game != null)
                {
                    if (game.GameState == GameState.Picking)
                    {
                        var team1 = db.GetTeam1(game).ToList();
                        var team2 = db.GetTeam2(game).ToList();
                        var gameEmbed = new EmbedBuilder
                        {
                            Title = $"Game: #{game.GameId} - Current Teams."
                        };

                        var t1Users = LobbyService.GetMentionList(LobbyService.GetUserList(Context.Guild, team1.Select(x => x.UserId)));
                        var t2Users = LobbyService.GetMentionList(LobbyService.GetUserList(Context.Guild, team2.Select(x => x.UserId)));
                        var t1c = db.GetTeamCaptain(Context.Guild.Id, Context.Channel.Id, game.GameId, 1);
                        var t2c = db.GetTeamCaptain(Context.Guild.Id, Context.Channel.Id, game.GameId, 2);
                        var queue = db.GetQueue(game);
                        var remainingIds = queue.Where(x =>
                            team1.All(y => y.UserId != x.UserId) &&
                            team2.All(y => y.UserId != x.UserId) &&
                            t1c.UserId != x.UserId &&
                            t2c.UserId != x.UserId).Select(x => x.UserId);
                        var remainingPlayers = LobbyService.GetMentionList(LobbyService.GetUserList(Context.Guild, remainingIds));
                        gameEmbed.AddField("Team 1", $"Captain: {MentionUtils.MentionUser(t1c.UserId)}\n{string.Join("\n", t1Users)}");
                        gameEmbed.AddField("Team 2", $"Captain: {MentionUtils.MentionUser(t2c.UserId)}\n{string.Join("\n", t2Users)}");
                        if (remainingPlayers.Any())
                        {
                            gameEmbed.AddField("Remaining Players", string.Join("\n", remainingPlayers));
                        }

                        var teamCaptain = game.Picks % 2 == 0 ? t1c : t2c;
                        if (game.PickOrder == CaptainPickOrder.PickOne)
                        {
                            gameEmbed.AddField("Captain Currently Picking", $"{MentionUtils.MentionUser(teamCaptain.UserId)} can pick **1** player for this pick.");
                        }
                        else if (game.PickOrder == CaptainPickOrder.PickTwo)
                        {
                            if (game.Picks == 1 || game.Picks == 2)
                            {
                                gameEmbed.AddField("Captain Currently Picking", $"{MentionUtils.MentionUser(teamCaptain.UserId)} can pick **2** players for this pick.");
                            }
                            else
                            {
                                gameEmbed.AddField("Captain Currently Picking", $"{MentionUtils.MentionUser(teamCaptain.UserId)} can pick **1** player for this pick.");
                            }
                        }

                        await ReplyAsync("", false, gameEmbed.Build());
                        return;
                    }
                }

                var lobbyQueue = db.GetQueue(lobby).ToList();
                if (lobbyQueue.Count > 0)
                {
                    if (lobby.HideQueue)
                    {
                        await Context.Message.DeleteAsync();
                        await Context.SimpleEmbedAsync($"**[{lobbyQueue.Count}/{lobby.PlayersPerTeam * 2}]**", Color.Blue);
                        return;
                    }

                    var embed = new EmbedBuilder
                    {
                        Color = Color.Blue
                    };
                    embed.Title = $"{Context.Channel.Name} [{lobbyQueue.Count}/{lobby.PlayersPerTeam * 2}]";
                    embed.Description = $"Game: #{(game?.GameId ?? 0) + 1}\n" +
                        string.Join("\n", lobbyQueue.Select(x => MentionUtils.MentionUser(x.UserId)));
                    await ReplyAsync("", false, embed.Build());
                }
                else
                {
                    await Context.SimpleEmbedAsync("The queue is empty.", Color.Blue);
                }
            }
        }

        [Command("LobbyLeaderboard", RunMode = RunMode.Async)]
        [Summary("Displays a leaderboard with stats for the current lobby only.")]
        [RateLimit(1, 10, Measure.Seconds, RateLimitFlags.ApplyPerGuild)]
        public virtual async Task ShowLobbyLeaderboardAsync(ISocketMessageChannel channel = null)
        {
            if (channel == null)
            {
                channel = Context.Channel;
            }

            if (!PremiumService.IsPremium(Context.Guild.Id))
            {
                await Context.SimpleEmbedAsync($"This is a premium only command. " +
                    $"In order to get premium must become an ELO premium subscriber at {PremiumService.PremiumConfig.AltLink} join the server " +
                    $"{PremiumService.PremiumConfig.ServerInvite} to recieve your role and then run the `claimpremium` command in your server.");
                return;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAndDeleteAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var updates = db.ScoreUpdates.AsNoTracking().AsQueryable().Where(x => x.ChannelId == channel.Id).ToArray().GroupBy(x => x.UserId);
                var infos = new Dictionary<ulong, int>();
                foreach (var group in updates)
                {
                    infos[group.Key] = group.Sum(x => x.ModifyAmount);
                }

                var groups = infos.OrderByDescending(x => x.Value).SplitList(20).ToArray();
                int index = 1;
                var pages = new List<ReactivePage>();
                foreach (var group in groups)
                {
                    var playerGroup = group.ToArray();
                    var lines = group.Select(x => $"{index++}: {MentionUtils.MentionUser(x.Key)} - `{x.Value}`").ToArray();

                    //index += lines.Length;
                    var page = new ReactivePage();
                    page.Color = Color.Blue;
                    page.Title = $"{channel.Name} - Leaderboard";
                    page.Description = string.Join("\n", lines);
                    pages.Add(page);
                }

                await _reactive.SendPagedMessageAsync(Context, Context.Channel, new ReactivePager
                {
                    Pages = pages
                }.ToCallBack().WithDefaultPagerCallbacks().WithJump());
            }
        }
    }
}