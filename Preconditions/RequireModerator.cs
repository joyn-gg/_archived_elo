using System.Linq;
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;

namespace RavenBOT.ELO.Modules.Preconditions
{
    public class RequireModerator : PreconditionBase
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context.Channel is SocketGuildChannel gChannel))
            {
                return Task.FromResult(PreconditionResult.FromError("This command can only be run in an ELO bot server."));
            }

            if (!(context.User is IGuildUser gUser))
            {
                return Task.FromResult(PreconditionResult.FromError("You are not recognised as a guild user."));
            }

            if (gUser.GuildPermissions.Administrator)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            var eloService = services.GetRequiredService<ELOService>();
            var competition = eloService.GetOrCreateCompetition(context.Guild.Id);

            if (competition.ModeratorRole != 0)
            {
                if (gUser.RoleIds.Contains(competition.ModeratorRole))
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }

            
            if (competition.AdminRole != 0)
            {
                if (gUser.RoleIds.Contains(competition.AdminRole))
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }

            return Task.FromResult(PreconditionResult.FromError("You are not a moderator, elo admin or server admin"));
        }

        public override string Name()
        {
            return "Moderator Command";
        }

        public override string PreviewText()
        {
            return "Requires the user has an elo moderator role, elo admin role or server admin role";
        }
    }
}