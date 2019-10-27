using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Methods.Migrations;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Premium;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    [Preconditions.RequireAdmin]
    public class CompetitionSetup : ReactiveBase
    {
        public ELOService Service { get; }
        public GuildService Prefix { get; }
        public PatreonIntegration PatreonIntegration { get; }
        public ELOMigrator Migrator { get; }
        public LegacyIntegration Legacy { get; }

        public CompetitionSetup(ELOService service, GuildService prefix, PatreonIntegration patreonIntegration, ELOMigrator migrator, LegacyIntegration legacy)
        {
            this.Prefix = prefix;
            PatreonIntegration = patreonIntegration;
            Migrator = migrator;
            Legacy = legacy;
            Service = service;
        }

        [Command("ClaimPremium", RunMode = RunMode.Sync)]
        [Summary("Claim a patreon premium subscription")]
        public async Task ClaimPremiumAsync()
        {
            await PatreonIntegration.Claim(Context);
        }

        [Command("RedeemLegacyToken", RunMode = RunMode.Sync)]
        [Summary("Redeem a 16 digit token for the old version of ELO")]
        public async Task RedeemLegacyTokenAsync([Remainder]string token = null)
        {
            if (token == null)
            {
                await ReplyAsync("This is used to redeem tokens that were created using the old ELO version.");
                return;
            }

            if (Migrator.RedeemToken(Context.Guild.Id, token))
            {
                await ReplyAsync("Token redeemed.");
            }
            else
            {
                await ReplyAsync("Invalid token provided.");
            }
        }

        [Command("LegacyExpiration", RunMode = RunMode.Sync)]
        [Summary("Displays the expiry date of any legacy subscription")]
        public async Task LegacyExpirationAsync()
        {
            var config = Legacy.GetPremiumConfig(Context.Guild.Id);
            if (config != null)
            {
                if (config.IsPremium())
                {
                    await ReplyAsync($"Expires on: {config.ExpiryDate.ToString("dd MMM yyyy")} {config.ExpiryDate.ToShortTimeString()}\nRemaining: {config.Remaining().GetReadableLength()}");
                }
                else
                {
                    await ReplyAsync("Legacy premium has already expired.");
                }
            }
            else
            {
                await ReplyAsync("This server does not have a legacy premium subscription.");
            }
        }

        [Command("RegistrationLimit", RunMode = RunMode.Async)]
        [Summary("Displays the maximum amount of registrations for the server")]
        public async Task GetRegisterLimit()
        {
            await ReplyAsync($"Current registration limit is a maximum of: {PatreonIntegration.GetRegistrationLimit(Context)}");
        }

        [Command("CompetitionInfo", RunMode = RunMode.Async)]
        [Alias("CompetitionSettings", "GameSettings")]
        [Summary("Displays information about the current servers competition settings")]
        public async Task CompetitionInfo()
        {
            var comp = Service.GetOrCreateCompetition(Context.Guild.Id);
            var infoStr = $"**Register Role:** {(comp.RegisteredRankId == 0 ? "N/A" : MentionUtils.MentionRole(comp.RegisteredRankId))}\n" +
                        $"**Admin Role:** {(comp.AdminRole == 0 ? "N/A" : MentionUtils.MentionRole(comp.AdminRole))}\n" +
                        $"**Moderator Role:** {(comp.ModeratorRole == 0 ? "N/A" : MentionUtils.MentionRole(comp.ModeratorRole))}\n" +
                        $"**Update Nicknames:** {comp.UpdateNames}\n" +
                        $"**Nickname Format:** {comp.NameFormat}\n" +
                        $"**Block Multiqueuing:** {comp.BlockMultiQueueing}\n" +
                        $"**Allow Negative Score:** {comp.AllowNegativeScore}\n" +
                        $"**Default Loss Amount:** -{comp.DefaultLossModifier}\n" +
                        $"**Default Win Amount:** +{comp.DefaultWinModifier}\n" +
                        $"**Allow Self Rename:** {comp.AllowSelfRename}\n" +
                        $"**Allow Re-registering:** {comp.AllowReRegister}\n" +
                        $"**Registered User Count:** {comp.RegistrationCount}\n" +
                        $"**Manual Game Count:** {comp.ManualGameCounter}\n" +
                        $"For rank info use the `ranks` command";
            await SimpleEmbedAsync(infoStr);
        }

        [Command("SetRegisterRole", RunMode = RunMode.Sync)]
        [Alias("Set RegisterRole", "RegisterRole")]
        [Summary("Sets or displays the current register role")]
        public async Task SetRegisterRole([Remainder] IRole role = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (role == null)
            {
                if (competition.RegisteredRankId != 0)
                {
                    var gRole = Context.Guild.GetRole(competition.RegisteredRankId);
                    if (gRole == null)
                    {
                        //Rank previously set but can no longer be found (deleted)
                        //May as well reset it.
                        competition.RegisteredRankId = 0;
                        Service.SaveCompetition(competition);
                        await ReplyAsync("Register role had previously been set but can no longer be found in the server. It has been reset.");
                    }
                    else
                    {
                        await ReplyAsync($"Current register role is: {gRole.Mention}");
                    }
                }
                else
                {
                    var serverPrefix = Prefix.GetPrefix(Context.Guild.Id) ?? Prefix.DefaultPrefix;
                    await ReplyAsync($"There is no register role set. You can set one with `{serverPrefix}SetRegisterRole @role` or `{serverPrefix}SetRegisterRole rolename`");
                }

                return;
            }

            competition.RegisteredRankId = role.Id;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Register role set to {role.Mention}");
        }

        [Command("SetRegisterMessage", RunMode = RunMode.Sync)]
        [Alias("Set RegisterMessage", "RegisterMessage")]
        [Summary("Sets the message shown to users when they register")]
        public async Task SetRegisterMessageAsync([Remainder] string message = null)
        {
            if (message == null)
            {
                message = "You have registered as `{name}`, all roles/name updates have been applied if applicable.";
            }
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.RegisterMessageTemplate = message;
            var testProfile = new Player(0, 0, "Player");
            testProfile.Wins = 5;
            testProfile.Losses = 2;
            testProfile.Draws = 1;
            testProfile.Points = 600;
            var exampleNick = competition.GetNickname(testProfile);

            Service.SaveCompetition(competition);
            await ReplyAsync($"Register Message set.\nExample:\n{exampleNick}");
        }

        [Command("RegisterMessageFormats", RunMode = RunMode.Async)]
        [Alias("RegisterFormats")]
        [Summary("Shows replacements that can be used in the register message")]
        public async Task ShowRegistrationFormatsAsync()
        {
            var response = "**Register Message Formats**\n" + // Use Title
                "{score} - Total points\n" +
                "{name} - Registration name\n" +
                "{wins} - Total wins\n" +
                "{draws} - Total draws\n" +
                "{losses} - Total losses\n" +
                "{games} - Games played\n\n" +
                "Example:\n" +
                "`RegisterMessageFormats Thank you for registering {name}` `Thank you for registering Player`\n" +
                "NOTE: Format is limited to 1024 characters long";

            await SimpleEmbedAsync(response);
        }

        [Command("SetNicknameFormat", RunMode = RunMode.Sync)]
        [Alias("Set NicknameFormat", "NicknameFormat", "NameFormat", "SetNameFormat")]
        [Summary("Sets how user nicknames are formatted")]
        public async Task SetNicknameFormatAsync([Remainder] string format)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.NameFormat = format;
            var testProfile = new Player(0, 0, "Player");
            testProfile.Wins = 5;
            testProfile.Losses = 2;
            testProfile.Draws = 1;
            testProfile.Points = 600;
            var exampleNick = competition.GetNickname(testProfile);

            Service.SaveCompetition(competition);
            await ReplyAsync($"Nickname Format set.\nExample: `{exampleNick}`");
        }

        [Command("NicknameFormats", RunMode = RunMode.Async)]
        [Alias("NameFormats")]
        [Summary("Shows replacements that can be used in the user nickname formats")]
        public async Task ShowNicknameFormatsAsync()
        {
            var response = "**NickNameFormats**\n" + // Use Title
                "{score} - Total points\n" +
                "{name} - Registration name\n" +
                "{wins} - Total wins\n" +
                "{draws} - Total draws\n" +
                "{losses} - Total losses\n" +
                "{games} - Games played\n\n" +
                "Examples:\n" +
                "`SetNicknameFormat {score} - {name}` `1000 - Player`\n" +
                "`SetNicknameFormat [{wins}] {name}` `[5] Player`\n" +
                "NOTE: Nicknames are limited to 32 characters long on discord";

            await ReplyAsync("", false, response.QuickEmbed());
        }

        [Command("AddRank", RunMode = RunMode.Sync)]
        [Alias("Add Rank", "UpdateRank")]
        [Summary("Adds a new rank with the specified amount of points")]
        public async Task AddRank(IRole role, int points)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.Ranks = competition.Ranks.Where(x => x.RoleId != role.Id).ToList();
            competition.Ranks.Add(new Rank
            {
                RoleId = role.Id,
                    Points = points
            });
            Service.SaveCompetition(competition);
            await ReplyAsync("Rank added.");
        }

        [Command("AddRank", RunMode = RunMode.Sync)]
        [Alias("Add Rank", "UpdateRank")]
        [Summary("Adds a new rank with the specified amount of points")]
        public async Task AddRank(int points, IRole role)
        {
            await AddRank(role, points);
        }

        [Command("RemoveRank", RunMode = RunMode.Sync)]
        [Alias("Remove Rank", "DelRank")]
        [Summary("Removes a rank based of the role's id")]
        public async Task RemoveRank(ulong roleId)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            competition.Ranks = competition.Ranks.Where(x => x.RoleId != roleId).ToList();
            Service.SaveCompetition(competition);
            await ReplyAsync("Rank Removed.");
        }

        [Command("RemoveRank", RunMode = RunMode.Sync)]
        [Alias("Remove Rank", "DelRank")]
        [Summary("Removes a rank")]
        public async Task RemoveRank(IRole role)
        {
            await RemoveRank(role.Id);
        }

        [Command("AllowNegativeScore", RunMode = RunMode.Sync)]
        [Alias("AllowNegative")]
        [Summary("Sets whether negative scores are allowed")]
        public async Task AllowNegativeAsync(bool? allowNegative = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (allowNegative == null)
            {
                await ReplyAsync($"Current Allow Negative Score Setting: {competition.AllowNegativeScore}");
                return;
            }
            competition.AllowNegativeScore = allowNegative.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Allow Negative Score set to {allowNegative.Value}");
        }

        [Command("AllowReRegister", RunMode = RunMode.Sync)]
        [Summary("Sets whether users are allowed to run the register command multiple times")]
        public async Task AllowReRegisterAsync(bool? reRegister = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (reRegister == null)
            {
                await ReplyAsync($"Current Allow re-register Setting: {competition.AllowReRegister}");
                return;
            }
            competition.AllowReRegister = reRegister.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Allow re-register set to {reRegister.Value}");
        }
                
        [Command("AllowSelfRename", RunMode = RunMode.Sync)]
        [Summary("Sets whether users are allowed to use the rename command")]
        public async Task AllowSelfRenameAsync(bool? selfRename = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (selfRename == null)
            {
                await ReplyAsync($"Current Allow Self Rename Setting: {competition.AllowSelfRename}");
                return;
            }
            competition.AllowSelfRename = selfRename.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Allow Self Rename set to {selfRename.Value}");
        }

        [Command("DefaultWinModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the default amount of points users can earn when winning.")]
        public async Task CompWinModifier(int? amountToAdd = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            if (!amountToAdd.HasValue)
            {
                await ReplyAsync($"Current DefaultWinModifier Setting: {competition.DefaultWinModifier}");
                return;
            }
            competition.DefaultWinModifier = amountToAdd.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Default Win Modifier set to {competition.DefaultWinModifier}");
        }

        
        [Command("DefaultLossModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the default amount of points users lose when the lose a game.")]
        public async Task CompLossModifier(int? amountToSubtract = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            
            if (!amountToSubtract.HasValue)
            {
                await ReplyAsync($"Current DefaultLossModifier Setting: {competition.DefaultLossModifier}");
                return;
            }
            competition.DefaultLossModifier = amountToSubtract.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Default Loss Modifier set to {competition.DefaultLossModifier}");
        }

        [Command("RankLossModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the amount of points lost for a user with the specified rank.")]
        public async Task RankLossModifier(IRole role, int? amountToSubtract = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var rank = competition.Ranks.FirstOrDefault(x => x.RoleId == role.Id);
            if (rank == null)
            {
                await ReplyAsync("Provided role is not a rank.");
                return;
            }

            rank.LossModifier = amountToSubtract;
            Service.SaveCompetition(competition);
            if (!amountToSubtract.HasValue)
            {
                await ReplyAsync($"This rank will now use the server's default loss value (-{competition.DefaultLossModifier}) when subtracting points.");
            }
            else
            {
                await ReplyAsync($"When a player with this rank loses they will lose {amountToSubtract} points");
            }
        }

        [Command("RankWinModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the amount of points lost for a user with the specified rank.")]
        public async Task RankWinModifier(IRole role, int? amountToAdd = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var rank = competition.Ranks.FirstOrDefault(x => x.RoleId == role.Id);
            if (rank == null)
            {
                await ReplyAsync("Provided role is not a rank.");
                return;
            }

            rank.WinModifier = amountToAdd;
            Service.SaveCompetition(competition);
            if (!amountToAdd.HasValue)
            {
                await ReplyAsync($"This rank will now use the server's default win value (+{competition.DefaultWinModifier}) when adding points.");
            }
            else
            {
                await ReplyAsync($"When a player with this rank wins they will gain {amountToAdd} points");
            }
        }

        [Command("UpdateNicknames", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will update user nicknames.")]
        public async Task UpdateNicknames(bool? updateNicknames = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (updateNicknames == null)
            {
                await ReplyAsync($"Current Update Nicknames Setting: {competition.UpdateNames}");
                return;
            }
            competition.UpdateNames = updateNicknames.Value;
            Service.SaveCompetition(competition);
            await ReplyAsync($"Update Nicknames set to {competition.UpdateNames}");
        }

        
        [Command("CreateReactionRegistration", RunMode = RunMode.Sync)]
        [Summary("Creates a message which users can react to in order to register")]
        public async Task CreateReactAsync([Remainder]string message = null)
        {
            var config = Service.GetReactiveRegistrationMessage(Context.Guild.Id);
            if (config == null)
            {
                config = new ELOService.ReactiveRegistrationMessage();
                config.GuildId = Context.Guild.Id;
            }

            var response = await SimpleEmbedAsync(message);
            config.MessageId = response.Id;
            Service.SaveReactiveRegistrationMessage(config);
            await response.AddReactionAsync(Service.registrationConfirmEmoji);
        }
    }
}