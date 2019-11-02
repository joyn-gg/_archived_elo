using Discord.WebSocket;
using ELO.Models;
using ELO.Services;

namespace ELO.Extensions
{
    public static class Extensions
    {
        public static bool IsRegistered(this SocketGuildUser user, out Player player)
        {
            using (var db = new Database())
            {
                player = db.Players.Find(user.Guild.Id, user.Id);
                return player != null;
            }
        }
    }
}
