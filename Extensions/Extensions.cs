using Discord.WebSocket;
using ELO.EF;
using ELO.EF.Models;
using System;
using System.Collections.Generic;
using System.Text;

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
