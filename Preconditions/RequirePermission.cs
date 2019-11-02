using Discord.Commands;
using Discord.WebSocket;
using ELO.EF;
using ELO.EF.Models;
using ELO.Extensions;
using ELO.Models;
using ELO.Services;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Preconditions
{
    public class RequirePermission : PreconditionBase
    {        
        public enum PermissionLevel
        {
            Owner,
            ServerAdmin,
            ELOAdmin,
            Moderator,
            Registered,
            Default
        }

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

            var result = permissionService.EvaluatePermission(command.Name, gUser, out var level);
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
                    if (gUser.IsRegistered(out var _))
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
                    using (var db = new Database())
                    {
                        var comp = db.Competitions.Find(gUser.Guild.Id);
                        bool pass;
                        if (comp == null)
                        {
                            pass = gUser.Roles.Any(x => x.Permissions.Administrator);
                        }
                        else
                        {
                            pass = gUser.Roles.Any(x => x.Id == comp.ModeratorRole || x.Id == comp.AdminRole || x.Permissions.Administrator);
                        }

                        if (pass)
                        {
                            return PreconditionResult.FromSuccess();
                        }
                        else
                        {
                            return PreconditionResult.FromError("You must be a moderator in order to run this command.");
                        }
                    }

                }

                if (Level == PermissionLevel.ELOAdmin)
                {
                    using (var db = new Database())
                    {
                        var comp = db.Competitions.Find(gUser.Guild.Id);
                        bool pass;
                        if (comp == null)
                        {
                            pass = gUser.GuildPermissions.AddReactions;
                        }
                        else
                        {
                            pass = gUser.Roles.Any(x => x.Id == comp.AdminRole || x.Permissions.Administrator);
                        }
                        if (pass)
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
            return "Moderator Command";
        }

        public override string PreviewText()
        {
            return "Requires the user has an elo moderator role, elo admin role or server admin role";
        }
    }
}
