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

        public static void ClearUserCache(ulong guildId, ulong userId)
        {
            lock (cacheLock)
            {
                var key = (guildId, userId);
                if (RegCache.ContainsKey(key))
                {
                    RegCache.Remove(key);
                }
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
    }
}