using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using System.Linq;
using System.Threading.Tasks;
using ELO.Extensions;

namespace ELO.Modules
{
    [RequirePermission(PermissionLevel.ServerAdmin)]
    [RequireContext(ContextType.Guild)]
    public class ManagementSetup : ModuleBase<ShardedCommandContext>
    {
        public CommandService CommandService { get; }

        public PermissionService Permissions { get; }

        public ManagementSetup(CommandService commandService, PermissionService permissions)
        {
            CommandService = commandService;
            Permissions = permissions;
        }

        [Command("PermissionLevels", RunMode = RunMode.Async)]
        [Summary("Shows all possible permission levels.")]
        public virtual async Task ShowLevelsAsync()
        {
            var options = Extensions.Extensions.GetEnumNameValues<PermissionLevel>();
            await Context.SimpleEmbedAsync($"Custom Permissions Levels:\n{string.Join("\n", options.Select(x => x.Item1))}", Color.Blue);
        }

        [Command("ShowPermissions", RunMode = RunMode.Async)]
        [Summary("Shows all custom set permission levels.")]
        public virtual async Task ShowPermissionsAsync()
        {
            using (var db = new Database())
            {
                var permissions = db.Permissions.AsQueryable().Where(x => x.GuildId == Context.Guild.Id);
                await Context.SimpleEmbedAsync($"Custom Permissions:\n{string.Join("\n", permissions.Select(x => $"{x.CommandName} - {x.Level}"))}", Color.Blue);
            }
        }

        [Command("SetCommandPermission", RunMode = RunMode.Sync)]
        [Summary("Sets the required permission for a specified command.")]
        public virtual async Task SetPermissionAsync(string commandName, PermissionLevel level)
        {
            using (var db = new Database())
            {
                var match = CommandService.Commands.FirstOrDefault(x => x.Name.Equals(commandName, System.StringComparison.InvariantCultureIgnoreCase) || x.Aliases.Any(a => a.Equals(commandName, System.StringComparison.InvariantCultureIgnoreCase)));

                if (match == null)
                {
                    await Context.SimpleEmbedAsync("Unknown command name.", Color.Red);
                    return;
                }

                if (!match.Preconditions.Any(x => x is RequirePermission) && !match.Module.Preconditions.Any(x => x is RequirePermission))
                {
                    await Context.SimpleEmbedAsync("This command cannot have it's permission overwritten.");
                    return;
                }

                var dbMatch = db.Permissions.Find(Context.Guild.Id, match.Name.ToLower());
                if (dbMatch != null)
                {
                    dbMatch.Level = level;
                    db.Permissions.Update(dbMatch);
                }
                else
                {
                    var permission = new CommandPermission
                    {
                        GuildId = Context.Guild.Id,
                        CommandName = match.Name.ToLower(),
                        Level = level
                    };
                    db.Permissions.Add(permission);
                }

                db.SaveChanges();
                PermissionService.PermissionCache.Remove(Context.Guild.Id);

                await Context.SimpleEmbedAsync($"{match.Name} permission level set to: {level}", Color.Blue);
            }
        }

        [Command("RemoveCommandPermission", RunMode = RunMode.Sync)]
        [Summary("Sets the required permission for a specified command.")]
        public virtual async Task RemovePermissionAsync(string commandName)
        {
            using (var db = new Database())
            {
                var match = CommandService.Commands.FirstOrDefault(x => x.Name.Equals(commandName, System.StringComparison.InvariantCultureIgnoreCase) || x.Aliases.Any(a => a.Equals(commandName, System.StringComparison.InvariantCultureIgnoreCase)));

                if (match == null)
                {
                    await Context.SimpleEmbedAsync("Unknown command name.", Color.Red);
                    return;
                }

                var permission = db.Permissions.Find(Context.Guild.Id, match.Name.ToLower());
                if (permission == null)
                {
                    await Context.SimpleEmbedAsync("Permission override not found.");
                    return;
                }
                db.Permissions.Remove(permission);
                db.SaveChanges();
                PermissionService.PermissionCache.Remove(Context.Guild.Id);
                await Context.SimpleEmbedAsync($"{match.Name} permission set back to default.", Color.Blue);
            }
        }

        [Command("SetModerator", RunMode = RunMode.Sync)]
        [Alias("Set Moderator", "Set Moderator Role", "SetMod", "Set Mod Role")]
        [Summary("Sets the ELO moderator role for the server.")]
        public virtual async Task SetModeratorAsync(SocketRole modRole = null)
        {
            using (var db = new Database())
            {
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                competition.ModeratorRole = modRole?.Id;
                db.Competitions.Update(competition);
                db.SaveChanges();
                PermissionService.PermissionCache.Remove(Context.Guild.Id);
                if (modRole != null)
                {
                    await Context.SimpleEmbedAsync("Moderator role set.", Color.Green);
                }
                else
                {
                    await Context.SimpleEmbedAsync("Mod role is no longer set, only ELO Admins and users with a role that has `Administrator` permissions can run moderator commands now.", Color.DarkBlue);
                }
            }
        }

        [Command("SetAdmin", RunMode = RunMode.Sync)]
        [Summary("Sets the ELO admin role for the server.")]
        public virtual async Task SetAdminAsync(SocketRole adminRole = null)
        {
            using (var db = new Database())
            {
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                competition.AdminRole = adminRole?.Id;
                db.Competitions.Update(competition);
                db.SaveChanges();
                PermissionService.PermissionCache.Remove(Context.Guild.Id);
                if (adminRole != null)
                {
                    await Context.SimpleEmbedAsync("Admin role set.", Color.Green);
                }
                else
                {
                    await Context.SimpleEmbedAsync("Admin role is no longer set, only users with a role that has `Administrator` permissions can act as an admin now.", Color.DarkBlue);
                }
            }
        }
    }
}