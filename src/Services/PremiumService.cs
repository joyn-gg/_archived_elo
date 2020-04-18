using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class PremiumService
    {
        public PremiumService(DiscordShardedClient client, Config config)
        {
            Client = client;
            PremiumConfig = config;
        }

        public DiscordShardedClient Client { get; }

        public Config PremiumConfig { get; }

        private PremiumRole GetPremiumRole(SocketGuildUser patreonUser)
        {
            using (var db = new Database())
            {
                var roles = db.PremiumRoles.ToArray();
                var userRole = roles.OrderByDescending(x => x.Limit).FirstOrDefault(x => patreonUser.Roles.Any(r => r.Id == x.RoleId));
                return userRole;
            }
        }

        public bool IsPremium(ulong guildId)
        {
            if (!PremiumConfig.Enabled) return true;
            using (var db = new Database())
            {
                var match = db.Competitions.Find(guildId);
                if (match == null) return false;
                if (match.LegacyPremiumExpiry != null && match.LegacyPremiumExpiry > DateTime.UtcNow)
                {
                    return true;
                }

                if (match.PremiumRedeemer == null)
                {
                    return false;
                }

                var patreonGuild = Client.GetGuild(PremiumConfig.GuildId);
                var patreonUser = patreonGuild?.GetUser(match.PremiumRedeemer.Value);
                if (patreonUser == null) return false;

                var patreonRole = GetPremiumRole(patreonUser);
                if (patreonRole == null)
                {
                    if (match.PremiumBuffer != null && match.PremiumBuffer > DateTime.UtcNow)
                    {
                        return true;
                    }

                    return false;
                }

                if (match.BufferedPremiumCount != patreonRole.Limit)
                {
                    match.BufferedPremiumCount = patreonRole.Limit;
                    db.Update(match);
                    db.SaveChanges();
                }
                return true;
            }
        }

        public int GetRegistrationLimit(ulong guildId)
        {
            if (!PremiumConfig.Enabled) return int.MaxValue;
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
                if (patreonRole == null)
                {
                    if (match.PremiumBuffer != null && match.PremiumBuffer > DateTime.UtcNow)
                    {
                        return match.BufferedPremiumCount ?? PremiumConfig.DefaultLimit;
                    }
                    return PremiumConfig.DefaultLimit;
                }

                if (match.BufferedPremiumCount != patreonRole.Limit)
                {
                    match.BufferedPremiumCount = patreonRole.Limit;
                    db.Update(match);
                    db.SaveChanges();
                }
                return patreonRole.Limit;
            }
        }

        public virtual async Task Claim(ShardedCommandContext context)
        {
            using (var db = new Database())
            {
                var patreonGuild = context.Client.GetGuild(PremiumConfig.GuildId);
                if (patreonGuild == null)
                {
                    await context.Channel.SendMessageAsync("Unable to access patreon guild.");
                    return;
                }

                var patreonUser = patreonGuild.GetUser(context.User.Id);
                if (patreonUser == null)
                {
                    await context.Channel.SendMessageAsync($"You must join the premium server {PremiumConfig.ServerInvite} and get a patreon role {PremiumConfig.AltLink} before being able to claim an upgrade.");
                    return;
                }

                var currentRole = GetPremiumRole(patreonUser);
                if (currentRole == null)
                {
                    await context.Channel.SendMessageAsync($"You do not have a patreon role, you can receive one by becoming a patron at {PremiumConfig.AltLink}");
                    return;
                }

                var config = db.Competitions.Find(context.Guild.Id);
                if (config != null)
                {
                    if (config.PremiumRedeemer == context.User.Id)
                    {
                        await context.Channel.SendMessageAsync("You've already claimed premium in this server.");
                        return;
                    }
                    else if (config.PremiumRedeemer != null)
                    {
                        //Run checks and compare new vs old redeemer
                        var guildClaimUser = patreonGuild.GetUser(config.PremiumRedeemer.Value);
                        if (guildClaimUser == null)
                        {
                            //Delete the claim.
                            await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)} was removed as they could not be found in the patreon server.");
                            config.PremiumRedeemer = null;
                        }
                        else
                        {
                            var guildClaimUserRole = GetPremiumRole(guildClaimUser);
                            if (guildClaimUserRole == null)
                            {
                                //User no longer is patron, delete claim.
                                await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)} was removed as they no longer have a patreon role.");
                                config.PremiumRedeemer = null;
                            }
                            else
                            {
                                if (guildClaimUserRole.Limit > currentRole.Limit)
                                {
                                    //This is larger than the current that is trying to be redeemed, discard the one being redeemed.
                                    await context.Channel.SendMessageAsync($"There is already a license redeemed with a higher user count ({guildClaimUserRole.Limit}) in this server. Your upgrade will not be applied.");
                                    return;
                                }
                                else
                                {
                                    await context.Channel.SendMessageAsync($"Another smaller upgrade was applied to this server, it has been replaced. The original license was for {guildClaimUserRole.Limit} users and was redeemed by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)}");

                                    //Delete the smaller claim.
                                    config.PremiumRedeemer = null;
                                }
                            }
                        }
                    }
                }

                var prevClaims = db.Competitions.Where(x => x.PremiumRedeemer == context.User.Id);
                foreach (var claim in prevClaims)
                {
                    claim.PremiumRedeemer = null;
                    claim.PremiumBuffer = null;
                    claim.BufferedPremiumCount = null;
                    await context.Channel.SendMessageAsync("You've already claimed a server, the old claim will be removed and applied to this server.");
                }

                db.UpdateRange(prevClaims);
                config.PremiumRedeemer = context.User.Id;
                config.PremiumBuffer = DateTime.UtcNow + TimeSpan.FromDays(28);
                config.BufferedPremiumCount = currentRole.Limit;

                db.Update(config);
                await context.Channel.SendMessageAsync($"The server has been upgraded to {currentRole.Limit} users");
                db.SaveChanges();
            }
        }

        public class Config
        {
            public bool Enabled { get; set; } = true;

            public ulong GuildId { get; set; }

            public int DefaultLimit { get; set; } = 20;

            public int LobbyLimit { get; set; } = 3;

            public string ServerInvite { get; set; }

            public string AltLink { get; set; }

            //public List<PremiumRole> Roles { get; set; } = new List<PremiumRole>();
        }

        public class PremiumRole
        {
            public ulong RoleId { get; set; }

            public int Limit { get; set; }
        }
    }
}