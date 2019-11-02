using Discord.WebSocket;
using ELO.EF;
using ELO.Models;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ELO.Services
{
    public class PremiumService
    {
        public PremiumService(ConfigManager configManager, DiscordShardedClient client)
        {
            Client = client;
            PremiumConfig = configManager.LastConfig.GetConfig<Config>("PremiumConfig") ?? new Config();
        }

        public DiscordShardedClient Client { get; }
        public Config PremiumConfig { get; }

        private PremiumRole GetPremiumRole(SocketGuildUser patreonUser)
        {
            var userRole = PremiumConfig.Roles.OrderByDescending(x => x.Limit).FirstOrDefault(x => patreonUser.Roles.Any(r => r.Id == x.RoleId));
            return userRole;
        }

        public int GetRegistrationLimit(ulong guildId)
        {
            using (var db = new Database())
            {
                var match = db.Competitions.Find(guildId);
                if (match == null) return PremiumConfig.DefaultLimit;
                if (match.LegacyPremiumExpiry != null && match.LegacyPremiumExpiry > DateTime.UtcNow)
                {
                    return int.MaxValue;
                }

                if (match.PremiumRedeemer == null)
                {
                    return PremiumConfig.DefaultLimit;
                }

                var patreonGuild = Client.GetGuild(PremiumConfig.GuildId);
                var patreonUser = patreonGuild?.GetUser(match.PremiumRedeemer.Value);
                if (patreonUser == null) return PremiumConfig.DefaultLimit;

                var patreonRole = GetPremiumRole(patreonUser);
                if (patreonRole == null) return PremiumConfig.DefaultLimit;

                return patreonRole.Limit;
            }
        }

        public class Config
        {
            public ulong GuildId { get; set; }

            public int DefaultLimit { get; set; } = 20;

            public string ServerInvite { get; set; }
            public string AltLink { get; set; }

            public List<PremiumRole> Roles { get; set; } = new List<PremiumRole>();
        }

        public class PremiumRole
        {
            public ulong RoleId { get; set; }
            public int Limit { get; set; }
        }
    }
}
