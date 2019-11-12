using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using RavenBOT.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                    await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var maps = db.Maps.Where(x => x.ChannelId == Context.Channel.Id).AsNoTracking().ToArray();
                if (maps.Length != 0)
                {
                    await SimpleEmbedAsync($"**Maps:** {string.Join(", ", maps.Select(x => x.MapName))}");
                }
                else
                {
                    await SimpleEmbedAsync("N/A");
                }
            }
        }

        [Command("Lobby", RunMode = RunMode.Async)]
        [Summary("Displays information about the current lobby.")]
        public virtual async Task LobbyInfoAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var embed = new EmbedBuilder
                {
                    Color = Color.Blue
                };

                embed.AddField("Settings",
                    $"**Players Per Team:** {lobby.PlayersPerTeam}\n" +
                    $"**Pick Mode:** {lobby.TeamPickMode}\n" +
                    $"{(lobby.MinimumPoints != null ? $"**Minimum Points to Queue:** {lobby.MinimumPoints}\n" : "")}");

                var maps = db.Maps.Where(x => x.ChannelId == Context.Channel.Id).AsNoTracking().ToArray();
                if (maps.Length != 0)
                {
                    embed.AddField("Maps", string.Join(", ", maps.Select(x => x.MapName)));
                }

                var maxGame = db.GetLatestGame(lobby);

                embed.AddField("Info", $"**Games Played:** {maxGame?.GameId}\n" +
                    "For Players in Queue use the `Queue` or `Q` Command.");
                await ReplyAsync(embed);
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
                    await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
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
                        var remainingPlayers = LobbyService.GetMentionList(LobbyService.GetUserList(Context.Guild, queue.Where(x => !team1.Any(y => y.UserId == x.UserId) && !team2.Any(y => y.UserId == x.UserId)).Select(x => x.UserId)));
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

                        await ReplyAsync(gameEmbed);
                        return;
                    }
                }

                var lobbyQueue = db.GetQueue(lobby).ToList();
                if (lobbyQueue.Count > 0)
                {
                    if (lobby.HideQueue)
                    {
                        await Context.Message.DeleteAsync();
                        await SimpleEmbedAsync($"**[{lobbyQueue.Count}/{lobby.PlayersPerTeam * 2}]**", Color.Blue);
                        return;
                    }

                    var embed = new EmbedBuilder
                    {
                        Color = Color.Blue
                    };
                    embed.Title = $"{Context.Channel.Name} [{lobbyQueue.Count}/{lobby.PlayersPerTeam * 2}]";
                    embed.Description = $"Game: #{(game?.GameId == null ? 0 : game.GameId) + 1}\n" +
                        string.Join("\n", lobbyQueue.Select(x => MentionUtils.MentionUser(x.UserId)));
                    await ReplyAsync(embed);
                }
                else
                {
                    await SimpleEmbedAsync("The queue is empty.", Color.Blue);
                }
            }
        }

        [Command("LobbyLeaderboard", RunMode = RunMode.Async)]
        [Summary("Displays a leaderboard with stats for the current lobby only.")]
        public virtual async Task ShowLobbyLeaderboardAsync(ISocketMessageChannel channel = null)
        {
            if (channel == null)
            {
                channel = Context.Channel;
            }

            if (!PremiumService.IsPremium(Context.Guild.Id))
            {
                await SimpleEmbedAsync($"This is a premium only command. " +
                    $"In order to get premium must become an ELO premium subscriber at {PremiumService.PremiumConfig.AltLink} join the server " +
                    $"{PremiumService.PremiumConfig.ServerInvite} to recieve your role and then run the `claimpremium` command in your server.");
                return;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(channel);
                if (lobby == null)
                {
                    await SimpleEmbedAndDeleteAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var updates = db.ScoreUpdates.AsNoTracking().Where(x => x.ChannelId == channel.Id).ToArray().GroupBy(x => x.UserId);
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

                await PagedReplyAsync(new ReactivePager
                {
                    Pages = pages
                }.ToCallBack().WithDefaultPagerCallbacks());
            }
        }
    }
}
