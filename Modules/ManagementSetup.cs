using Discord.Commands;
using Discord.WebSocket;
using Discord;
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