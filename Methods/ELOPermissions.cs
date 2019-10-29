using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RavenBOT.ELO.Modules.Models.CompetitionConfig;

namespace RavenBOT.ELO.Modules.Methods
{
    public partial class ELOService
    {
        public Dictionary<ulong, CachedPermission> PermissionCache = new Dictionary<ulong, CachedPermission>();
        public bool PermissionBypass = false;
        public ulong OwnerId = 0;
        public async Task PopulateOwner()
        {
            if (OwnerId == 0)
            {
                var info = await Client.GetApplicationInfoAsync();
                OwnerId = info.Owner.Id;
            }
        }

        public class CachedPermission
        {
            public ulong AdminRoleId { get; set; } = 0;
            public ulong ModeratorRoleId { get; set; } = 0;

            public Dictionary<string, PermissionLevel> CachedPermissions = new Dictionary<string, PermissionLevel>();
        }

        public CachedPermission GetPermission(ulong guildId)
        {
            if (PermissionCache.TryGetValue(guildId, out var cached))
            {
                return cached;
            }
            else
            {
                var comp = GetOrCreateCompetition(guildId);
                PermissionCache[guildId] = new CachedPermission
                {
                    AdminRoleId = comp.AdminRole,
                    ModeratorRoleId = comp.ModeratorRole,
                    CachedPermissions = comp.Permissions
                };
                return PermissionCache[guildId];
            }
        }

        public bool? EvaluatePermission(CachedPermission permissions, string commandName, SocketGuildUser user, out PermissionLevel? permissionLevel)
        {
            var match = permissions.CachedPermissions.FirstOrDefault(x => x.Key.Equals(commandName.ToLower(), StringComparison.OrdinalIgnoreCase));
            if (permissions.CachedPermissions.TryGetValue(commandName.ToLower(), out var value))
            {
                permissionLevel = value;
                if (value == PermissionLevel.Default) return null;
                if (value == PermissionLevel.Registered)
                {
                    return user.IsRegistered(this, out var _);
                }
                if (value == PermissionLevel.Moderator)
                {
                    return user.Roles.Any(x => x.Id == permissions.ModeratorRoleId || x.Id == permissions.AdminRoleId || x.Permissions.Administrator);
                }
                if (value == PermissionLevel.ELOAdmin)
                {
                    return user.Roles.Any(x => x.Id == permissions.AdminRoleId || x.Permissions.Administrator);
                }
                if (value == PermissionLevel.ServerAdmin)
                {
                    return user.GuildPermissions.Administrator;
                }
                if (value == PermissionLevel.Owner)
                {
                    return user.Id == user.Guild.OwnerId;
                }

                //Returning null is equivalent to level is default
                return null;
            }
            else
            {
                permissionLevel = null;
                return null;
            }
        }
    }
}
