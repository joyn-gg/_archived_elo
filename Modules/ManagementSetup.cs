using Discord.Commands;
using Discord.WebSocket;
using Discord;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using System.Threading.Tasks;
using RavenBOT.ELO.Modules.Models;
using static RavenBOT.ELO.Modules.Models.CompetitionConfig;
using System.Linq;

namespace RavenBOT.ELO.Modules.Modules
{
    [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.ServerAdmin)]
    [RavenRequireContext(ContextType.Guild)]
    public class ManagementSetup : ReactiveBase
    {
        public ELOService Service { get; }
        public CommandService CommandService { get; }

        public ManagementSetup(ELOService service, CommandService commandService)
        {
            Service = service;
            CommandService = commandService;
        }

        [Command("PermissionLevels", RunMode = RunMode.Async)]
        [Summary("Shows all possible permission levels.")]
        public async Task ShowLevelsAsync()
        {
            var options = Extensions.GetEnumNameValues<PermissionLevel>();
            await SimpleEmbedAsync($"Custom Permissions Levels:\n{string.Join("\n", options.Select(x => x.Item1))}", Color.Blue);
        }

        [Command("ShowPermissions", RunMode = RunMode.Async)]
        [Summary("Shows all custom set permission levels.")]
        public async Task ShowPermissionsAsync()
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            await SimpleEmbedAsync($"Custom Permissions:\n{string.Join("\n", competition.Permissions.Select(x => $"{x.Key} - {x.Value}"))}", Color.Blue);
        }

        [Command("SetCommandPermission", RunMode = RunMode.Sync)]
        [Summary("Sets the required permission for a specified command.")]
        public async Task SetPermissionAsync(string commandName, PermissionLevel level)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            var match = CommandService.Commands.FirstOrDefault(x => x.Name.Equals(commandName, System.StringComparison.OrdinalIgnoreCase) || x.Aliases.Any(a => a.Equals(commandName, System.StringComparison.OrdinalIgnoreCase)));

            if (match == null)
            {
                await SimpleEmbedAsync("Unknown command name.", Color.Red);
                return;
            }

            competition.Permissions[match.Name.ToLower()] = level;
            Service.PermissionCache[Context.Guild.Id] = new ELOService.CachedPermission
            {
                AdminRoleId = competition.AdminRole,
                ModeratorRoleId = competition.ModeratorRole,
                CachedPermissions = competition.Permissions
            };

            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"{match.Name} permission level set to: {level}", Color.Blue);
        }

        [Command("RemoveCommandPermission", RunMode = RunMode.Sync)]
        [Summary("Sets the required permission for a specified command.")]
        public async Task RemovePermissionAsync(string commandName)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            var match = CommandService.Commands.FirstOrDefault(x => x.Name.Equals(commandName, System.StringComparison.OrdinalIgnoreCase) || x.Aliases.Any(a => a.Equals(commandName, System.StringComparison.OrdinalIgnoreCase)));

            if (match == null)
            {
                await SimpleEmbedAsync("Unknown command name.", Color.Red);
                return;
            }

            competition.Permissions.Remove(match.Name.ToLower());

            Service.PermissionCache[Context.Guild.Id] = new ELOService.CachedPermission
            {
                AdminRoleId = competition.AdminRole,
                ModeratorRoleId = competition.ModeratorRole,
                CachedPermissions = competition.Permissions
            };

            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"{match.Name} permission set back to default.", Color.Blue);
        }

        [Command("SetModerator", RunMode = RunMode.Sync)]
        [Alias("Set Moderator", "Set Moderator Role", "SetMod", "Set Mod Role")]
        [Summary("Sets the ELO moderator role for the server.")]
        public async Task SetModeratorAsync(SocketRole modRole = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.ModeratorRole = modRole?.Id ?? 0;
            Service.PermissionCache[Context.Guild.Id] = new ELOService.CachedPermission
            {
                AdminRoleId = competition.AdminRole,
                ModeratorRoleId = competition.ModeratorRole,
                CachedPermissions = competition.Permissions
            };
            Service.SaveCompetition(competition);
            if (modRole != null)
            {
                await SimpleEmbedAsync("Moderator role set.", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("Mod role is no longer set, only ELO Admins and users with a role that has `Administrator` permissions can run moderator commands now.", Color.DarkBlue);
            }
        }

        [Command("SetAdmin", RunMode = RunMode.Sync)]
        [Summary("Sets the ELO admin role for the server.")]
        public async Task SetAdminAsync(SocketRole adminRole = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.AdminRole = adminRole?.Id ?? 0;
            Service.PermissionCache[Context.Guild.Id] = new ELOService.CachedPermission
            {
                AdminRoleId = competition.AdminRole,
                ModeratorRoleId = competition.ModeratorRole,
                CachedPermissions = competition.Permissions
            };
            Service.SaveCompetition(competition);
            if (adminRole != null)
            {
                await SimpleEmbedAsync("Admin role set.", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("Admin role is no longer set, only users with a role that has `Administrator` permissions can act as an admin now.", Color.DarkBlue);
            }
        }
    }
}