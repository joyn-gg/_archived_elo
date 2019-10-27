using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireUserPermission(Discord.GuildPermission.Administrator)]
    [RavenRequireContext(ContextType.Guild)]
    public class ManagementSetup : ReactiveBase
    {
        public ELOService Service { get; }

        public ManagementSetup(ELOService service)
        {
            Service = service;
        }

        [Command("SetModerator", RunMode = RunMode.Sync)]
        [Alias("Set Moderator", "Set Moderator Role", "SetMod", "Set Mod Role")]
        [Summary("Sets the ELO moderator role for the server.")]
        public async Task SetModeratorAsync(SocketRole modRole = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.ModeratorRole = modRole?.Id ?? 0;
            Service.SaveCompetition(competition);
            if (modRole != null)
            {
                await ReplyAsync("Moderator role set.");
            }
            else
            {
                await ReplyAsync("Mod role is no longer set, only ELO Admins and users with a role that has `Administrator` permissions can run moderator commands now.");
            }
        }

        [Command("SetAdmin", RunMode = RunMode.Sync)]
        [Summary("Sets the ELO admin role for the server.")]
        public async Task SetAdminAsync(SocketRole adminRole = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.AdminRole = adminRole?.Id ?? 0;
            Service.SaveCompetition(competition);
            if (adminRole != null)
            {
                await ReplyAsync("Admin role set.");
            }
            else
            {
                await ReplyAsync("Admin role is no longer set, only users with a role that has `Administrator` permissions can act as an admin now.");
            }
        }
    }
}