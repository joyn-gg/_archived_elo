using Discord.WebSocket;
using ELO.Models;
using ELO.Services;
using System.Collections.Generic;
using System.Linq;

namespace ELO.Extensions
{
    public static class Extensions
    {
        // public static Dictionary<ulong, Dictionary<ulong, bool>> RegistrationCache = new Dictionary<ulong, Dictionary<ulong, bool>>();

        private static object cacheLock = new object();

        public static Dictionary<(ulong guildId, ulong userId), bool> RegCache = new Dictionary<(ulong guildId, ulong userId), bool>();

        public static void SetRegistrationState(ulong guildId, ulong userId, bool state)
        {
            lock (cacheLock)
            {
                var key = (guildId, userId);
                RegCache[key] = state;
            }
        }

        public static bool IsRegistered(this SocketGuildUser user)
        {
            var key = (user.Guild.Id, user.Id);

            lock (cacheLock)
            {
                if (RegCache.TryGetValue(key, out bool registered))
                {
                    return registered;
                }
                else
                {
                    using (var db = new Database())
                    {
                        var playerExists = db.Players.Any(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                        RegCache[key] = playerExists;
                        return playerExists;
                    }
                }
            }
        }

        public static bool IsRegistered(this SocketGuildUser user, out Player player)
        {
            var key = (user.Guild.Id, user.Id);
            bool containsKey;
            bool registered;
            lock (cacheLock)
            {
                containsKey = RegCache.TryGetValue(key, out registered);

                // User is not cached so query db and store result.
                if (!containsKey)
                {
                    using (var db = new Database())
                    {
                        player = db.Players.FirstOrDefault(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                    }

                    registered = player != null;
                    RegCache[key] = registered;
                    return registered;
                }

                // Cached user is considered registered
                if (registered)
                {
                    using (var db = new Database())
                    {
                        player = db.Players.FirstOrDefault(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                    }

                    // This will refresh the registration status of the user
                    registered = player != null;

                    RegCache[key] = registered;
                    return registered;
                }

                // Cached user is not registered so do not populate.
                player = null;
                return false;
            }
        }

        /*
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
        }*/
    }
}