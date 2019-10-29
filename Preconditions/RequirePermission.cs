﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using System;
using System.Linq;
using System.Threading.Tasks;
using static RavenBOT.ELO.Modules.Methods.ELOService;
using static RavenBOT.ELO.Modules.Models.CompetitionConfig;

namespace RavenBOT.ELO.Modules.Preconditions
{
    public class RequirePermission : PreconditionBase
    {
        private readonly PermissionLevel Level;
        public RequirePermission(PermissionLevel level)
        {
            if (level == PermissionLevel.Default) throw new Exception("Cannot use default as the default value.");

            Level = level;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context.Channel is SocketGuildChannel gChannel))
            {
                return PreconditionResult.FromError("This command can only be run in an ELO bot server.");
            }

            if (!(context.User is SocketGuildUser gUser))
            {
                return PreconditionResult.FromError("You are not recognised as a guild user.");
            }

            var eloService = services.GetRequiredService<ELOService>();
            if (eloService.PermissionBypass)
            {
                await eloService.PopulateOwner();
                if (context.User.Id == eloService.OwnerId)
                {
                    return PreconditionResult.FromSuccess();
                }
            }
            //var competition = eloService.GetOrCreateCompetition(context.Guild.Id);
            var permission = eloService.GetPermission(context.Guild.Id);

            var result = eloService.EvaluatePermission(permission, command.Name, gUser, out var level);
            if (result == true)
            {
                return PreconditionResult.FromSuccess();
            }
            else if (result == false)
            {
                return PreconditionResult.FromError($"You do not have permission to use this command Level: {level}");
            }
            else
            {
                if (Level == PermissionLevel.Registered)
                {
                    if (gUser.IsRegistered(eloService, out var _))
                    {
                        return PreconditionResult.FromSuccess();
                    }
                    else
                    {
                        return PreconditionResult.FromError("You must be registered in order to run this command.");
                    }
                }

                if (Level == PermissionLevel.Moderator)
                {
                    if (gUser.Roles.Any(x => x.Id == permission.ModeratorRoleId || x.Id == permission.AdminRoleId || x.Permissions.Administrator) || gUser.GuildPermissions.Administrator)
                    {
                        return PreconditionResult.FromSuccess();
                    }
                    else
                    {
                        return PreconditionResult.FromError("You must be a moderator in order to run this command.");
                    }
                }

                if (Level == PermissionLevel.ELOAdmin)
                {
                    if (gUser.Roles.Any(x => x.Id == permission.AdminRoleId || x.Permissions.Administrator) || gUser.GuildPermissions.Administrator)
                    {
                        return PreconditionResult.FromSuccess();
                    }
                    else
                    {
                        return PreconditionResult.FromError("You must be an elo admin in order to run this command.");
                    }
                }

                if (Level == PermissionLevel.ServerAdmin)
                {
                    if (gUser.GuildPermissions.Administrator)
                    {
                        return PreconditionResult.FromSuccess();
                    }
                    else
                    {
                        return PreconditionResult.FromError("You must be a server administrator in order to run this command.");
                    }
                }

                if (Level == PermissionLevel.Owner)
                {
                    if (gUser.Id == gUser.Guild.OwnerId)
                    {
                        return PreconditionResult.FromSuccess();
                    }
                    else
                    {
                        return PreconditionResult.FromError("You must be the server owner in order to run this command.");
                    }
                }
            }

            return PreconditionResult.FromError($"You do not have permission to run this command. Level: {Level}");
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