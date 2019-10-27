using System.Collections.Generic;
using Discord.WebSocket;
using RavenBOT.Common;
using System.Linq;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;

namespace RavenBOT.ELO.Modules.Premium
{
    public class PatreonIntegration : IServiceable
    {
        public IDatabase Database { get; }
        public LegacyIntegration Legacy { get; }

        public PatreonIntegration(IDatabase database, LegacyIntegration legacy)
        {
            Database = database;
            Legacy = legacy;
        }

        private PatreonConfig lastConfig = null;

        public void SaveConfig(PatreonConfig config)
        {
            lastConfig = config;
            Database.Store(config, PatreonConfig.DocumentName());
        }

        public PatreonConfig GetConfig()
        {
            if (lastConfig != null) return lastConfig;
            return Database.Load<PatreonConfig>(PatreonConfig.DocumentName()) ?? new PatreonConfig();
        }

        private KeyValuePair<ulong, PatreonConfig.ELORole> GetPremiumRole(SocketGuildUser patreonUser)
        {
            var config = GetConfig();
            var userRole = config.Roles.OrderByDescending(x => x.Value.MaxRegistrationCount).FirstOrDefault(x => patreonUser.Roles.Any(r => r.Id == x.Key));
            return userRole;
        }

        public int GetRegistrationLimit(DiscordShardedClient client, SocketGuild guild)
        {
            var config = GetConfig();
            if (!config.Enabled) return int.MaxValue;

            //Add legacy support
            var legacyUpgrade = Legacy.GetPremiumConfig(guild.Id);
            if (legacyUpgrade != null)
            {
                if (legacyUpgrade.IsPremium())
                {
                    return int.MaxValue;
                }
            }

            var guildUpgrade = Database.Load<ClaimProfile>(ClaimProfile.DocumentName(guild.Id));
            if (guildUpgrade == null) return config.DefaultRegistrationLimit;

            var patreonGuild = client.GetGuild(config.GuildId); 
            var patreonUser = patreonGuild?.GetUser(guildUpgrade.UserId);
            if (patreonUser == null) return config.DefaultRegistrationLimit;

            var patreonRole = GetPremiumRole(patreonUser);
            if (patreonRole.Value == null) return config.DefaultRegistrationLimit;

            return patreonRole.Value.MaxRegistrationCount;
        }

        public int GetRegistrationLimit(ShardedCommandContext context)
        {
            return GetRegistrationLimit(context.Client, context.Guild);
        }

        public async Task Claim(ShardedCommandContext context)
        {            
            //Assumed context, claim is being applied to the server where it's being claimed in
            //TODO: Check if this fetches user from cache.
            var config = GetConfig();
            if (!config.Enabled) return;

            var patreonGuild = context.Client.GetGuild(config.GuildId);
            if (patreonGuild == null)
            {
                await context.Channel.SendMessageAsync("Unable to access patreon guild.");
                return;
            }

            var patreonUser = patreonGuild.GetUser(context.User.Id);
            if (patreonUser == null)
            {
                await context.Channel.SendMessageAsync($"You must join the premium server {config.ServerInvite} and get a patreon role {config.PageUrl} before being able to claim an upgrade.");
                return;
            }

            var currentRole = GetPremiumRole(patreonUser);
            if (currentRole.Value == null)
            {
                await context.Channel.SendMessageAsync("You do not have a patreon role.");
                return;   
            }

            var guildLicense = Database.Load<ClaimProfile>(ClaimProfile.DocumentName(context.Guild.Id));
            if (guildLicense != null)
            {
                if (guildLicense.UserId == context.User.Id)
                {
                    await context.Channel.SendMessageAsync("You've already claimed premium in this server.");
                    return;
                }
                else
                {
                    var guildClaimUser = patreonGuild.GetUser(guildLicense.UserId);
                    if (guildClaimUser == null)
                    {
                        //Delete the claim.
                        await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(guildLicense.UserId)} was removed as they could not be found in the patreon server.");
                        Database.Remove<ClaimProfile>(ClaimProfile.DocumentName(guildLicense.GuildId));
                    }
                    else
                    {
                        var guildClaimUserRole = GetPremiumRole(guildClaimUser);
                        if (guildClaimUserRole.Value == null)
                        {
                            //User no longer is patron, delete claim.
                            await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(guildLicense.UserId)} was removed as they no longer have a patreon role.");
                            Database.Remove<ClaimProfile>(ClaimProfile.DocumentName(guildLicense.GuildId));
                        }
                        else
                        {
                            if (guildClaimUserRole.Value.MaxRegistrationCount > currentRole.Value.MaxRegistrationCount)
                            {
                                //This is larger than the current that is trying to be redeemed, discard the one being redeemed.
                                await context.Channel.SendMessageAsync($"There is already a license redeemed with a higher user count ({guildClaimUserRole.Value.MaxRegistrationCount}) in this server. Your upgrade will not be applied.");
                                return;
                            }
                            else
                            {
                                await context.Channel.SendMessageAsync($"Another smaller upgrade was applied to this server, it has been replaced. The original license was for {guildClaimUserRole.Value.MaxRegistrationCount} users and was redeemed by {MentionUtils.MentionUser(guildLicense.UserId)}");
                                //Delete the smaller claim.
                                Database.Remove<ClaimProfile>(ClaimProfile.DocumentName(guildLicense.GuildId));
                            }
                        }
                    }
                }
            }

            var previousClaims = Database.Query<ClaimProfile>(x => x.UserId == context.User.Id);
            foreach (var claim in previousClaims)
            {
                Database.Remove<ClaimProfile>(ClaimProfile.DocumentName(claim.GuildId));
                await context.Channel.SendMessageAsync("You've already claimed a server, the old claim will be removed and applied to this server.");
            }

            var userLicense = new ClaimProfile
            {
                GuildId = context.Guild.Id,
                UserId = context.User.Id
            };

            Database.Store(userLicense, ClaimProfile.DocumentName(context.Guild.Id));
            await context.Channel.SendMessageAsync($"The server has been upgraded to {currentRole.Value.MaxRegistrationCount} users");
        }

        // Config for defining user allowed counts based on roles of users
        public class PatreonConfig
        {
            public static string DocumentName() => "PatreonConfig";


            public class ELORole
            {
                public int MaxRegistrationCount { get; set; }
                public ulong RoleId { get; set; }
            }

            public Dictionary<ulong, ELORole> Roles { get; set; } = new Dictionary<ulong, ELORole>();
            //public ulong BaseRole { get; set; }
            public ulong GuildId { get; set; }
            public int DefaultRegistrationLimit { get; set; } = 20;
            public bool Enabled { get; set; } = true;
            public string ServerInvite { get; set; }
            public string PageUrl { get; set; }
        }

        public class ClaimProfile
        {            
            public static string DocumentName(ulong guildId)
            {
                return $"ClaimProfile-{guildId}";
            }

            public ulong UserId { get; set; }
            public ulong GuildId { get; set; }
        }
    }
}