using Discord;
using Discord.WebSocket;
using RavenBOT.ELO.Modules.Models;

namespace RavenBOT.ELO.Modules.Methods
{
    public static class ELOExtensions
    {
        public static bool IsLobby(this IMessageChannel channel, ELOService service, out Lobby lobby)
        {
            if (!(channel is SocketGuildChannel gChannel))
            {
                lobby = null;
                return false;
            }

            lobby = service.GetLobby(gChannel.Guild.Id, gChannel.Id);
            return lobby != null;
        }

        public static bool CanRename(this SocketGuildUser user, SocketGuildUser target)
        {
            if (user.GuildPermissions.ManageNicknames)
            {
                if (user.Hierarchy > target.Hierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanManageRoles(this SocketGuildUser user, SocketGuildUser target)
        {
            if (user.GuildPermissions.ManageRoles)
            {
                if (user.Hierarchy > target.Hierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsRegistered(this IUser user, ELOService service, out Player player)
        {
            if (!(user is SocketGuildUser gUser))
            {
                player = null;
                return false;
            }

            player = service.GetPlayer(gUser.Guild.Id, gUser.Id);
            return player != null;
        }
    }
}