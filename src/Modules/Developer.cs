using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Preconditions;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using MoreLinq;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ELO.Models;
using Patreon.NET;

namespace ELO.Modules
{
    [DevWhitelist]
    [Group("devcmd")]
    public class Developer : ReactiveBase
    {
        public Developer(Random random, PremiumService prem, CommandService cmd)
        {
            Random = random;
            Prem = prem;
            Cmd = cmd;
        }

        public Random Random { get; }

        public PremiumService Prem { get; }

        public CommandService Cmd { get; }

        [Command("LeaveGuild")]
        public async Task LeaveGuildAsync(ulong guildId)
        {
            var match = Context.Client.GetGuild(guildId);
            if (match == null)
            {
                await ReplyAsync("Guild not found");
                return;
            }

            await match.LeaveAsync();
            await ReplyAsync($"Guild {match.Name} has been left [{match.Id}]");
        }

        [Command("GuildInfo")]
        public async Task GuildInfoAsync(ulong guildId)
        {
            var match = Context.Client.GetGuild(guildId);
            if (match == null)
            {
                await ReplyAsync("Guild not found");
                return;
            }

            using (var db = new Database())
            {
                var comp = db.Competitions.FirstOrDefault(x => x.GuildId == match.Id);
                var memberCount = match.MemberCount;
                var owner = match.OwnerId;

                var inv = await GetInviteAsync(match.Id);

                var embed = new EmbedBuilder();

                embed.AddField("Info",
                    $"Member Count: {memberCount}\n" +
                    $"Owner: {Discord.MentionUtils.MentionUser(owner)} {match.Owner?.Username}#{match.Owner?.Discriminator} {match.OwnerId}\n" +
                    $"Invite: {inv?.Url ?? "N/A"}");

                if (comp != null)
                {
                    var registered = db.Players.Count(x => x.GuildId == Context.Guild.Id);
                    var gameCount = db.GameResults.Count(x => x.GuildId == Context.Guild.Id);
                    var decGameCount = db.GameResults.Count(x => x.GuildId == Context.Guild.Id && (x.GameState == GameState.Decided || x.GameState == GameState.Draw));
                    var manualGameCount = db.ManualGameResults.Count(x => x.GuildId == Context.Guild.Id);

                    embed.AddField("Roles",
                                $"**Register Role:** {(comp.RegisteredRankId == null ? "N/A" : MentionUtils.MentionRole(comp.RegisteredRankId.Value))}\n" +
                                $"**Admin Role:** {(comp.AdminRole == null ? "N/A" : MentionUtils.MentionRole(comp.AdminRole.Value))}\n" +
                                $"**Moderator Role:** {(comp.ModeratorRole == null ? "N/A" : MentionUtils.MentionRole(comp.ModeratorRole.Value))}");
                    embed.AddField("Options",
                                $"**Allow Multi-Queuing:** {comp.AllowMultiQueueing}\n" +
                                $"**Allow Negative Score:** {comp.AllowNegativeScore}\n" +
                                $"**Update Nicknames:** {comp.UpdateNames}\n" +
                                $"**Display Error Messages:** {comp.DisplayErrors}\n" +
                                $"**Allow Self Rename:** {comp.AllowSelfRename}\n" +
                                $"**Allow Re-registering:** {comp.AllowReRegister}\n" +
                                $"**Requeue Delay:** {(comp.RequeueDelay.HasValue ? comp.RequeueDelay.Value.GetReadableLength() : "None")}\n" +
                                $"**Voting Enabled:** {comp.AllowVoting}\n" +
                                $"**Custom Prefix:** {comp.Prefix ?? "N/A"}\n" +
                                $"**Auto Queue Timeout:** {(comp.QueueTimeout.HasValue ? comp.QueueTimeout.Value.GetReadableLength() : "None")}");

                    embed.AddField("Premium",
                                $"**Premium Buffer Until:** " +
                                $"{(comp.PremiumBuffer.HasValue ? comp.PremiumBuffer.Value.ToShortDateString() + " " + comp.PremiumBuffer.Value.ToShortTimeString() : "N/A")}\n" +
                                $"**Premium Buffer Count:** {(comp.BufferedPremiumCount.HasValue ? comp.BufferedPremiumCount.Value.ToString() : "N/A")}\n" +
                                $"**Premium Redeemer:** {(comp.PremiumRedeemer.HasValue ? MentionUtils.MentionUser(comp.PremiumRedeemer.Value) : "N/A")}\n" +
                                $"**Registration Limit:** {Prem.GetRegistrationLimit(Context.Guild.Id)}");

                    embed.AddField("Stats",
                        $"**Registrations:** {registered}\n" +
                        $"**Games Created:** {gameCount}\n" +
                        $"**Games Submitted:** {decGameCount}\n" +
                        $"**Manual Games:** {manualGameCount}");

                    embed.AddField("Formatting", $"**Nickname Format:** {comp.NameFormat}\n" +
                                $"**Registration Message:** {comp.RegisterMessageTemplate.FixLength(128)}");

                    embed.AddField("Rank Info",
                    $"**Default Win Amount:** +{comp.DefaultWinModifier}\n" +
                    $"**Default Loss Amount:** -{comp.DefaultLossModifier}\n" +
                    $"**Default Points On Register:** {comp.DefaultRegisterScore}");
                }

                await ReplyAsync("", false, embed.Build());
            }
        }

        private async Task<IInvite> GetInviteAsync(ulong guildId)
        {
            var guild = Context.Client.GetGuild(guildId);
            if (guild == null)
            {
                return null;
            }

            foreach (var invite in await guild.GetInvitesAsync())
            {
                if (invite.IsRevoked) continue;
                return invite;
            }

            try
            {
                var vanity = await guild.GetVanityInviteAsync();
                return vanity;
            }
            catch (Exception e)
            {
                //
            }

            foreach (var channel in guild.TextChannels)
            {
                try
                {
                    var tmp = await channel.CreateInviteAsync();
                    return tmp;
                }
                catch (Exception e)
                {
                    //
                }
            }

            foreach (var channel in guild.VoiceChannels)
            {
                try
                {
                    var tmp = await channel.CreateInviteAsync();
                    return tmp;
                }
                catch (Exception e)
                {
                    //
                }
            }

            return null;
        }

        private PatreonClient.PledgesInfo cacheInfo = null;

        [Command("AnalyzePatronsCache")]
        public async Task PatreonTestCachedAsync(string apiKey = null, string campaignId = null)
        {
            if (apiKey == null || campaignId == null)
            {
                await ReplyAsync("Must supply api key and campaign ID when running this command.");
                return;
            }

            await ReplyAsync("Pulling patreon info.");
            var guild = Context.Client.GetGuild(Prem.PremiumConfig.GuildId);
            if (cacheInfo == null)
            {
                var client = new Patreon.NET.PatreonClient(apiKey);
                var pledges = await client.GetCampaignPledges(campaignId);
                cacheInfo = pledges;
            }

            var rewardMissing = cacheInfo.Users.Count(x => x.Rewards == null);
            var userMissing = cacheInfo.Users.Count(x => x.UserIncluded == null);

            int uidMissing = 0;
            var premiumUsers = new Dictionary<ulong, HashSet<ulong>>();

            PremiumService.PremiumRole[] roles;
            await using (var db = new Database())
            {
                roles = db.PremiumRoles.ToArray();
            }

            foreach (var pledge in cacheInfo.Users)
            {
                var userId = pledge.UserIncluded.Attributes.SocialConnections.Discord?.UserId;
                if (userId == null)
                {
                    uidMissing++;
                }
                else
                {
                    var roleIds = pledge.Rewards.SelectMany(x => x.Attributes.DiscordRoleIds);
                    var entitledRoles = new HashSet<ulong>();
                    foreach (var role in roleIds)
                    {
                        entitledRoles.Add(ulong.Parse(role));
                    }

                    var uid = ulong.Parse(userId);

                    if (premiumUsers.TryGetValue(uid, out var uCol))
                    {
                        foreach (var entitledRoleId in entitledRoles)
                        {
                            uCol.Add(entitledRoleId);
                        }

                        entitledRoles = uCol;
                    }
                    else
                    {
                        premiumUsers.Add(uid, entitledRoles);
                    }
                }
            }

            var contentStrings = new List<string>();
            int maxLength = 2000;
            var builder = new StringBuilder();
            foreach (var premiumUser in premiumUsers)
            {
                var gUser = guild.GetUser(premiumUser.Key);
                if (gUser != null)
                {
                    var userPremiumRoles = gUser.Roles.Where(x => roles.Any(y => y.RoleId == x.Id)).ToArray();
                    /*if (userPremiumRoles.Length > 0)
                    {
                        // User has premium but wrong roles.
                        if (userPremiumRoles.Any(r => !premiumUser.Value.Contains(r.Id)))
                        {
                            builder.AppendLine($"{gUser.Mention} `{gUser.Username}#{gUser.Discriminator}` has role when only entitled to {string.Join(" ", premiumUser.Value.Select(x => "<@&" + x + ">"))}");
                        }
                    }*/

                    if (premiumUser.Value.Count > 0 && userPremiumRoles.Length == 0)
                    {
                        var content =
                            $"{gUser.Mention} `{gUser.Username}#{gUser.Discriminator}` has no roles but is entitled to {string.Join(" ", premiumUser.Value.Select(x => "<@&" + x + ">"))}";
                        if (builder.Length + content.Length > maxLength)
                        {
                            contentStrings.Add(builder.ToString());
                            builder.Clear();
                        }

                        builder.AppendLine(content);
                    }
                }
            }

            var gUsersWithPremiumRoles = guild.Users.Where(u => u.Roles.Any(r => roles.Any(y => y.RoleId == r.Id))).ToArray();

            // Iterate through all users in the server which have a premium role
            foreach (var user in gUsersWithPremiumRoles)
            {
                // Get roles from the user which are in the bot's premium role list
                var uPremiumRoles = user.Roles.Where(x => roles.Any(r => r.RoleId == x.Id)).ToArray();

                // Find the list of roles which the user is entitled to.
                var uEntitledRoles = premiumUsers.FirstOrDefault(x => x.Key == user.Id).Value;
                if (uPremiumRoles.Length > 0)
                {
                    if (uEntitledRoles == null)
                    {
                        // User has prem roles but no prem
                        var content =
                            $"{user.GetDisplayName()} {user.Mention} has premium roles but no premium";
                        if (builder.Length + content.Length > maxLength)
                        {
                            contentStrings.Add(builder.ToString());
                            builder.Clear();
                        }

                        builder.AppendLine(content);
                    }
                    else
                    {
                        // Find users discord roles which are not contained in the entitled roles list.
                        var notEntitled = uPremiumRoles.Where(r => !uEntitledRoles.Contains(r.Id)).ToArray();
                        if (notEntitled.Length > 0)
                        {
                            // User has prem roles but not entitled to them.
                            var content =
                                $"{user.GetDisplayName()} {user.Mention} has premium roles that they are not entitled to {string.Join(" ", notEntitled.Select(x => x.Mention))}";
                            if (builder.Length + content.Length > maxLength)
                            {
                                contentStrings.Add(builder.ToString());
                                builder.Clear();
                            }

                            builder.AppendLine(content);
                        }
                    }
                }
            }

            contentStrings.Add(builder.ToString());

            foreach (var contentString in contentStrings)
            {
                if (contentString.Length > 0)
                {
                    await ReplyAsync(contentString.FixLength(2000));
                }
            }
        }

        [Command("AnalyzePatrons")]
        public async Task PatreonTestAsync(string apiKey = null, string campaignId = null)
        {
            if (apiKey == null || campaignId == null)
            {
                await ReplyAsync("Must supply api key and campaign ID when running this command.");
                return;
            }

            await ReplyAsync("Pulling patreon info.");
            var guild = Context.Client.GetGuild(Prem.PremiumConfig.GuildId);
            var client = new Patreon.NET.PatreonClient(apiKey);
            var pledges = await client.GetCampaignPledges(campaignId);

            var rewardMissing = pledges.Users.Count(x => x.Rewards == null);
            var userMissing = pledges.Users.Count(x => x.UserIncluded == null);

            int uidMissing = 0;
            var premiumUsers = new Dictionary<ulong, HashSet<ulong>>();

            PremiumService.PremiumRole[] roles;
            await using (var db = new Database())
            {
                roles = db.PremiumRoles.ToArray();
            }

            foreach (var pledge in pledges.Users)
            {
                var userId = pledge.UserIncluded.Attributes.SocialConnections.Discord?.UserId;
                if (userId == null)
                {
                    uidMissing++;
                }
                else
                {
                    var roleIds = pledge.Rewards.SelectMany(x => x.Attributes.DiscordRoleIds);
                    var entitledRoles = new HashSet<ulong>();
                    foreach (var role in roleIds)
                    {
                        entitledRoles.Add(ulong.Parse(role));
                    }

                    var uid = ulong.Parse(userId);

                    if (premiumUsers.TryGetValue(uid, out var uCol))
                    {
                        foreach (var entitledRoleId in entitledRoles)
                        {
                            uCol.Add(entitledRoleId);
                        }

                        entitledRoles = uCol;
                    }
                    else
                    {
                        premiumUsers.Add(uid, entitledRoles);
                    }
                }
            }

            foreach (var premiumUser in premiumUsers)
            {
                var gUser = guild.GetUser(premiumUser.Key);
                if (gUser != null)
                {
                    var userPremiumRoles = gUser.Roles.Where(x => roles.Any(y => y.RoleId == x.Id)).ToArray();
                    if (userPremiumRoles.Length > 0)
                    {
                        // User has premium but wrong roles.
                        if (userPremiumRoles.Any(r => !premiumUser.Value.Contains(r.Id)))
                        {
                            Console.WriteLine($"{gUser.GetDisplayName()} has role when only entitled to {string.Join(" ", premiumUser.Value.Select(x => "<@&" + x + ">"))}");
                        }
                    }

                    if (premiumUser.Value.Count > 0 && userPremiumRoles.Length == 0)
                    {
                        Console.WriteLine($"{gUser.GetDisplayName()} has no roles but is entitled to {string.Join(" ", premiumUser.Value.Select(x => "<@&" + x + ">"))}");
                    }
                }
            }

            var gUsersWithPremiumRoles = guild.Users.Where(u => u.Roles.Any(r => roles.Any(y => y.RoleId == r.Id))).ToArray();
            var builder = new StringBuilder();

            // Iterate through all users in the server which have a premium role
            foreach (var user in gUsersWithPremiumRoles)
            {
                // Get roles from the user which are in the bot's premium role list
                var uPremiumRoles = user.Roles.Where(x => roles.Any(r => r.RoleId == x.Id)).ToArray();

                // Find the list of roles which the user is entitled to.
                var uEntitledRoles = premiumUsers.FirstOrDefault(x => x.Key == user.Id).Value;
                if (uPremiumRoles.Length > 0)
                {
                    if (uEntitledRoles == null)
                    {
                        // User has prem roles but no prem
                        builder.AppendLine($"{user.GetDisplayName()} {user.Mention} has premium roles but no premium");
                    }
                    else
                    {
                        // Find users discord roles which are not contained in the entitled roles list.
                        var notEntitled = uPremiumRoles.Where(r => !uEntitledRoles.Contains(r.Id)).ToArray();
                        if (notEntitled.Length > 0)
                        {
                            // User has prem roles but not entitled to them.
                            builder.AppendLine($"{user.GetDisplayName()} {user.Mention} has premium roles that they are not entitled to {string.Join(" ", notEntitled.Select(x => x.Mention))}");
                        }
                    }
                }
            }

            await ReplyAsync(builder.ToString().FixLength(2047));
        }

        [Command("ConsoleDoc", RunMode = RunMode.Async)]
        public async Task ConsoleDocs()
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Modules");
            foreach (var module in Cmd.Modules)
            {
                builder.AppendLine($"[{module.Name}](#{module.Name}) - {module.Commands.Count} commands\n");
            }

            var seenEnums = new List<Type>();
            var enumBuilder = new StringBuilder();
            enumBuilder.AppendLine("# Types");

            foreach (var module in Cmd.Modules)
            {
                builder.AppendLine($"## {module.Name}");
                if (module.Preconditions.Count > 0)
                {
                    var mPreconditionString = string.Join("\n\n", module.Preconditions.Select(x =>
                    {
                        if (x is PreconditionBase preBase)
                        {
                            return $"__{preBase.Name()}__: {preBase.PreviewText()}";
                        }
                        else
                        {
                            return x.GetType().Name;
                        }
                    }).Distinct().ToArray());
                    builder.AppendLine("Preconditions:\n\n" + mPreconditionString);
                }
                builder.AppendLine("|Name|Description|Parameters|Aliases|Permissions|Remarks|\n|--|--|--|--|--|--|");

                foreach (var command in module.Commands.OrderBy(x => x.Name))
                {
                    var preconditionString = string.Join("<hr>", command.Preconditions.Select(x =>
                    {
                        if (x is PreconditionBase preBase)
                        {
                            return $"__{preBase.Name()}__ {preBase.PreviewText()}";
                        }
                        else
                        {
                            return x.GetType().Name;
                        }
                    }).Distinct().ToArray());

                    builder.AppendLine($"|{command.Name}|{command.Summary}|" + string.Join(" ", command.Parameters.Select(parameter =>
                    {
                        var initial = parameter.Name + (parameter.Summary == null ? "" : $"({parameter.Summary})");

                        if (parameter.IsOptional)
                        {
                            if (parameter.DefaultValue == null)
                            {
                                initial += $":optional";
                            }
                            else
                            {
                                initial += $":optional({parameter.DefaultValue})";
                            }
                        }

                        if (parameter.IsMultiple)
                        {
                            initial += ":multiple";
                        }

                        if (parameter.Type.IsEnum)
                        {
                            if (seenEnums.All(x => x != parameter.Type))
                            {
                                seenEnums.Add(parameter.Type);
                                enumBuilder.AppendLine($"## {parameter.Type.Name}");
                                enumBuilder.AppendLine(string.Join("\n\n", parameter.Type.GetEnumNames().Select(x => $"`{x}`")));
                            }

                            initial = $"[{initial}](#{parameter.Type.Name})";
                        }

                        return "{" + initial + "}";
                    }))
                    +
                    $"|{string.Join(", ", command.Aliases)}|{preconditionString}|{command.Remarks}|");
                }
            }

            Console.WriteLine(builder.ToString());
            Console.WriteLine(enumBuilder.ToString());
        }

        [Command("DeveloperUsers", RunMode = RunMode.Sync)]
        public async Task ShowDevelopersAsync()
        {
            using (var db = new Database())
            {
                var devs = db.WhitelistedDevelopers.ToArray();
                await ReplyAsync("Devs:\n" + string.Join("\n", devs.Select(x => Discord.MentionUtils.MentionUser(x.UserId)).ToArray()));
            }
        }

        [Command("AddDeveloperUser", RunMode = RunMode.Sync)]
        public async Task AddRoleAsync(SocketUser user)
        {
            using (var db = new Database())
            {
                var match = db.WhitelistedDevelopers.Find(user.Id);
                if (match != null)
                {
                    await ReplyAsync("User is already whitelisted.");
                    return;
                }
                else
                {
                    db.WhitelistedDevelopers.Add(new Preconditions.Dev.WhitelistedUser
                    {
                        UserId = user.Id
                    });
                }

                db.SaveChanges();
            }

            await ReplyAsync("Done.");
        }

        [Command("ScanWebhookDeleteMessage", RunMode = RunMode.Sync)]
        public async Task ScanWebhookDeleteAsync(ulong messageId)
        {
            var message = await Context.Channel.GetMessageAsync(messageId);
            if (message == null)
            {
                await ReplyAsync("Message not found.");
                return;
            }

            await Prem.TryParseWebhookResponse(message);
        }

        [Command("AddPremiumRole", RunMode = RunMode.Sync)]
        public async Task AddRoleAsync(SocketRole role, int maxCount)
        {
            await using (var db = new Database())
            {
                var match = db.PremiumRoles.Find(role.Id);
                if (match != null)
                {
                    match.Limit = maxCount;
                }
                else
                {
                    db.PremiumRoles.Add(new PremiumService.PremiumRole
                    {
                        RoleId = role.Id,
                        Limit = maxCount
                    });
                }

                db.SaveChanges();
            }

            await ReplyAsync("Done.");
        }

        [Command("PremiumRoles")]
        public async Task ShowRolesAsync()
        {
            await using (var db = new Database())
            {
                var roles = db.PremiumRoles.ToArray();
                await SimpleEmbedAsync("Roles:\n" + string.Join("\n", roles.Select(x => MentionUtils.MentionRole(x.RoleId) + " - " + x.Limit)));
            }
        }

        [Command("LastLegacyPremium", RunMode = RunMode.Async)]
        public async Task LastLegacyPremium()
        {
            await using (var db = new Database())
            {
                var configs = db.Competitions.AsNoTracking().Where(x => x.LegacyPremiumExpiry != null).ToArray().OrderByDescending(x => x.LegacyPremiumExpiry).Take(20).ToArray();

                await SimpleEmbedAsync(string.Join("\n", configs.Select(x => $"{x.GuildId} - Expires on: {x.LegacyPremiumExpiry.Value.ToString("dd MMM yyyy")} Remaining: {(x.LegacyPremiumExpiry.Value - DateTime.UtcNow).GetReadableLength()}")));
            }
        }

        /*
        [Command("SetPatreonUrl", RunMode = RunMode.Async)]
        public async Task SetPatreonUrl([Remainder]string url)
        {
            var config = Local.GetConfig();

            var premiumConfig = config.GetConfig<PremiumService.Config>(premKey) ?? new PremiumService.Config();

            premiumConfig.GuildId = Context.Guild.Id;
            premiumConfig.AltLink = url;

            config.AdditionalConfigs[premKey] = premiumConfig;

            Local.SaveConfig(config);
            await ReplyAsync("Done.");
        }

        [Command("SetPatreonGuildInvite", RunMode = RunMode.Async)]
        public async Task SetPatreonGuildInvite([Remainder]string url)
        {
            var config = Local.GetConfig();

            var premiumConfig = config.GetConfig<PremiumService.Config>(premKey) ?? new PremiumService.Config();

            premiumConfig.GuildId = Context.Guild.Id;
            premiumConfig.ServerInvite = url;

            config.AdditionalConfigs[premKey] = premiumConfig;

            Local.SaveConfig(config);
            await ReplyAsync("Done.");
        }*/

        [Command("PurgePermissionCache", RunMode = RunMode.Async)]
        public async Task PurgeCache()
        {
            ELO.Extensions.Extensions.RegCache.Clear();
            ELO.Services.PermissionService.PermissionCache.Clear();
            await ReplyAsync("Done.");
        }

        [Command("PurgePrefixCache", RunMode = RunMode.Async)]
        public async Task PurgePrefixCache()
        {
            ELO.Handlers.ELOEventHandler.ClearPrefixCache();
            await ReplyAsync("Done.");
        }

        [Command("CannotBeRun", RunMode = RunMode.Async)]
        [Summary("This command should never be able to run")]
        [CannotRun]
        public async Task CBR()
        {
            await ReplyAsync("Whoops.");
        }

        public class CannotRun : PreconditionBase
        {
            public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
            {
                return Task.FromResult(PreconditionResult.FromError("This command cannot be run at all."));
            }

            public override string Name()
            {
                return "Cannot Run";
            }

            public override string PreviewText()
            {
                return "This command should not be able to run under any circumstances";
            }
        }
    }
}