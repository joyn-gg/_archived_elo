using Discord.WebSocket;
using ELO.Models;
using ELO.Services;
using System.Collections.Generic;

namespace ELO.Extensions
{
    public static class Extensions
    {
        public static Dictionary<ulong, Dictionary<ulong, bool>> RegistrationCache = new Dictionary<ulong, Dictionary<ulong, bool>>();

        private static object cacheLock = new object();

        public static bool IsRegistered(this SocketGuildUser user, out Player player, bool required = true)
        {
            //Create a new db session.
            using (var db = new Database())
            {
                //Ensure there is a cached value for the user in question
                if (!RegistrationCache.ContainsKey(user.Guild.Id))
                {
                    RegistrationCache.Add(user.Guild.Id, new Dictionary<ulong, bool>());
                }

                //Find the cache for the specified guild
                var guildCache = RegistrationCache[user.Guild.Id];
                if (guildCache.TryGetValue(user.Id, out var registered))
                {
                    //Check cache to avoid initial db query
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
                    //The user is not cached so populate the cache with if they are registered or not
                    lock (cacheLock)
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
}