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
            Client.MessageReceived += MessageReceivedAsync;
        }

        private async Task MessageReceivedAsync(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message))
            {
                return;
            }

            await TryParseWebhookResponse(message);
        }

        public async Task TryParseWebhookResponse(SocketUserMessage message)
        {
            // Ensure method variables are configured first.
            if (PremiumConfig.DeletionWebhookChannel == null || PremiumConfig.DeletionWebhookClientId == null)
            {
                return;
            }

            // Ensure message is from the authorized webhook only.
            if (message.Author.Id != PremiumConfig.DeletionWebhookClientId)
            {
                return;
            }

            // Ensure message is sent in the authorized channel only.
            if (message.Channel.Id != PremiumConfig.DeletionWebhookChannel)
            {
                return;
            }

            try
            {
                var model = Premium.DeletionResponse.FromJson(message.Content);
                if (model == null) return;

                ulong? userId = null;

                // Try to parse main userId field, if not found, try to fallback to secondary
                if (!string.IsNullOrWhiteSpace(model.DiscordUserId))
                {
                    if (ulong.TryParse(model.DiscordUserId, out var uId))
                    {
                        userId = uId;
                    }
                }

                if (userId == null)
                {
                    if (!string.IsNullOrWhiteSpace(model.DiscordId))
                    {
                        if (ulong.TryParse(model.DiscordId, out var uId))
                        {
                            userId = uId;
                        }
                    }
                }

                // Parse failed or userId not found.
                if (userId == null)
                {
                    // TODO: Notify in webhook channel.
                    return;
                }

                if (!model.LastPaymentStatus.Equals("paid", StringComparison.InvariantCultureIgnoreCase))
                {
                    // TODO: Notify not parsed due to no successful payment made.
                    return;
                }

                var roleIdStrings = model.RoleIds.Split(",").Select(x => x.Trim()).ToArray();
                List<ulong> roleIds = new List<ulong>();
                foreach (var roleId in roleIdStrings)
                {
                    if (ulong.TryParse(roleId, out var rId))
                    {
                        roleIds.Add(rId);
                    }
                }

                if (roleIds.Count == 0)
                {
                    // TODO: Notify not parsed due to no roles available
                    return;
                }

                var paymentDate = model.LastPayment.UtcDateTime;
                var now = DateTime.UtcNow;
                if (paymentDate.Month != now.Month || paymentDate.Year != now.Year)
                {
                    // TODO: Notify not parsed due to payment being made outside of current payment period.
                    // Potentially still use this even if outside of current month for consistency.
                    return;
                }

                using (var db = new Database())
                {
                    var premiumRoles = db.PremiumRoles.ToArray();
                    var matched = new List<PremiumRole>();

                    // Find all users entitled premium roles.
                    foreach (var id in roleIds)
                    {
                        var match = premiumRoles.FirstOrDefault(x => x.RoleId == id);
                        if (match != null)
                        {
                            matched.Add(match);
                        }
                    }

                    if (matched.Count == 0)
                    {
                        // No entitled roles found? this shouldnt happen but could if the config changes.
                        return;
                    }

                    // This is the role the user should have for the tier they're currently paying for.
                    // This will be used to configure the user's registration limit for their servers for now.
                    var maxMatch = matched.OrderByDescending(x => x.Limit).First();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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

        private async Task DownloadMembers()
        {
            var _ = Task.Run(async () =>
            {
                var patreonGuild = Client.GetGuild(PremiumConfig.GuildId);
                if (patreonGuild == null) return;
                await patreonGuild.DownloadUsersAsync();
            });
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
                patreonGuild.DownloadUsersAsync();
                var patreonUser = patreonGuild?.GetUser(match.PremiumRedeemer.Value);
                if (patreonUser == null) return false;

                var patreonRole = GetPremiumRole(patreonUser);
                if (patreonRole == null)
                {
                    /*
                    if (match.PremiumBuffer != null && match.PremiumBuffer > DateTime.UtcNow)
                    {
                        return true;
                    }
                    */
                    return false;
                }

                /*
                if (match.BufferedPremiumCount != patreonRole.Limit)
                {
                    match.BufferedPremiumCount = patreonRole.Limit;
                    db.Update(match);
                    db.SaveChanges();
                }
                */
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
                    /*
                    if (match.PremiumBuffer != null && match.PremiumBuffer > DateTime.UtcNow)
                    {
                        return match.BufferedPremiumCount ?? PremiumConfig.DefaultLimit;
                    }
                    */
                    return PremiumConfig.DefaultLimit;
                }

                /*
                if (match.BufferedPremiumCount != patreonRole.Limit)
                {
                    match.BufferedPremiumCount = patreonRole.Limit;
                    db.Update(match);
                    db.SaveChanges();
                }
                */

                var allRedeemed = db.Competitions.Where(x => x.PremiumRedeemer == match.PremiumRedeemer).ToArray();
                int limit = patreonRole.Limit / allRedeemed.Length;

                return limit;
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
                        var oldClaimUser = patreonGuild.GetUser(config.PremiumRedeemer.Value);
                        if (oldClaimUser == null)
                        {
                            //Delete the claim.
                            await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)} was removed as they could not be found in the patreon server.");
                            config.PremiumRedeemer = null;
                        }
                        else
                        {
                            var oldClaimUserRole = GetPremiumRole(oldClaimUser);
                            if (oldClaimUserRole == null)
                            {
                                //User no longer is patron, delete claim.
                                await context.Channel.SendMessageAsync($"An old upgrade by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)} was removed as they no longer have a patreon role.");
                                config.PremiumRedeemer = null;
                            }
                            else
                            {
                                var oldUserClaims = db.Competitions.Where(x => x.PremiumRedeemer == config.PremiumRedeemer.Value).ToArray();
                                int oldLimit = oldClaimUserRole.Limit;
                                if (oldUserClaims.Length > 0)
                                {
                                    oldLimit = oldLimit / oldUserClaims.Length;
                                }

                                var newUserClaims = db.Competitions.Where(x => x.PremiumRedeemer == context.User.Id).ToArray();
                                int newLimit = oldClaimUserRole.Limit;
                                if (newUserClaims.Length > 0)
                                {
                                    newLimit = newLimit / newUserClaims.Length;
                                }

                                if (oldLimit > newLimit)
                                {
                                    //This is larger than the current that is trying to be redeemed, discard the one being redeemed.
                                    await context.Channel.SendMessageAsync($"There is already a license redeemed with a higher user count ({oldLimit}) in this server. Your upgrade will not be applied.");
                                    return;
                                }
                                else
                                {
                                    await context.Channel.SendMessageAsync($"Another smaller upgrade was applied to this server, it has been replaced. The original license was for {oldLimit} users and was redeemed by {MentionUtils.MentionUser(config.PremiumRedeemer.Value)}");

                                    //Delete the smaller claim.
                                    config.PremiumRedeemer = null;
                                }
                            }
                        }
                    }
                }

                var claims = db.Competitions.Where(x => x.PremiumRedeemer == context.User.Id).ToArray();

                // Use claims.length + 1 since current guild is not premium yet at this stage.
                int remaining = claims.Length > 0 ? claims.Length + 1 : 1;

                if (claims.Length >= PremiumConfig.ServerLimit)
                {
                    await context.Channel.SendMessageAsync($"You have already claimed the maximum amount of servers (`{PremiumConfig.ServerLimit}`) with this premium subscription, please remove one to continue.");
                    db.SaveChanges();
                    return;
                }

                config.PremiumRedeemer = context.User.Id;

                //config.PremiumBuffer = DateTime.UtcNow + TimeSpan.FromDays(28);
                //config.BufferedPremiumCount = currentRole.Limit;

                db.Update(config);

                if (remaining <= 1)
                {
                    await context.Channel.SendMessageAsync($"The server has been upgraded to `{currentRole.Limit}` users");
                }
                else
                {
                    await context.Channel.SendMessageAsync($"The server has been upgraded to `{currentRole.Limit / remaining}` users, the premium subscription for `{currentRole.Limit}` registrations is currently being split over `{remaining}` servers");
                }
                db.SaveChanges();
            }
        }

        public class Config
        {
            public bool Enabled { get; set; } = true;

            public ulong GuildId { get; set; }

            public int DefaultLimit { get; set; } = 20;

            public int LobbyLimit { get; set; } = 3;

            public int ServerLimit { get; set; } = 3;

            public ulong? DeletionWebhookChannel { get; set; } = null;

            public ulong? DeletionWebhookClientId { get; set; } = null;

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