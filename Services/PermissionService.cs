using Discord.WebSocket;
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

        public DiscordShardedClient Client { get; }

        public async Task PopulateOwner()
        {
            if (OwnerId == 0)
            {
                var info = await Client.GetApplicationInfoAsync();
                OwnerId = info.Owner.Id;
            }
        }

        public bool? EvaluatePermission(string commandName, SocketGuildUser user, out PermissionLevel? permissionLevel)
        {
            permissionLevel = null;
            using (var db = new Database())
            {
                var permission = db.Permissions.Find(user.Guild.Id, commandName);
                if (permission == null) return null;

                if (permission.Level == PermissionLevel.Default) return null;
                if (permission.Level == PermissionLevel.Registered)
                {
                    return db.Players.Find(user.Guild.Id, user.Id) != null;
                }
                if (permission.Level == PermissionLevel.Moderator)
                {
                    var comp = db.Competitions.Find(user.Guild.Id);
                    if (comp == null) return user.Roles.Any(x => x.Permissions.Administrator);
                    return user.Roles.Any(x => x.Id == comp.ModeratorRole || x.Id == comp.AdminRole || x.Permissions.Administrator);
                }
                if (permission.Level == PermissionLevel.ELOAdmin)
                {
                    var comp = db.Competitions.Find(user.Guild.Id);
                    if (comp == null) return user.Roles.Any(x => x.Permissions.Administrator);
                    return user.Roles.Any(x => x.Id == comp.AdminRole || x.Permissions.Administrator);
                }
                if (permission.Level == PermissionLevel.ServerAdmin)
                {
                    return user.GuildPermissions.Administrator;
                }
                if (permission.Level == PermissionLevel.Owner)
                {
                    return user.Id == user.Guild.OwnerId;
                }
            }

            return null;
        }
    }
}
