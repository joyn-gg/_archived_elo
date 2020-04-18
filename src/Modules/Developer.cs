using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
    [RavenRequireOwner]
    [Group("devcmd")]
    public class Developer : ReactiveBase
    {
        public Developer(Random random, PremiumService prem)
        {
            Random = random;
            Prem = prem;
        }

        public Random Random { get; }

        public PremiumService Prem { get; }

        private string premKey = "PremiumConfig";

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