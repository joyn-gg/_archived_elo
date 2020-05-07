using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using RavenBOT.Common;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public class UserCommands : ReactiveBase
    {
        public UserCommands(PremiumService premium, UserService userService, TopggVoteService voteService)
        {
            Premium = premium;
            UserService = userService;
            VoteService = voteService;
        }

        public PremiumService Premium { get; }

        public UserService UserService { get; }

        public TopggVoteService VoteService { get; }

        [Command("ForceRegisterUsers", RunMode = RunMode.Sync)]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public virtual async Task ForceRegisterAsync(params SocketGuildUser[] users)
        {
            foreach (var user in users)
            {
                if (!await RegisterAsync(user))
                {
                    await SimpleEmbedAsync($"Exited on forceregister for user {user.Mention}, unable to register.");
                    break;
                }
            }
        }

        [Command("ForceRegister", RunMode = RunMode.Sync)]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public virtual async Task ForceRegisterAsync(SocketGuildUser regUser, [Remainder]string name = null)
        {
            await RegisterAsync(regUser, name);
        }

        [Command("Register", RunMode = RunMode.Sync)]
        [Alias("reg")]
        [Summary("Register for the ELO competition.")]
        public virtual async Task RegisterAsync([Remainder]string name = null)
        {
            await RegisterAsync(Context.User as SocketGuildUser, name);
        }

        public virtual async Task<bool> RegisterAsync(SocketGuildUser regUser, [Remainder]string name = null)
        {
            if (regUser == null) return false;

            if (name == null)
            {
                name = regUser.Username;
            }

            using (var db = new Database())
            {
                var comp = db.GetOrCreateCompetition(Context.Guild.Id);

                if (!(regUser as SocketGuildUser).IsRegistered(out var user))
                {
                    var registered = ((IQueryable<Player>)db.Players).Count(x => x.GuildId == Context.Guild.Id);
                    var limit = Premium.GetRegistrationLimit(Context.Guild.Id);
                    if (limit < registered)
                    {
                        var voteState = await VoteService.CheckAsync(Context.Client, regUser.Id);
                        if (voteState == TopggVoteService.ResultType.NotConfigured)
                        {
                            await SimpleEmbedAsync($"This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations, you can get premium by subscribing at {Premium.PremiumConfig.AltLink} for support and to claim premium, a patron must join the ELO server: {Premium.PremiumConfig.ServerInvite}", Color.DarkBlue);
                            return false;
                        }
                        else if (voteState == TopggVoteService.ResultType.NotVoted)
                        {
                            await SimpleEmbedAsync($"This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations, " +
                                $"you can get premium by subscribing at {Premium.PremiumConfig.AltLink} for support and to claim premium, " +
                                $"a patron must join the ELO server: {Premium.PremiumConfig.ServerInvite}\n" +
                                $"For up to {VoteService.MaxRegLimit} registrations, you may also enable registration by voting at https://top.gg/bot/{Context.Client.CurrentUser.Id}", Color.DarkBlue);
                            return false;
                        }
                        else
                        {
                            if (registered > VoteService.MaxRegLimit)
                            {
                                await SimpleEmbedAsync($"This server has exceeded the maximum registration count of {limit} (Currently Registered {registered}), it must be upgraded to premium to allow additional registrations, " +
                                    $"you can get premium by subscribing at {Premium.PremiumConfig.AltLink} for support and to claim premium, " +
                                    $"a patron must join the ELO server: {Premium.PremiumConfig.ServerInvite}\n" +
                                    $"For up to {VoteService.MaxRegLimit} registrations, you may also enable registration by voting at https://top.gg/bot/{Context.Client.CurrentUser.Id}", Color.DarkBlue);
                                return false;
                            }
                        }
                    }

                    user = new Player(regUser.Id, Context.Guild.Id, name);
                    user.Points = comp.DefaultRegisterScore;
                    db.Players.Add(user);
                    db.SaveChanges();
                }
                else
                {
                    if (!comp.AllowReRegister)
                    {
                        if (regUser.Id != Context.User.Id)
                        {
                            await SimpleEmbedAndDeleteAsync($"{regUser.Mention} is already registered.", Color.Red);
                        }
                        else
                        {
                            await SimpleEmbedAndDeleteAsync("You are not allowed to re-register.", Color.Red);
                        }
                        return true;
                    }

                    user.DisplayName = name;
                    db.Players.Update(user);
                    db.SaveChanges();
                }
                if (ELO.Extensions.Extensions.RegistrationCache.TryGetValue(Context.Guild.Id, out var gCache))
                {
                    if (gCache.ContainsKey(regUser.Id))
                    {
                        gCache[regUser.Id] = true;
                    }
                }
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray();
                var responses = await UserService.UpdateUserAsync(comp, user, ranks, regUser);

                await SimpleEmbedAsync(comp.FormatRegisterMessage(user), Color.Blue);
                if (responses.Count > 0)
                {
                    await SimpleEmbedAsync(string.Join("\n", responses), Color.Red);
                }
            }

            return true;
        }

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public virtual async Task RenameAsync(SocketGuildUser user, [Remainder]string name)
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

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public virtual async Task RenameAsync([Remainder]string name = null)
        {
            if (name == null)
            {
                await SimpleEmbedAndDeleteAsync("You must specify a new name in order to be renamed.", Color.Red);
                return;
            }

            using (var db = new Database())
            {
                if (!(Context.User as SocketGuildUser).IsRegistered(out var user))
                {
                    await SimpleEmbedAsync("You are not registered yet.", Color.DarkBlue);
                    return;
                }

                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                if (!comp.AllowSelfRename)
                {
                    await SimpleEmbedAndDeleteAsync("You are not allowed to rename yourself.", Color.Red);
                    return;
                }

                var originalDisplayName = user.DisplayName;
                user.DisplayName = name;
                var newName = comp.GetNickname(user);
                var gUser = Context.User as SocketGuildUser;
                var currentName = gUser.Nickname ?? gUser.Username;
                if (comp.UpdateNames && !currentName.Equals(newName))
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

                db.Players.Update(user);
                db.SaveChanges();
                await SimpleEmbedAsync($"Your profile has been renamed from {Format.Sanitize(originalDisplayName)} to {user.GetDisplayNameSafe()}", Color.Green);
            }
        }

        [Command("RenameUser", RunMode = RunMode.Sync)]
        [Alias("ForceRename")]
        [Summary("Renames the specified user.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public virtual async Task RenameUserAsync(SocketGuildUser user, [Remainder]string newname)
        {
            if (!user.IsRegistered(out var player))
            {
                await SimpleEmbedAndDeleteAsync("User isn't registered.", Color.Red);
                return;
            }

            player.DisplayName = newname;
            using (var db = new Database())
            {
                db.Players.Update(player);
                db.SaveChanges();

                var competition = db.Competitions.Find(Context.Guild.Id);
                var responses = await UserService.UpdateUserAsync(competition, player, db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToArray(), user);
                if (responses.Any())
                {
                    await SimpleEmbedAsync("User's profile has been renamed\n" + string.Join("\n", responses), Color.Red);
                }
                else
                {
                    await SimpleEmbedAsync("User's profile has been renamed successfully.", Color.Green);
                }
            }
        }
    }
}