using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public partial class Info : ReactiveBase
    {
        public HttpClient HttpClient { get; }
        public CommandService CommandService { get; }
        public HelpService HelpService { get; }
        public GameService GameService { get; }
        public PermissionService PermissionService { get; }
        public PremiumService Premium { get; }

        public Info(HttpClient httpClient, CommandService commandService, HelpService helpService, GameService gameService, PermissionService permissionService, PremiumService premium)
        {
            HttpClient = httpClient;
            CommandService = commandService;
            HelpService = helpService;
            GameService = gameService;
            PermissionService = permissionService;
            Premium = premium;
        }

        [Command("Invite")]
        [Summary("Returns the bot invite")]
        public virtual async Task InviteAsync()
        {
            await SimpleEmbedAsync($"Invite: https://discordapp.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&scope=bot&permissions=8");
        }

        [Command("Help")]
        [Summary("Shows available commands based on the current user permissions")]
        public virtual async Task HelpAsync()
        {
            using (var db = new Database())
            {
                if (!PermissionService.PermissionCache.ContainsKey(Context.Guild.Id))
                {
                    var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                    var guildModel = new PermissionService.CachedPermissions
                    {
                        GuildId = Context.Guild.Id,
                        AdminId = comp.AdminRole,
                        ModId = comp.ModeratorRole
                    };

                    var permissions = db.Permissions.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                    foreach (var commandGroup in CommandService.Commands.GroupBy(x => x.Name.ToLower()))
                    {
                        var match = permissions.FirstOrDefault(x => x.CommandName.Equals(commandGroup.Key, StringComparison.OrdinalIgnoreCase));
                        if (match == null)
                        {
                            guildModel.Cache.Add(commandGroup.Key.ToLower(), null);
                        }
                        else
                        {
                            guildModel.Cache.Add(commandGroup.Key.ToLower(), new PermissionService.CachedPermissions.CachedPermission
                            {
                                CommandName = commandGroup.Key.ToLower(),
                                Level = match.Level
                            });
                        }
                    }

                    PermissionService.PermissionCache[Context.Guild.Id] = guildModel;
                }
            }
            await GenerateHelpAsync();
        }

        [Command("FullHelp")]
        [RequirePermission(PermissionLevel.Moderator)]
        [Summary("Displays all commands without checking permissions")]
        public virtual async Task FullHelpAsync()
        {
            await GenerateHelpAsync(false);
        }

        public virtual async Task GenerateHelpAsync(bool checkPreconditions = true)
        {
            try
            {
                var res = await HelpService.PagedHelpAsync(Context, checkPreconditions, null,
                "You can react with the :1234: emote and type a page number to go directly to that page too,\n" +
                "otherwise react with the arrows (◀ ▶) to change pages.\n");
                if (res != null)
                {
                    await PagedReplyAsync(res.ToCallBack().WithDefaultPagerCallbacks().WithJump());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [Command("Shards")]
        [Summary("Displays information about all shards")]
        public virtual async Task ShardInfoAsync()
        {
            var info = Context.Client.Shards.Select(x => $"[{x.ShardId}] {x.Status} {x.ConnectionState} - Guilds: {x.Guilds.Count} Users: {x.Guilds.Sum(g => g.MemberCount)}");
            await ReplyAsync($"```\n" + $"{string.Join("\n", info)}\n" + $"```");
        }

        [RateLimit(1, 1, Measure.Minutes, RateLimitFlags.ApplyPerGuild)]
        [Command("Stats")]
        [Summary("Bot Info and Stats")]
        public virtual async Task InformationAsync()
        {
            string changes;
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/PassiveModding/ELO/commits");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)");
            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                changes = "There was an error fetching the latest changes.";
            }
            else
            {
                dynamic result = JArray.Parse(await response.Content.ReadAsStringAsync());
                changes = $"[{((string)result[0].sha).Substring(0, 7)}]({result[0].html_url}) {result[0].commit.message}\n" + $"[{((string)result[1].sha).Substring(0, 7)}]({result[1].html_url}) {result[1].commit.message}\n" + $"[{((string)result[2].sha).Substring(0, 7)}]({result[2].html_url}) {result[2].commit.message}";
            }

            var embed = new EmbedBuilder();

            embed.WithAuthor(
                x =>
                {
                    x.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();
                    x.Name = $"{Context.Client.CurrentUser.Username}'s Official Invite";
                    x.Url = $"https://discordapp.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&scope=bot&permissions=2146958591";
                });
            embed.AddField("Changes", changes.FixLength());

            embed.AddField("Members", $"Bot: {Context.Client.Guilds.Sum(x => x.Users.Count(z => z.IsBot))}\nHuman: {Context.Client.Guilds.Sum(x => x.Users.Count(z => !z.IsBot))}\nPresent: {Context.Client.Guilds.Sum(x => x.Users.Count(u => u.Status != UserStatus.Offline))}", true);
            embed.AddField("Members", $"Online: {Context.Client.Guilds.Sum(x => x.Users.Count(z => z.Status == UserStatus.Online))}\nAFK: {Context.Client.Guilds.Sum(x => x.Users.Count(z => z.Status == UserStatus.Idle))}\nDND: {Context.Client.Guilds.Sum(x => x.Users.Count(u => u.Status == UserStatus.DoNotDisturb))}", true);
            embed.AddField("Channels", $"Text: {Context.Client.Guilds.Sum(x => x.TextChannels.Count)}\nVoice: {Context.Client.Guilds.Sum(x => x.VoiceChannels.Count)}\nTotal: {Context.Client.Guilds.Sum(x => x.Channels.Count)}", true);
            embed.AddField("Guilds", $"Count: {Context.Client.Guilds.Count}\nTotal Users: {Context.Client.Guilds.Sum(x => x.MemberCount)}\nTotal Cached: {Context.Client.Guilds.Sum(x => x.Users.Count())}\n", true);
            var orderedShards = Context.Client.Shards.OrderByDescending(x => x.Guilds.Count).ToList();
            embed.AddField("Shards", $"Shards: {Context.Client.Shards.Count}\nMax: G:{orderedShards.First().Guilds.Count} ID:{orderedShards.First().ShardId}\nMin: G:{orderedShards.Last().Guilds.Count} ID:{orderedShards.Last().ShardId}", true);
            embed.AddField("Commands", $"Commands: {CommandService.Commands.Count()}\nAliases: {CommandService.Commands.Sum(x => x.Attributes.Count)}\nModules: {CommandService.Modules.Count()}", true);
            embed.AddField(":hammer_pick:", $"Heap: {Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2)} MB\nUp: {(DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\D\ hh\H\ mm\M\ ss\S")}", true);
            embed.AddField(":beginner:", $"Written by: [PassiveModding](https://github.com/PassiveModding)\nDiscord.Net {DiscordConfig.Version}", true);

            await ReplyAsync("", false, embed.Build());
        }

        [Command("Ranks", RunMode = RunMode.Async)]
        [Summary("Displays information about the server's current ranks")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task ShowRanksAsync()
        {
            using (var db = new Database())
            {
                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToList();
                if (ranks.Count == 0)
                {
                    await SimpleEmbedAsync("There are currently no ranks set up.", Color.Blue);
                    return;
                }

                var msg = ranks.OrderByDescending(x => x.Points).Select(x => $"{MentionUtils.MentionRole(x.RoleId)} - ({x.Points}) W: (+{x.WinModifier ?? comp.DefaultWinModifier}) L: (-{x.LossModifier ?? comp.DefaultLossModifier})").ToArray();
                await SimpleEmbedAsync(string.Join("\n", msg), Color.Blue);
            }
        }

        [Command("Profile", RunMode = RunMode.Async)] // Please make default command name "Stats"
        [Alias("Info", "GetUser")]
        [Summary("Displays information about you or the specified user.")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task InfoAsync(SocketGuildUser user = null)
        {
            if (user == null)
            {
                user = Context.User as SocketGuildUser;
            }

            using (var db = new Database())
            {
                var player = db.Players.Find(Context.Guild.Id, user.Id);
                if (player == null)
                {
                    if (user.Id == Context.User.Id)
                    {
                        await SimpleEmbedAsync("You are not registered.", Color.DarkBlue);
                    }
                    else
                    {
                        await SimpleEmbedAsync("That user is not registered.", Color.Red);
                    }
                    return;
                }

                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToList();
                var maxRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
                string rankStr = null;
                if (maxRank != null)
                {
                    rankStr = $"Rank: {MentionUtils.MentionRole(maxRank.RoleId)} ({maxRank.Points})\n";
                }

                await SimpleEmbedAsync($"{player.GetDisplayNameSafe()} Stats\n" + // Use Title?
                            $"Points: {player.Points}\n" +
                            rankStr +
                            $"Wins: {player.Wins}\n" +
                            $"Losses: {player.Losses}\n" +
                            $"Draws: {player.Draws}\n" +
                            $"Games: {player.Games}\n" +
                            $"Registered At: {player.RegistrationDate.ToString("dd MMM yyyy")} {player.RegistrationDate.ToShortTimeString()}", Color.Blue);
            }

            //TODO: Add game history (last 5) to this response
            //+ if they were on the winning team?
            //maybe only games with a decided result should be shown?
        }

        [Command("Leaderboard", RunMode = RunMode.Async)]
        [Alias("lb", "top20")]
        [Summary("Shows the current server-wide leaderboard.")]
        [RequirePermission(PermissionLevel.Registered)]
        [RateLimit(1, 10, Measure.Seconds, RateLimitFlags.ApplyPerGuild | RateLimitFlags.NoLimitForAdmins)]
        public virtual async Task LeaderboardAsync(LeaderboardSortMode mode = LeaderboardSortMode.points)
        {
            using (var db = new Database())
            {
                //Retrieve players in the current guild from database
                var users = db.Players.AsNoTracking().Where(x => x.GuildId == Context.Guild.Id);

                //Order players by score and then split them into groups of 20 for pagination
                IEnumerable<Player>[] userGroups;
                switch (mode)
                {
                    case LeaderboardSortMode.point:
                        userGroups = users.OrderByDescending(x => x.Points).SplitList(20).ToArray();
                        break;
                    case LeaderboardSortMode.wins:
                        userGroups = users.OrderByDescending(x => x.Wins).SplitList(20).ToArray();
                        break;
                    case LeaderboardSortMode.losses:
                        userGroups = users.OrderByDescending(x => x.Losses).SplitList(20).ToArray();
                        break;
                    case LeaderboardSortMode.wlr:
                        userGroups = users.OrderByDescending(x => x.Losses == 0 ? x.Wins : (double)x.Wins / x.Losses).SplitList(20).ToArray();
                        break;
                    case LeaderboardSortMode.games:
                        userGroups = users.ToList().OrderByDescending(x => x.Games).SplitList(20).ToArray();
                        break;
                    default:
                        return;
                }
                if (userGroups.Length == 0)
                {
                    await SimpleEmbedAsync("There are no registered users in this server yet.", Color.Blue);
                    return;
                }

                //Convert the groups into formatted pages for the response message
                var pages = GetPages(userGroups, mode);

                if (!Premium.IsPremium(Context.Guild.Id))
                {
                    pages = pages.Take(1).ToList();
                    pages.Add(new ReactivePage
                    {
                        Description = $"In order to access a complete leaderboard, consider joining ELO premium at {Premium.PremiumConfig.AltLink}, patrons must also be members of the ELO server at: {Premium.PremiumConfig.ServerInvite}"
                    });
                }

                //Construct a paginated message with each of the leaderboard pages
                var callback = new ReactivePager(pages).ToCallBack();
                callback.Precondition = async (x, y) => y.UserId == Context.User.Id;
                await PagedReplyAsync(callback.WithDefaultPagerCallbacks().WithJump());
            }
        }

        public List<ReactivePage> GetPages(IEnumerable<Player>[] groups, LeaderboardSortMode mode)
        {
            //Start the index at 1 because we are ranking players here ie. first place.
            int index = 1;
            var pages = new List<ReactivePage>(groups.Length);
            foreach (var group in groups)
            {
                var playerGroup = group.ToArray();
                var lines = GetPlayerLines(playerGroup, index, mode);
                index = lines.Item1;
                var page = new ReactivePage();
                page.Color = Color.Blue;
                page.Title = $"{Context.Guild.Name} - Leaderboard";
                page.Description = lines.Item2;
                pages.Add(page);
            }

            return pages;
        }

        //Returns the updated index and the formatted player lines
        public (int, string) GetPlayerLines(Player[] players, int startValue, LeaderboardSortMode mode)
        {
            var sb = new StringBuilder();

            //Iterate through the players and add their summary line to the list.
            foreach (var player in players)
            {
                switch (mode)
                {
                    case LeaderboardSortMode.point:
                        sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{player.Points}`");
                        break;
                    case LeaderboardSortMode.wins:
                        sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{player.Wins}`");
                        break;
                    case LeaderboardSortMode.losses:
                        sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{player.Losses}`");
                        break;
                    case LeaderboardSortMode.wlr:
                        var wlr = player.Losses == 0 ? player.Wins : (double)player.Wins / player.Losses;
                        sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{Math.Round(wlr, 2, MidpointRounding.AwayFromZero)}`");
                        break;
                    case LeaderboardSortMode.games:
                        sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{player.Games}`");

                        break;
                }

                startValue++;
            }

            //Return the updated start value and the list of player lines.
            return (startValue, sb.ToString());
        }

        private CommandInfo Command { get; set; }
        protected override void BeforeExecute(CommandInfo command)
        {
            Command = command;
            base.BeforeExecute(command);
        }
    }
}
