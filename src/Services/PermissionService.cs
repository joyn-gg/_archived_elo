using Discord.WebSocket;
using ELO.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class PermissionService
    {
        public PermissionService(DiscordShardedClient client)
        {
            Client = client;
        }

        public bool PermissionBypass = false;

        public ulong OwnerId = 0;

        public static Dictionary<ulong, CachedPermissions> PermissionCache = new Dictionary<ulong, CachedPermissions>();

        public class CachedPermissions
        {
            public ulong GuildId;

            public ulong? AdminId = null;

            public ulong? ModId = null;

            public Dictionary<string, CachedPermission> Cache = new Dictionary<string, CachedPermission>();

            public class CachedPermission
            {
                public string CommandName;

                public PermissionLevel Level;
            }
        }

        public DiscordShardedClient Client { get; }

        public virtual async Task PopulateOwner()
        {
            if (OwnerId == 0)
            {
                var info = await Client.GetApplicationInfoAsync();
                OwnerId = info.Owner.Id;
            }
        }

        public (CachedPermissions, CachedPermissions.CachedPermission) GetCached(ulong guildId, string commandName)
        {
            commandName = commandName.ToLower();
            CachedPermissions.CachedPermission permission;

            // Check if guild cache contains current server already
            if (PermissionCache.TryGetValue(guildId, out var guildCache))
            {
                // Check guild's cache for server
                if (guildCache.Cache.TryGetValue(commandName.ToLower(), out permission))
                {
                    if (permission == null)
                    {
                        //No override found.
                        return (guildCache, null);
                    }
                }
                else
                {
                    using (var db = new Database())
                    {
                        var dbPermission = db.Permissions.FirstOrDefault(x => x.GuildId == guildId && x.CommandName == commandName);
                        if (dbPermission == null)
                        {
                            guildCache.Cache.Add(commandName.ToLower(), null);
                        }
                        else
                        {
                            permission = new CachedPermissions.CachedPermission
                            {
                                CommandName = commandName.ToLower(),
                                Level = dbPermission.Level
                            };
                            guildCache.Cache.Add(permission.CommandName, permission);
                        }
                    }
                }
            }
            else
            {
                using (var db = new Database())
                {
                    var comp = db.GetOrCreateCompetition(guildId);
                    guildCache = new CachedPermissions
                    {
                        GuildId = guildId,
                        ModId = comp.ModeratorRole,
                        AdminId = comp.AdminRole
                    };

                    var dbPermission = db.Permissions.FirstOrDefault(x => x.GuildId == guildId && x.CommandName == commandName);
                    if (dbPermission == null)
                    {
                        permission = null;
                        guildCache.Cache.Add(commandName.ToLower(), null);
                    }
                    else
                    {
                        permission = new CachedPermissions.CachedPermission
                        {
                            CommandName = commandName.ToLower(),
                            Level = dbPermission.Level
                        };
                        guildCache.Cache.Add(permission.CommandName, permission);
                    }

                    PermissionCache.Add(guildId, guildCache);
                }
            }

            return (guildCache, permission);
        }

        public (bool?, CachedPermissions, CachedPermissions.CachedPermission) EvaluateCustomPermission(string commandName, SocketGuildUser user, out PermissionLevel? permissionLevel)
        {
            permissionLevel = null;
            var perms = GetCached(user.Guild.Id, commandName);
            var guildCache = perms.Item1;
            if (perms.Item2 == null) return (null, guildCache, null);
            var permission = perms.Item2;

            permissionLevel = permission.Level;
            if (permission.Level == PermissionLevel.Default) return (null, guildCache, null);
            if (permission.Level == PermissionLevel.Registered)
            {
                return (user.IsRegistered(), guildCache, permission);
            }

            if (permission.Level == PermissionLevel.Moderator)
            {
                return (user.GuildPermissions.Administrator || user.Roles.Any(x => x.Id == guildCache.ModId || x.Id == guildCache.AdminId || x.Permissions.Administrator), guildCache, permission);
            }
            if (permission.Level == PermissionLevel.ELOAdmin)
            {
                return (user.GuildPermissions.Administrator || user.Roles.Any(x => x.Id == guildCache.AdminId || x.Permissions.Administrator), guildCache, permission);
            }
            if (permission.Level == PermissionLevel.ServerAdmin)
            {
                return (user.GuildPermissions.Administrator, guildCache, permission);
            }
            if (permission.Level == PermissionLevel.Owner)
            {
                return (user.Id == user.Guild.OwnerId, guildCache, permission);
            }

            return (null, guildCache, permission);
        }
    }
}