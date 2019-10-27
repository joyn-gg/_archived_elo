using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    public partial class Info
    {
        [Command("LastGame", RunMode = RunMode.Async)]
        [Alias("Last Game", "Latest Game", "LatestGame", "lg")]
        [Summary("Shows information about the most recent game in the current (or specified) lobby")]
        public async Task LastGameAsync(SocketGuildChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketGuildChannel;
            }
            //return the result of the last game if it can be found.
            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetCurrentGame(lobby);
            if (game == null)
            {
                await SimpleEmbedAsync("Latest game is not available.", Color.Red);
                return;
            }

            await DisplayGameAsync(game);
        }

        public async Task DisplayGameAsync(ManualGameResult game)
        {
            var embed = Service.GetGameEmbed(Context, game);

            await ReplyAsync(embed);
        }

        public async Task DisplayGameAsync(GameResult game)
        {
            var embed = Service.GetGameEmbed(Context, game);

            await ReplyAsync(embed);
        }

        [Command("GameInfo", RunMode = RunMode.Async)]
        [Alias("Game Info", "Show Game", "ShowGame", "sg")]
        [Summary("Shows information about the specified game in the current (or specified) lobby")]
        public async Task GameInfoAsync(int gameNumber, SocketGuildChannel lobbyChannel = null) //add functionality to specify lobby
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketGuildChannel;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                return;
            }

            var game = Service.GetGame(Context.Guild.Id, lobbyChannel.Id, gameNumber);
            if (game == null)
            {
                await SimpleEmbedAsync("Invalid Game Id.", Color.Red);
                return;
            }

            await DisplayGameAsync(game);
        }

        [Command("ManualGameInfo", RunMode = RunMode.Async)]
        [Alias("Manual Game Info", "Show Manual Game", "ShowManualGame", "smg")]
        [Summary("Shows information about the specified manual game")]
        public async Task ManualGameInfoAsync(int gameNumber)
        {
            var game = Service.GetManualGame(Context.Guild.Id, gameNumber);
            if (game == null)
            {
                await SimpleEmbedAsync("Specified game number is invalid.", Color.Red);
                return;
            }

            await DisplayGameAsync(game);
        }

        [Command("ManualGameList", RunMode = RunMode.Async)]
        [Alias("Manual Game List", "ManualGamesList", "ShowManualGames", "ListManualGames")]
        [Summary("Displays statuses for the last 100 manual games in the server")]
        public async Task ManualGameList()
        {
            var games = Service.GetManualGames(x => x.GuildId == Context.Guild.Id).OrderByDescending(x => x.GameId).Take(100);

            if (games.Count() == 0)
            {
                await SimpleEmbedAsync("There aren't any manual games in history.", Color.Blue);
                return;
            }

            var gamePages = games.SplitList(5);
            var pages = new List<ReactivePage>();
            foreach (var page in gamePages)
            {
                var content = page.Select(x =>
                {
                    if (x.ScoreUpdates.Count == 0) return null;

                    var scoreInfos = x.ScoreUpdates.Select(s =>
                        {
                            //TODO: reduce string construction nesting.
                            return $"{MentionUtils.MentionUser(s.Key)} {(s.Value >= 0 ? "+" + s.Value : s.Value.ToString())}";
                        });

                    if (x.GameState != ManualGameResult.ManualGameState.Legacy)
                    {
                        return new EmbedFieldBuilder()
                            .WithName($"#{x.GameId}: {x.GameState}")
                            .WithValue(string.Join("\n", scoreInfos) + $"\n **Submitted by: {MentionUtils.MentionUser(x.Submitter)}**");
                    }
                    else
                    {
                        //TODO: Is it necessary to check ALL users or maybe just first?
                        if (x.ScoreUpdates.All(val => val.Value >= 0))
                        {
                            return new EmbedFieldBuilder()
                                .WithName($"#{x.GameId}: Win")
                                .WithValue(string.Join("\n", scoreInfos) + $"\n **Submitted by: {MentionUtils.MentionUser(x.Submitter)}**");
                        }
                        else
                        {
                            return new EmbedFieldBuilder()
                                .WithName($"#{x.GameId}: Lose")
                                .WithValue(string.Join("\n", scoreInfos) + $"\n **Submitted by: {MentionUtils.MentionUser(x.Submitter)}**");
                        }
                    }
                }).Where(x => x != null).ToList();

                if (content.Count == 0) continue;

                pages.Add(new ReactivePage
                {
                    Fields = content
                });
            }

            await PagedReplyAsync(new ReactivePager(pages).ToCallBack().WithDefaultPagerCallbacks());
        }

        [Command("GameList", RunMode = RunMode.Async)]
        [Alias("Game List", "GamesList", "ShowGames", "ListGames")]
        [Summary("Displays statuses for the last 100 games in the lobby")]
        public async Task GameListAsync(SocketGuildChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketGuildChannel;
            }

            //TODO: Check if necessary to load the lobby (what are the benefits of response vs performance hit of query)
            var lobby = Service.GetLobby(Context.Guild.Id, lobbyChannel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                return;
            }

            var games = Service.GetGames(Context.Guild.Id, lobbyChannel.Id).OrderByDescending(x => x.GameId).Take(100);
            if (games.Count() == 0)
            {
                await SimpleEmbedAsync("There aren't any games in history for the specified lobby.", Color.Blue);
                return;
            }

            var gamePages = games.SplitList(20);
            var pages = new List<ReactivePage>();
            foreach (var page in gamePages)
            {
                var content = page.Select(x =>
                {
                    if (x.GameState == GameResult.State.Decided)
                    {
                        return $"`#{x.GameId}:` Team {x.WinningTeam}";
                    }
                    return $"`#{x.GameId}:` {x.GameState}";
                });
                pages.Add(new ReactivePage
                {
                    Description = string.Join("\n", content).FixLength(1023)
                });
            }

            await PagedReplyAsync(new ReactivePager(pages).ToCallBack().WithDefaultPagerCallbacks());
        }
    }
}