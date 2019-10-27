using Discord;
using Discord.Commands;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Methods.Migrations;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Premium;
using System.Linq;
using System.Threading.Tasks;

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
                await SimpleEmbedAsync("This is used to redeem tokens that were created using the old ELO version.", Color.Blue);
                return;
            }

            if (Migrator.RedeemToken(Context.Guild.Id, token))
            {
                await SimpleEmbedAsync("Token redeemed.", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync($"Invalid token provided, if you believe this is a mistake please contact support at: {PatreonIntegration.GetConfig().ServerInvite}", Color.Red);
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
                    await SimpleEmbedAsync($"Expires on: {config.ExpiryDate.ToString("dd MMM yyyy")} {config.ExpiryDate.ToShortTimeString()}\nRemaining: {config.Remaining().GetReadableLength()}", Color.Blue);
                }
                else
                {
                    await SimpleEmbedAsync("Legacy premium has already expired.", Color.Red);
                }
            }
            else
            {
                await SimpleEmbedAsync("This server does not have a legacy premium subscription.", Color.Red);
            }
        }

        [Command("RegistrationLimit", RunMode = RunMode.Async)]
        [Summary("Displays the maximum amount of registrations for the server")]
        public async Task GetRegisterLimit()
        {
            await SimpleEmbedAsync($"Current registration limit is a maximum of: {PatreonIntegration.GetRegistrationLimit(Context)}", Color.Blue);
        }

        [Command("CompetitionInfo", RunMode = RunMode.Async)]
        [Alias("CompetitionSettings", "GameSettings")]
        [Summary("Displays information about the current servers competition settings")]
        public async Task CompetitionInfo()
        {
            var comp = Service.GetOrCreateCompetition(Context.Guild.Id);
            var embed = new EmbedBuilder
            {
                Color = Color.Blue
            };
            embed.AddField("Roles", 
                        $"**Register Role:** {(comp.RegisteredRankId == 0 ? "N/A" : MentionUtils.MentionRole(comp.RegisteredRankId))}\n" +
                        $"**Admin Role:** {(comp.AdminRole == 0 ? "N/A" : MentionUtils.MentionRole(comp.AdminRole))}\n" +
                        $"**Moderator Role:** {(comp.ModeratorRole == 0 ? "N/A" : MentionUtils.MentionRole(comp.ModeratorRole))}");
            embed.AddField("Options",
                        $"**Block Multiqueuing:** {comp.BlockMultiQueueing}\n" +
                        $"**Allow Negative Score:** {comp.AllowNegativeScore}\n" +
                        $"**Update Nicknames:** {comp.UpdateNames}\n" +
                        $"**Allow Self Rename:** {comp.AllowSelfRename}\n" +
                        $"**Allow Re-registering:** {comp.AllowReRegister}");
            embed.AddField("Stats", 
                        $"**Registered User Count:** {comp.RegistrationCount}\n" +
                        $"**Manual Game Count:** {comp.ManualGameCounter}");
            embed.AddField("Formatting", $"**Nickname Format:** {comp.NameFormat}\n" +
                        $"**Registration Message:** {comp.RegisterMessageTemplate}");
            embed.AddField("Rank Info",
            $"**Default Loss Amount:** -{comp.DefaultLossModifier}\n" +
            $"**Default Win Amount:** +{comp.DefaultWinModifier}\n" +
            $"For rank info use the `ranks` command");
            await ReplyAsync(embed);
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
                        await SimpleEmbedAsync("Register role had previously been set but can no longer be found in the server. It has been reset.", Color.DarkBlue);
                    }
                    else
                    {
                        await SimpleEmbedAsync($"Current register role is: {gRole.Mention}", Color.Blue);
                    }
                }
                else
                {
                    var serverPrefix = Prefix.GetPrefix(Context.Guild.Id) ?? Prefix.DefaultPrefix;
                    await SimpleEmbedAsync($"There is no register role set. You can set one with `{serverPrefix}SetRegisterRole @role` or `{serverPrefix}SetRegisterRole rolename`", Color.Blue);
                }

                return;
            }

            competition.RegisteredRankId = role.Id;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Register role set to {role.Mention}", Color.Green);
        }

        [Command("SetRegisterMessage", RunMode = RunMode.Sync)]
        [Alias("Set RegisterMessage")]
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
            var exampleRegisterMessage = competition.FormatRegisterMessage(testProfile);

            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Register Message set.\nExample:\n{exampleRegisterMessage}", Color.Green);
        }

        [Command("RegisterMessage", RunMode = RunMode.Async)]
        [Summary("Displays the current register message for the server")]
        public async Task ShowRegisterMessageAsync()
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var testProfile = new Player(0, 0, "Player");
            testProfile.Wins = 5;
            testProfile.Losses = 2;
            testProfile.Draws = 1;
            testProfile.Points = 600;

            Service.SaveCompetition(competition);
            var response = new EmbedBuilder
            {
                Color = Color.Blue
            };

            if (!string.IsNullOrWhiteSpace(competition.RegisterMessageTemplate))
            {
                response.AddField("Unformatted Message", competition.RegisterMessageTemplate);
                response.AddField("Example Message", competition.FormatRegisterMessage(testProfile));
                await ReplyAsync(response);
                return;
            }

            await SimpleEmbedAsync($"This server does not have a register message set.", Color.DarkBlue);
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

            await SimpleEmbedAsync(response, Color.Blue);
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
            await SimpleEmbedAsync($"Nickname Format set.\nExample: `{exampleNick}`", Color.Green);
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

            await SimpleEmbedAsync(response, Color.Blue);
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
            await SimpleEmbedAsync("Rank added, if you wish to change the win/loss point values, use the `RankWinModifier` and `RankLossModifier` commands.", Color.Green);
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
            await SimpleEmbedAsync("Rank Removed.", Color.Green);
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
                await SimpleEmbedAsync($"Current Allow Negative Score Setting: {competition.AllowNegativeScore}", Color.Blue);
                return;
            }
            competition.AllowNegativeScore = allowNegative.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Allow Negative Score set to {allowNegative.Value}", Color.Green);
        }

        [Command("AllowReRegister", RunMode = RunMode.Sync)]
        [Summary("Sets whether users are allowed to run the register command multiple times")]
        public async Task AllowReRegisterAsync(bool? reRegister = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (reRegister == null)
            {
                await SimpleEmbedAsync($"Current Allow re-register Setting: {competition.AllowReRegister}", Color.Blue);
                return;
            }
            competition.AllowReRegister = reRegister.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Allow re-register set to {reRegister.Value}", Color.Green);
        }

        [Command("AllowSelfRename", RunMode = RunMode.Sync)]
        [Summary("Sets whether users are allowed to use the rename command")]
        public async Task AllowSelfRenameAsync(bool? selfRename = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (selfRename == null)
            {
                await SimpleEmbedAsync($"Current Allow Self Rename Setting: {competition.AllowSelfRename}", Color.Blue);
                return;
            }
            competition.AllowSelfRename = selfRename.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Allow Self Rename set to {selfRename.Value}", Color.Green);
        }

        [Command("DefaultWinModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the default amount of points users can earn when winning.")]
        public async Task CompWinModifier(int? amountToAdd = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            if (!amountToAdd.HasValue)
            {
                await SimpleEmbedAsync($"Current DefaultWinModifier Setting: {competition.DefaultWinModifier}", Color.Blue);
                return;
            }
            competition.DefaultWinModifier = amountToAdd.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Default Win Modifier set to {competition.DefaultWinModifier}", Color.Green);
        }


        [Command("DefaultLossModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the default amount of points users lose when the lose a game.")]
        public async Task CompLossModifier(int? amountToSubtract = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            if (!amountToSubtract.HasValue)
            {
                await SimpleEmbedAsync($"Current DefaultLossModifier Setting: {competition.DefaultLossModifier}", Color.Blue);
                return;
            }
            competition.DefaultLossModifier = amountToSubtract.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Default Loss Modifier set to {competition.DefaultLossModifier}", Color.Green);
        }

        [Command("RankLossModifier", RunMode = RunMode.Sync)]
        [Summary("Sets the amount of points lost for a user with the specified rank.")]
        public async Task RankLossModifier(IRole role, int? amountToSubtract = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var rank = competition.Ranks.FirstOrDefault(x => x.RoleId == role.Id);
            if (rank == null)
            {
                await SimpleEmbedAsync("Provided role is not a rank.", Color.Red);
                return;
            }

            rank.LossModifier = amountToSubtract;
            Service.SaveCompetition(competition);
            if (!amountToSubtract.HasValue)
            {
                await SimpleEmbedAsync($"This rank will now use the server's default loss value (-{competition.DefaultLossModifier}) when subtracting points.", Color.Blue);
            }
            else
            {
                await SimpleEmbedAsync($"When a player with this rank loses they will lose {amountToSubtract} points", Color.Green);
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
                await SimpleEmbedAsync("Provided role is not a rank.", Color.Red);
                return;
            }

            rank.WinModifier = amountToAdd;
            Service.SaveCompetition(competition);
            if (!amountToAdd.HasValue)
            {
                await SimpleEmbedAsync($"This rank will now use the server's default win value (+{competition.DefaultWinModifier}) whenSimpleEmbedAsync adding points.", Color.Blue);
            }
            else
            {
                await SimpleEmbedAsync($"When a player with this rank wins they will gain {amountToAdd} points", Color.Green);
            }
        }

        [Command("UpdateNicknames", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will update user nicknames.")]
        public async Task UpdateNicknames(bool? updateNicknames = null)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (updateNicknames == null)
            {
                await SimpleEmbedAsync($"Current Update Nicknames Setting: {competition.UpdateNames}", Color.Blue);
                return;
            }
            competition.UpdateNames = updateNicknames.Value;
            Service.SaveCompetition(competition);
            await SimpleEmbedAsync($"Update Nicknames set to {competition.UpdateNames}", Color.Green);
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