using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Premium;
using System.Linq;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]

    public class UserCommands : ReactiveBase
    {
        public ELOService Service { get; }
        public PatreonIntegration Premium { get; }

        public UserCommands(ELOService service, PatreonIntegration prem)
        {
            Service = service;
            Premium = prem;
        }

        [Command("Register", RunMode = RunMode.Sync)]
        [Alias("reg")]
        [Summary("Register for the ELO competition.")]
        public async Task RegisterAsync([Remainder]string name = null)
        {

            if (name == null)
            {
                name = Context.User.Username;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (Context.User.IsRegistered(Service, out var player))
            {
                if (!competition.AllowReRegister)
                {
                    await SimpleEmbedAndDeleteAsync("You are not allowed to re-register.", Color.Red);
                    return;
                }
            }
            else
            {
                var limit = Premium.GetRegistrationLimit(Context);
                if (limit < competition.RegistrationCount)
                {
                    var config = Premium.GetConfig();
                    await SimpleEmbedAsync($"This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations, you can get premium by subscribing at {config.PageUrl} for support and to claim premium, a patreon must join the ELO server: {config.ServerInvite}", Color.DarkBlue);
                    return;
                }
                player = Service.CreatePlayer(Context.Guild.Id, Context.User.Id, name);
                competition.RegistrationCount++;
                Service.SaveCompetition(competition);
            }

            player.DisplayName = name;

            var responses = await Service.UpdateUserAsync(competition, player, Context.User as SocketGuildUser);

            await SimpleEmbedAsync(competition.FormatRegisterMessage(player), Color.Blue);
            if (responses.Count > 0)
            {
                await SimpleEmbedAsync(string.Join("\n", responses), Color.Red);
            }
        }

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public async Task RenameAsync(SocketGuildUser user, [Remainder]string name)
        {
            if (user.Id == Context.User.Id)
            {
                await SimpleEmbedAsync("Try renaming yourself without the @mention ex. `Rename NewName`", Color.DarkBlue);
            }
            else
            {
                await SimpleEmbedAsync("To rename another user, use the `RenameUser` command instead.", Color.DarkBlue);
            }
        }

        [Command("RenameUser", RunMode = RunMode.Sync)]
        [Alias("ForceRename")]
        [Summary("Renames the specified user.")]
        [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
        public async Task RenameUserAsync(SocketGuildUser user, [Remainder]string newname)
        {
            if (!user.IsRegistered(Service, out var player))
            {
                await SimpleEmbedAndDeleteAsync("User isn't registered.", Color.Red);
                return;
            }

            player.DisplayName = newname;
            Service.SavePlayer(player);

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var responses = await Service.UpdateUserAsync(competition, player, user);
            if (responses.Any())
            {
                await SimpleEmbedAsync("User's profile has been renamed\n" + string.Join("\n", responses), Color.Red);
            }
            else
            {
                await SimpleEmbedAsync("User's profile has been renamed successfully.", Color.Green);
            }
        }

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public async Task RenameAsync([Remainder]string name = null)
        {


            if (!Context.User.IsRegistered(Service, out var player))
            {
                await SimpleEmbedAsync("You are not registered yet.", Color.DarkBlue);
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (!competition.AllowSelfRename)
            {
                await SimpleEmbedAndDeleteAsync("You are not allowed to rename yourself.", Color.Red);
                return;
            }

            if (name == null)
            {
                await SimpleEmbedAndDeleteAsync("You must specify a new name in order to be renamed.", Color.Red);
                return;
            }

            var originalDisplayName = player.DisplayName;
            player.DisplayName = name;
            var newName = competition.GetNickname(player);

            var gUser = (Context.User as SocketGuildUser);
            var currentName = gUser.Nickname ?? gUser.Username;
            if (competition.UpdateNames && !currentName.Equals(newName))
            {
                if (gUser.Hierarchy < Context.Guild.CurrentUser.Hierarchy)
                {
                    if (Context.Guild.CurrentUser.GuildPermissions.ManageNicknames)
                    {
                        await gUser.ModifyAsync(x => x.Nickname = newName);
                    }
                    else
                    {
                        await SimpleEmbedAsync("The bot does not have the `ManageNicknames` permission and therefore cannot update your nickname.", Color.Red);
                    }
                }
                else
                {
                    await SimpleEmbedAsync("You have a higher permission level than the bot and therefore it cannot update your nickname.", Color.Red);
                }
            }

            Service.SavePlayer(player);
            await SimpleEmbedAsync($"Your profile has been renamed from {Discord.Format.Sanitize(originalDisplayName)} to {player.GetDisplayNameSafe()}", Color.Green);
        }
    }
}