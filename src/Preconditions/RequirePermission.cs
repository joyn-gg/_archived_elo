using Discord.Commands;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Services;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Preconditions
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

            /*
            var eloService = services.GetRequiredService<ELOService>();
            if (eloService.PermissionBypass)
            {
                await eloService.PopulateOwner();
                if (context.User.Id == eloService.OwnerId)
                {
                    return PreconditionResult.FromSuccess();
                }
            }
            */

            var permissionService = services.GetRequiredService<PermissionService>();
            if (permissionService.PermissionBypass)
            {
                await permissionService.PopulateOwner();
                if (context.User.Id == permissionService.OwnerId)
                {
                    return PreconditionResult.FromSuccess();
                }
            }

            var result = permissionService.EvaluateCustomPermission(command.Name, gUser, out var level);
            if (result.Item1 == true)
            {
                return PreconditionResult.FromSuccess();
            }
            else if (result.Item1 == false)
            {
                return PreconditionResult.FromError($"You do not have permission to use this command Level: {level}");
            }
            else
            {
                if (Level == PermissionLevel.Registered)
                {
                    if (gUser.IsRegistered(out var _, false))
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
                    if (gUser.GuildPermissions.Administrator || gUser.Roles.Any(x => x.Id == result.Item2.ModId || x.Id == result.Item2.AdminId || x.Permissions.Administrator))
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
                    using (var db = new Database())
                    {
                        if (gUser.GuildPermissions.Administrator || gUser.Roles.Any(x => x.Id == result.Item2.AdminId || x.Permissions.Administrator))
                        {
                            return PreconditionResult.FromSuccess();
                        }
                        else
                        {
                            return PreconditionResult.FromError("You must be an elo admin in order to run this command.");
                        }
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
            return "Custom Permission";
        }

        public override string PreviewText()
        {
            return $"Requires the user the specified permission level of: {Level}, NOTE: This may be overridden by the server";
        }
    }
}
