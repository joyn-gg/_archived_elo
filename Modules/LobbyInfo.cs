using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    public partial class LobbyManagement
    {
        [Command("Maps", RunMode = RunMode.Async)]
        [Alias("MapList")]
        [Summary("Displays map information")]
        public async Task MapsAsync()
        {
            if (!await CheckLobbyAsync())
            {
                return;
            }
            string maps;
            if (CurrentLobby.MapSelector != null)
            {
                maps = $"\n**Map Mode:** {CurrentLobby.MapSelector.Mode}\n" +
                        $"**Maps:** {string.Join(", ", CurrentLobby.MapSelector.Maps)}\n" +
                        $"**Recent Maps:** {string.Join(", ", CurrentLobby.MapSelector.GetHistory())}";
            }
            else
            {
                maps = "N/A";
            }

            await SimpleEmbedAsync(maps);
        }

        [Command("Lobby", RunMode = RunMode.Async)]
        [Summary("Displays information about the current lobby.")]
        public async Task LobbyInfoAsync()
        {
            if (!await CheckLobbyAsync() || !await CheckRegisteredAsync())
            {
                return;
            }

            var embed = new EmbedBuilder
            {
                Color = Color.Blue
            };

            embed.AddField("Settings",
                $"**Players Per Team:** {CurrentLobby.PlayersPerTeam}\n" +
                $"**Pick Mode:** {CurrentLobby.TeamPickMode}\n" +
                $"{(CurrentLobby.MinimumPoints != null ? $"**Minimum Points to Queue:** {CurrentLobby.MinimumPoints}\n" : "")}");

            string maps;
            if (CurrentLobby.MapSelector != null)
            {
                var history = CurrentLobby.MapSelector.GetHistory();
                maps = $"\n**Map Mode:** {CurrentLobby.MapSelector.Mode}\n" +
                        $"**Maps:** {(CurrentLobby.MapSelector.Maps.Count == 0 ? "N/A" : string.Join(", ", CurrentLobby.MapSelector.Maps))}\n" +
                        $"**Recent Maps:** {(history.Count == 0 ? "N/A" : string.Join(", ", history))}";
            }
            else
            {
                maps = "N/A";
            }

            embed.AddField("Map Info", maps);

            embed.AddField("Info", $"**Games Played:** {CurrentLobby.CurrentGameCount}\n" +
                "For Players in Queue use the `Queue` or `Q` Command.");
            await ReplyAsync(embed);
        }

        [Command("Queue", RunMode = RunMode.Async)]
        [Alias("Q", "lps", "listplayers", "playerlist", "who")]
        [Summary("Displays information about the current queue or current game being picked.")]
        public async Task ShowQueueAsync()
        {
            if (!await CheckLobbyAsync())
            {
                return;
            }

            var game = Service.GetCurrentGame(CurrentLobby);
            if (game != null)
            {
                if (game.GameState == Models.GameResult.State.Picking)
                {
                    var gameEmbed = new EmbedBuilder
                    {
                        Title = $"Current Teams."
                    };

                    var t1Users = GetMentionList(GetUserList(Context.Guild, game.Team1.Players));
                    var t2Users = GetMentionList(GetUserList(Context.Guild, game.Team2.Players));
                    var remainingPlayers = GetMentionList(GetUserList(Context.Guild, game.Queue.Where(x => !game.Team1.Players.Contains(x) && !game.Team2.Players.Contains(x))));
                    gameEmbed.AddField("Team 1", $"Captain: {MentionUtils.MentionUser(game.Team1.Captain)}\n{string.Join("\n", t1Users)}");
                    gameEmbed.AddField("Team 2", $"Captain: {MentionUtils.MentionUser(game.Team2.Captain)}\n{string.Join("\n", t2Users)}");
                    gameEmbed.AddField("Remaining Players", string.Join("\n", remainingPlayers));
                    await ReplyAsync(gameEmbed);
                    return;
                }
            }

            if (CurrentLobby.Queue.Count > 0)
            {
                if (CurrentLobby.HideQueue)
                {
                    await Context.Message.DeleteAsync();
                    await SimpleEmbedAsync($"**[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**", Color.Blue);
                    return;
                }
                var mentionList = GetMentionList(GetUserList(Context.Guild, CurrentLobby.Queue));
                var embed = new EmbedBuilder();
                embed.Title = $"{Context.Channel.Name} [{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]";
                embed.Description = $"Game: #{CurrentLobby.CurrentGameCount + 1}\n" +
                    string.Join("\n", mentionList);
                await ReplyAsync(embed);
            }
            else
            {
                await SimpleEmbedAsync("The queue is empty.", Color.Blue);
            }
        }

        [Command("LobbyLeaderboard", RunMode = RunMode.Async)]
        [Summary("Displays a leaderboard with stats for the current lobby only.")]
        public async Task ShowLobbyLeaderboardAsync(ISocketMessageChannel channel = null)
        {
            if (channel == null)
            {
                channel = Context.Channel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAndDeleteAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            var lobbyGames = Service.GetGames(Context.Guild.Id, channel.Id);
            if (lobbyGames.Count() == 0)
            {
                await SimpleEmbedAsync("There have been no games played in the given lobby.", Color.Blue);
                return;
            }

            //userId, points, wins, losses, games
            var playerInfos = new Dictionary<ulong, (ulong, int, int, int, int)>();
            foreach (var game in lobbyGames.Where(x => x.GameState == Models.GameResult.State.Decided))
            {
                var winners = game.GetWinningTeam();
                var losers = game.GetLosingTeam();
                foreach (var player in winners.Item2.Players)
                {
                    if (game.ScoreUpdates.ContainsKey(player))
                    {
                        if (!playerInfos.TryGetValue(player, out var playerMatch))
                        {
                            playerMatch = (player, game.ScoreUpdates[player], 1, 0, 1);
                        }
                        else
                        {
                            playerMatch.Item2 += game.ScoreUpdates[player];
                            playerMatch.Item3++;
                            playerMatch.Item5++;
                        }

                        playerInfos[player] = playerMatch;
                    }
                }

                foreach (var player in losers.Item2.Players)
                {
                    if (game.ScoreUpdates.ContainsKey(player))
                    {
                        if (!playerInfos.TryGetValue(player, out var playerMatch))
                        {
                            playerMatch = (player, game.ScoreUpdates[player], 0, 1, 1);
                        }
                        else
                        {
                            playerMatch.Item2 += game.ScoreUpdates[player];
                            playerMatch.Item4++;
                            playerMatch.Item5++;
                        }

                        playerInfos[player] = playerMatch;
                    }
                }
            }

            var infos = playerInfos.OrderByDescending(x => x.Value.Item2).Select(x => x.Value);
            var groups = infos.SplitList(20).ToArray();
            var pages = GetPages(groups, channel);

            await PagedReplyAsync(new ReactivePager
            {
                Pages = pages
            }.ToCallBack().WithDefaultPagerCallbacks());
        }

        public List<ReactivePage> GetPages(IEnumerable<(ulong, int, int, int, int)>[] groups, ISocketMessageChannel channel)
        {
            //Start the index at 1 because we are ranking players here ie. first place.
            int index = 1;
            var pages = new List<ReactivePage>(groups.Length);
            foreach (var group in groups)
            {
                var playerGroup = group.ToArray();
                var lines = GetPlayerLines(playerGroup, index);
                index = lines.Item1;
                var page = new ReactivePage();
                page.Title = $"{channel.Name} - Leaderboard";
                page.Description = lines.Item2;
                pages.Add(page);
            }

            return pages;
        }

        //Returns the updated index and the formatted player lines
        public (int, string) GetPlayerLines((ulong, int, int, int, int)[] players, int startValue)
        {
            var sb = new StringBuilder();

            //Iterate through the players and add their summary line to the list.
            foreach (var player in players)
            {
                sb.AppendLine($"{startValue}: {MentionUtils.MentionUser(player.Item1)} - {player.Item2}");
                startValue++;
            }

            //Return the updated start value and the list of player lines.
            return (startValue, sb.ToString());
        }

    }
}