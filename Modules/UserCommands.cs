using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Premium;
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
                    await ReplyAsync("You are not allowed to re-register.");
                    return;
                }
            }
            else
            {
                var limit = Premium.GetRegistrationLimit(Context);
                if (limit < competition.RegistrationCount)
                {
                    await ReplyAsync($"This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations");
                    return;
                }
                player = Service.CreatePlayer(Context.Guild.Id, Context.User.Id, name);
                competition.RegistrationCount++;
                Service.SaveCompetition(competition);
            }

            player.DisplayName = name;

            var responses = await Service.UpdateUserAsync(competition, player, Context.User as SocketGuildUser);

            await ReplyAsync(competition.FormatRegisterMessage(player));
            if (responses.Count > 0)
            {
                await SimpleEmbedAsync(string.Join("\n", responses));
            }
        }

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public async Task RenameAsync([Remainder]string name = null)
        {


            if (!Context.User.IsRegistered(Service, out var player))
            {
                await ReplyAsync("You are not registered yet.");
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            if (!competition.AllowSelfRename)
            {
                await ReplyAsync("You are not allowed to rename yourself");
                return;
            }

            if (name == null)
            {
                await ReplyAsync("You must specify a new name in order to be renamed.");
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
                        await ReplyAsync("The bot does not have the `ManageNicknames` permission and therefore cannot update your nickname.");
                    }
                }
                else
                {
                    await ReplyAsync("You have a higher permission level than the bot and therefore it cannot update your nickname.");
                }
            }

            Service.SavePlayer(player);
            await ReplyAsync($"Your profile has been renamed from {Discord.Format.Sanitize(originalDisplayName)} to {player.GetDisplayNameSafe()}");
        }
    }
}