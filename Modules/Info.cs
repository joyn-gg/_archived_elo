using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using Discord;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public partial class Info : ReactiveBase
    {
        public ELOService Service { get; }

        public Info(ELOService service)
        {
            Service = service;
        }

        [Command("Ranks", RunMode = RunMode.Async)]
        [Summary("Displays information about the server's current ranks")]
        public async Task ShowRanksAsync()
        {
            var comp = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (!comp.Ranks.Any())
            {
                await ReplyAsync("There are currently no ranks set up.");
                return;
            }

            var msg = comp.Ranks.OrderByDescending(x => x.Points).Select(x => $"{MentionUtils.MentionRole(x.RoleId)} - ({x.Points}) W: (+{x.WinModifier ?? comp.DefaultWinModifier}) L: (-{x.LossModifier ?? comp.DefaultLossModifier})").ToArray();
            await ReplyAsync("", false, string.Join("\n", msg).QuickEmbed());
        }

        [Command("Profile", RunMode = RunMode.Async)] // Please make default command name "Stats"
        [Alias("Info", "GetUser")]
        [Summary("Displays information about you or the specified user.")]
        public async Task InfoAsync(SocketGuildUser user = null)    
        {
            if (user == null)
            {
                user = Context.User as SocketGuildUser;
            }

            var player = Service.GetPlayer(Context.Guild.Id, user.Id);
            if (player == null)
            {
                if (user.Id == Context.User.Id)
                {
                    await ReplyAsync("You are not registered.");
                }
                else
                {
                    await ReplyAsync("That user is not registered.");
                }
                return;
            }
            
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var rank = competition.MaxRank(player.Points);
            string rankStr = null;
            if (rank != null)
            {
                rankStr = $"Rank: {MentionUtils.MentionRole(rank.RoleId)} ({rank.Points})\n";
            }

            var response = $"{player.GetDisplayNameSafe()} Stats\n" + // Use Title?
                            $"Points: {player.Points}\n"+
                            rankStr +
                            $"Wins: {player.Wins}\n"+
                            $"Losses: {player.Losses}\n"+
                            $"Draws: {player.Draws}\n"+
                            $"Games: {player.Games}\n"+
                            $"Registered At: {player.RegistrationDate.ToString("dd MMM yyyy")} {player.RegistrationDate.ToShortTimeString()}\n"+
                            $"{string.Join("\n", player.AdditionalProperties.Select(x => $"{x.Key}: {x.Value}"))}";

                            //TODO: Add game history (last 5) to this response
                            //+ if they were on the winning team?
                            //maybe only games with a decided result should be shown?

            await ReplyAsync("", false, response.QuickEmbed());
        }

        [Command("Leaderboard", RunMode = RunMode.Async)]
        [Alias("lb", "top20")]
        [Summary("Shows the current server-wide leaderboard.")]
        //TODO: Ratelimiting as this is a data heavy command.
        public async Task LeaderboardAsync()
        {
            //TODO: Implement sort modes

            //Retrieve players in the current guild from database
            var users = Service.GetPlayers(x => x.GuildId == Context.Guild.Id);

            //Order players by score and then split them into groups of 20 for pagination
            var userGroups = users.OrderByDescending(x => x.Points).SplitList(20).ToArray();
            if (userGroups.Length == 0)
            {
                await ReplyAsync("There are no registered users in this server yet.");
                return;
            }

            //Convert the groups into formatted pages for the response message
            var pages = GetPages(userGroups);

            //Construct a paginated message with each of the leaderboard pages
            await PagedReplyAsync(new ReactivePager(pages).ToCallBack().WithDefaultPagerCallbacks());
        }

        public List<ReactivePage> GetPages(IEnumerable<Player>[] groups)
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
                page.Title = $"{Context.Guild.Name} - Leaderboard";
                page.Description = lines.Item2;
                pages.Add(page);
            }

            return pages;
        }

        //Returns the updated index and the formatted player lines
        public (int, string) GetPlayerLines(Player[] players, int startValue)
        {
            var sb = new StringBuilder();
            
            //Iterate through the players and add their summary line to the list.
            foreach (var player in players)
            {
                sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - {player.Points}");
                startValue++;
            }

            //Return the updated start value and the list of player lines.
            return (startValue, sb.ToString());
        }
    }
}