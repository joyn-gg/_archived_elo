using System;
using System.Linq;
using RavenBOT.Common;

namespace RavenBOT.ELO.Modules.Premium
{
    public class LegacyIntegration : IServiceable
    {
        public LegacyIntegration(IDatabase database)
        {
            Database = database;
        }

        private IDatabase Database { get; }

        public LegacyPremium GetPremiumConfig(ulong guildId)
        {
            return Database.Load<LegacyPremium>(LegacyPremium.DocumentName(guildId));
        }

        public DateTime GetLatestExpiryDate()
        {
            var items = Database.Query<LegacyPremium>();
            var max = items.Max(x => x.ExpiryDate);
            return max;
        }

        public void SaveConfig(LegacyPremium config)
        {
            Database.Store<LegacyPremium>(config, LegacyPremium.DocumentName(config.GuildId));
        }

        public class LegacyPremium
        {
            public static string DocumentName(ulong guildId)
            {
                return $"LegacyPremium-{guildId}";
            }

            public ulong GuildId { get; set; }
            public DateTime ExpiryDate { get; set; }

            public bool IsPremium()
            {
                return ExpiryDate > DateTime.UtcNow;
            }

            public TimeSpan Remaining()
            {
                return ExpiryDate - DateTime.UtcNow;
            }
        }
    }
}