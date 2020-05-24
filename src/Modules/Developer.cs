using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Preconditions;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private string premKey = "PremiumConfig";

        [Command("ConsoleDoc", RunMode = RunMode.Async)]
        public async Task ConsoleDocs()
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Commands");

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
                            return $"__{preBase.Name()}__ {preBase.PreviewText()}";
                        }
                        else
                        {
                            return x.GetType().Name;
                        }
                    }).Distinct().ToArray());
                    builder.AppendLine("Preconditions:\n\n" + mPreconditionString);
                }
                builder.AppendLine("|Name|Description|Parameters|Example|Aliases|Permissions|Remarks|\n|--|--|--|--|--|--|--|");

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
                             if (seenEnums.All(x => !x.Equals(parameter.Type)))
                             {
                                 seenEnums.Add(parameter.Type);
                                 enumBuilder.AppendLine($"## {parameter.Type.Name}");
                                 enumBuilder.AppendLine(string.Join("\n\n", parameter.Type.GetEnumNames().Select(x => $"`{x}`")));
                             }
                         }

                         return "{" + initial + "}";
                     }))
                    +
                    $"|N/A|{string.Join(", ", command.Aliases)}|{preconditionString}|{command.Remarks}|");
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
            using (var db = new Database())
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
            using (var db = new Database())
            {
                var roles = db.PremiumRoles.ToArray();
                await SimpleEmbedAsync("Roles:\n" + string.Join("\n", roles.Select(x => MentionUtils.MentionRole(x.RoleId) + " - " + x.Limit)));
            }
        }

        [Command("LastLegacyPremium", RunMode = RunMode.Async)]
        public async Task LastLegacyPremium()
        {
            using (var db = new Database())
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
        public async Task PurgeCache([Remainder]string url)
        {
            ELO.Extensions.Extensions.RegistrationCache = new Dictionary<ulong, Dictionary<ulong, bool>>();
            ELO.Services.PermissionService.PermissionCache = new Dictionary<ulong, PermissionService.CachedPermissions>();
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