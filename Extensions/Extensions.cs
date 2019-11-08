using Discord.WebSocket;
using ELO.Models;
using ELO.Services;
using System.Collections.Generic;

namespace ELO.Extensions
{
    public static class Extensions
    {
        public static Dictionary<ulong, Dictionary<ulong, bool>> RegistrationCache = new Dictionary<ulong, Dictionary<ulong, bool>>();

        public static bool IsRegistered(this SocketGuildUser user, out Player player, bool required = true)
        {
            using (var db = new Database())
            {
                if (!RegistrationCache.ContainsKey(user.Guild.Id))
                {
                    RegistrationCache.Add(user.Guild.Id, new Dictionary<ulong, bool>());
                }

                var guildCache = RegistrationCache[user.Guild.Id];
                if (guildCache.TryGetValue(user.Id, out var registered))
                {
                    if (registered)
                    {
                        //Query db here
                        if (required)
                        {
                            player = db.Players.Find(user.Guild.Id, user.Id);
                        }
                        else
                        {
                            player = null;
                        }

                        return true;
                    }
                    else
                    {
                        player = null;
                        return false;
                    }
                }
                else
                {
                    player = db.Players.Find(user.Guild.Id, user.Id);
                    if (player == null)
                    {
                        guildCache.Add(user.Id, false);
                        return false;
                    }
                    else
                    {
                        guildCache.Add(user.Id, true);
                        return true;
                    }
                }
            }
        }
    }
}
