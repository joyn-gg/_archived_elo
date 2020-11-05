using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;

using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RequireContext(ContextType.Guild)]
    public class UserCommands : ModuleBase<ShardedCommandContext>
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
            if (users.Length > 5)
            {
                await Context.SimpleEmbedAsync($"You may only force register 5 members at a time.");
                return;
            }
            foreach (var user in users)
            {
                if (!await RegisterAsync(user))
                {
                    await Context.SimpleEmbedAsync($"Exited on forceregister for user {user.Mention}, unable to register.");
                    break;
                }
            }
        }

        [Command("Register", RunMode = RunMode.Sync)]
        [Alias("reg")]
        [Summary("Register for the ELO competition.")]
        [RateLimit(1, 20, Measure.Seconds)]
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

                if (regUser.IsRegistered(out var user))
                {
                    if (!comp.AllowReRegister)
                    {
                        if (regUser.Id != Context.User.Id)
                        {
                            await Context.SimpleEmbedAndDeleteAsync($"{regUser.Mention} is already registered.", Color.Red);
                        }
                        else
                        {
                            await Context.SimpleEmbedAndDeleteAsync("You are not allowed to re-register.", Color.Red);
                        }
                        return true;
                    }

                    user.DisplayName = name;
                    db.Players.Update(user);
                    db.SaveChanges();
                }
                else
                {
                    var registered = ((IQueryable<Player>)db.Players).Count(x => x.GuildId == Context.Guild.Id);
                    var limit = Premium.GetRegistrationLimit(Context.Guild.Id);
                    if (limit <= registered)
                    {
                        var voteState = await VoteService.CheckAsync(Context.Client, regUser.Id);

                        var registrationLimitEmbed = new EmbedBuilder
                        {
                            Title = "Registration Limit Exceeded",
                            Url = Premium.PremiumConfig.AltLink,
                            ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(),
                            Color = Color.DarkBlue
                        }
                        .AddField("Subscribe", $"You can upgrade your registration limit by subscribing at [Patreon]({Premium.PremiumConfig.AltLink})\n" +
                        $"For Support, visit the ELO [Support Server]({Premium.PremiumConfig.ServerInvite})", true);

                        bool displayVoteInfo = false;
                        bool allowRegistrationForVote = false;

                        if (voteState == TopggVoteService.ResultType.NotVoted)
                        {
                            displayVoteInfo = true;
                        }
                        else if (voteState == TopggVoteService.ResultType.Voted)
                        {
                            // Check registration count against new vote limit
                            if (registered >= VoteService.MaxRegLimit)
                            {
                                displayVoteInfo = true;
                            }
                            else
                            {
                                allowRegistrationForVote = true;
                            }
                        }

                        if (displayVoteInfo)
                        {
                            registrationLimitEmbed.AddField("Voting", $"For up to {VoteService.MaxRegLimit} registrations, " +
                                $"individuals may register by voting at [ELO](https://top.gg/bot/{Context.Client.CurrentUser.Id}) and then running the `register` command", true);
                        }

                        if (!allowRegistrationForVote)
                        {
                            registrationLimitEmbed.AddField("Limits",
                                $"**Currently Registered:** {registered}/{limit} users" +
                                (displayVoteInfo ? $"\n**Voted Registration Limit:** {VoteService.MaxRegLimit} users" : ""));

                            await ReplyAsync("", false, registrationLimitEmbed.Build());
                            return false;
                        }
                    }

                    user = new Player(regUser.Id, Context.Guild.Id, name)
                    {
                        Points = comp.DefaultRegisterScore
                    };
                    db.Players.Add(user);
                    db.SaveChanges();
                    Extensions.Extensions.ClearUserCache(Context.Guild.Id, user.UserId);
                }

                var ranks = db.Ranks.AsQueryable().Where(x => x.GuildId == Context.Guild.Id).ToArray();
                var responses = await UserService.UpdateUserAsync(comp, user, ranks, regUser);

                if (regUser.Id == Context.User.Id)
                {
                    await Context.SimpleEmbedAsync(comp.FormatRegisterMessage(user), Color.Blue);
                }
                else
                {
                    await Context.SimpleEmbedAsync($"{regUser.Mention} was registered as `{Format.Sanitize(name)}` by {Context.User.Mention}", Color.Blue);
                }

                if (responses.Count > 0)
                {
                    await Context.SimpleEmbedAsync(string.Join("\n", responses), Color.Red);
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
                await Context.SimpleEmbedAsync("Try renaming yourself without the @mention ex. `Rename NewName`", Color.DarkBlue);
            }
            else
            {
                await Context.SimpleEmbedAsync("To rename another user, use the `RenameUser` command instead.", Color.DarkBlue);
            }
        }

        [Command("Rename", RunMode = RunMode.Sync)]
        [Summary("Rename yourself.")]
        public virtual async Task RenameAsync([Remainder]string name = null)
        {
            if (name == null)
            {
                await Context.SimpleEmbedAndDeleteAsync("You must specify a new name in order to be renamed.", Color.Red);
                return;
            }

            using (var db = new Database())
            {
                if (!(Context.User as SocketGuildUser).IsRegistered(out var user))
                {
                    await Context.SimpleEmbedAsync("You are not registered yet.", Color.DarkBlue);
                    return;
                }

                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                if (!comp.AllowSelfRename)
                {
                    await Context.SimpleEmbedAndDeleteAsync("You are not allowed to rename yourself.", Color.Red);
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
                            await Context.SimpleEmbedAsync("The bot does not have the `ManageNicknames` permission and therefore cannot update your nickname.", Color.Red);
                        }
                    }
                    else
                    {
                        await Context.SimpleEmbedAsync("You have a higher permission level than the bot and therefore it cannot update your nickname.", Color.Red);
                    }
                }

                db.Players.Update(user);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Your profile has been renamed from {Format.Sanitize(originalDisplayName)} to {user.GetDisplayNameSafe()}", Color.Green);
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
                await Context.SimpleEmbedAndDeleteAsync("User isn't registered.", Color.Red);
                return;
            }

            player.DisplayName = newname;
            using (var db = new Database())
            {
                db.Players.Update(player);
                db.SaveChanges();

                var competition = db.Competitions.Find(Context.Guild.Id);
                var responses = await UserService.UpdateUserAsync(competition, player, db.Ranks.AsQueryable().Where(x => x.GuildId == Context.Guild.Id).ToArray(), user);
                if (responses.Any())
                {
                    await Context.SimpleEmbedAsync("User's profile has been renamed\n" + string.Join("\n", responses), Color.Red);
                }
                else
                {
                    await Context.SimpleEmbedAsync("User's profile has been renamed successfully.", Color.Green);
                }
            }
        }
    }
}