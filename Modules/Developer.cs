using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Methods.Migrations;
using RavenBOT.ELO.Modules.Premium;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireOwner]
    public class Developer : ReactiveBase
    {
        public Developer(IDatabase database, Random random, ELOService service, PatreonIntegration prem, ELOMigrator migrator, LocalManagementService local)
        {
            Database = database;
            Random = random;
            Service = service;
            PremiumService = prem;
            Migrator = migrator;
            Local = local;
        }
        public IDatabase Database { get; }
        public Random Random { get; }
        public ELOService Service { get; }
        public PatreonIntegration PremiumService { get; }
        public ELOMigrator Migrator { get; }
        public LocalManagementService Local { get; }

        [Command("RunMigrationTask", RunMode = RunMode.Sync)]
        public async Task RunMigrationTaskAsync()
        {
            await ReplyAsync("Running migration.");
            var _ = Task.Run(async () => 
            {
                Migrator.RunMigration(Local);
                await ReplyAsync("Done.");
            });
        }

        [Command("AddPremiumRole", RunMode = RunMode.Sync)]
        public async Task AddRoleAsync(SocketRole role, int maxCount)
        {
            var config = PremiumService.GetConfig();
            config.GuildId = Context.Guild.Id;
            config.Roles.Add(role.Id, new PatreonIntegration.PatreonConfig.ELORole
            {
                RoleId = role.Id,
                MaxRegistrationCount = maxCount
            });
            PremiumService.SaveConfig(config);
            await ReplyAsync("Done.");
        }

        [Command("SetRegistrationCounts", RunMode = RunMode.Async)]
        public async Task SetCounts()
        {
            Service.UpdateCompetitionSetups();
            await ReplyAsync("Running... This will not send a message upon completion.");
        }


        [Command("LastLegacyPremium", RunMode = RunMode.Async)]
        public async Task LastLegacyPremium()
        {
            var date = Migrator.Legacy.GetLatestExpiryDate();
            await ReplyAsync($"Expires on: {date.ToString("dd MMM yyyy")} {date.ToShortTimeString()}\nRemaining: {(date - DateTime.UtcNow).GetReadableLength()}");
        }

        [Command("TogglePremium", RunMode = RunMode.Async)]
        public async Task TogglePremium()
        {
            var config = PremiumService.GetConfig();
            config.Enabled = !config.Enabled;
            PremiumService.SaveConfig(config);
            await ReplyAsync($"Premium Enabled: {config.Enabled}");
        }

        [Command("SetPatreonUrl", RunMode = RunMode.Async)]
        public async Task SetPatreonUrl([Remainder]string url)
        {
            var config = PremiumService.GetConfig();
            config.PageUrl = url;
            PremiumService.SaveConfig(config);
            await ReplyAsync($"Set.");
        }

        [Command("SetPatreonGuildInvite", RunMode = RunMode.Async)]
        public async Task SetPatreonGuildInvite([Remainder]string url)
        {
            var config = PremiumService.GetConfig();
            config.ServerInvite = url;
            PremiumService.SaveConfig(config);
            await ReplyAsync($"Set.");
        }
    }
}