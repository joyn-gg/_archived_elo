using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Models;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    public class UserManagement : ReactiveBase
    {
        public UserManagement(PremiumService premium, UserService userService)
        {
            Premium = premium;
            UserService = userService;
        }

        public PremiumService Premium { get; }
        public UserService UserService { get; }

        //TODO: Player specific ban lookup

        [Command("Bans", RunMode = RunMode.Async)]
        [Alias("Banlist")]
        [Summary("Shows all bans for the current server.")]
        public async Task Bans()
        {
            using (var db = new Database())
            {
                var bans = db.Bans.Where(x => x.GuildId == Context.Guild.Id).ToList();

                if (bans.Count == 0)
                {
                    await SimpleEmbedAsync("There aren't any banned players.", Color.Blue);
                    return;
                }
                var pages = bans.OrderBy(x => x.RemainingTime).SplitList(20).Select(x =>
                {
                    var page = new ReactivePage();

                    page.Description = string.Join("\n", x.Select(p =>
                    {
                        if (p.IsExpired)
                        {
                            if (p.ManuallyDisabled)
                            {
                                return $"{MentionUtils.MentionUser(p.UserId)} - Manually Disabled";
                            }
                            else
                            {
                                return $"{MentionUtils.MentionUser(p.UserId)} - Expired On {p.ExpiryTime.ToString("dd MMM yyyy")} at {p.ExpiryTime.ToShortTimeString()} Length: {p.Length.GetReadableLength()}";
                            }
                        }
                        else
                        {
                            return $"{MentionUtils.MentionUser(p.UserId)} - {p.ExpiryTime.ToString("dd MMM yyyy")} {p.ExpiryTime.ToShortTimeString()} in {p.RemainingTime.GetReadableLength()}";
                        }
                    }));
                    return page;
                });
                var pager = new ReactivePager(pages);
                await PagedReplyAsync(pager.ToCallBack().WithDefaultPagerCallbacks());
            }
        }

        [Command("Unban", RunMode = RunMode.Sync)]
        [Summary("Unbans the specified user.")]
        public async Task Unban(SocketGuildUser user)
        {
            if (!user.IsRegistered(out var player))
            {
                await SimpleEmbedAndDeleteAsync("Player is not registered.", Color.Red);
                return;
            }

            using (var db = new Database())
            {
                var bans = db.Bans.Where(x => x.GuildId == Context.Guild.Id && x.UserId == user.Id).ToList();
                if (bans.Count == 0)
                {
                    await SimpleEmbedAsync("Player has never been banned.", Color.DarkBlue);
                    return;
                }

                if (bans.All(x => x.IsExpired))
                {
                    await SimpleEmbedAsync("Player is not banned.", Color.DarkBlue);
                    return;
                }

                foreach (var ban in bans)
                {
                    if (!ban.IsExpired) ban.ManuallyDisabled = true;
                }

                db.UpdateRange(bans);
                db.SaveChanges();
                await SimpleEmbedAsync("Player has been unbanned.", Color.Green);
            }
        }

        [Command("BanUser", RunMode = RunMode.Sync)]
        [Alias("Ban")]
        [Summary("Bans the specified user for the specified amount of time, optional reason.")]
        public async Task BanUserAsync(SocketGuildUser user, TimeSpan time, [Remainder]string reason = null)
        {
            await BanUserAsync(time, user, reason);
        }

        [Command("BanUser", RunMode = RunMode.Sync)]
        [Alias("Ban")]
        [Summary("Bans the specified user for the specified amount of time, optional reason.")]
        public async Task BanUserAsync(TimeSpan time, SocketGuildUser user, [Remainder]string reason = null)
        {
            using (var db = new Database())
            {
                var player = db.Players.Find(Context.Guild.Id, user.Id);
                if (player == null)
                {
                    await SimpleEmbedAndDeleteAsync("User is not registered.", Color.Red);
                    return;
                }

                var ban = new Ban
                {
                    Moderator = Context.User.Id,
                    TimeOfBan = DateTime.UtcNow,
                    Length = time,
                    UserId = user.Id,
                    Comment = reason,
                    ManuallyDisabled = false,
                    GuildId = Context.Guild.Id
                };

                db.Bans.Add(ban);
                db.SaveChanges();
                await SimpleEmbedAsync($"{user.Mention} banned from joining games until: {ban.ExpiryTime.ToString("dd MMM yyyy")} {ban.ExpiryTime.ToShortTimeString()} in {ban.RemainingTime.GetReadableLength()}", Color.DarkRed);
            }
        }

        /*
        [Command("DeleteUser", RunMode = RunMode.Sync)]
        [Alias("DelUser")]
        [Summary("Deletes the specified user from the ELO competition, NOTE: Will not affect the LobbyLeaderboard command")]
        public async Task DeleteUserAsync(SocketGuildUser user)
        {
            var player = Service.GetPlayer(Context.Guild.Id, user.Id);
            if (player == null)
            {
                await SimpleEmbedAndDeleteAsync("User isn't registered.", Color.Red);
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            //Remove user ranks, register role and nickname
            Service.RemovePlayer(player);
            await SimpleEmbedAsync("User profile deleted.", Color.Green);
            competition.RegistrationCount--;
            Service.SaveCompetition(competition);

            if (user.Hierarchy < Context.Guild.CurrentUser.Hierarchy)
            {
                if (Context.Guild.CurrentUser.GuildPermissions.ManageRoles)
                {
                    var rolesToRemove = user.Roles.Where(x => competition.Ranks.Any(r => r.RoleId == x.Id)).ToList();
                    if (competition.RegisteredRankId != 0)
                    {
                        var registerRole = Context.Guild.GetRole(competition.RegisteredRankId);
                        if (registerRole != null)
                        {
                            rolesToRemove.Add(registerRole);
                        }
                    }
                    if (rolesToRemove.Any())
                    {
                        await user.RemoveRolesAsync(rolesToRemove);
                    }
                }

                if (competition.UpdateNames)
                {
                    if (Context.Guild.CurrentUser.GuildPermissions.ManageNicknames)
                    {
                        if (user.Nickname != null)
                        {
                            //TODO: Combine role and nick modification to reduce discord requests
                            await user.ModifyAsync(x => x.Nickname = null);
                        }
                    }
                }
            }
            else
            {
                await SimpleEmbedAsync("The user being deleted has a higher permission level than the bot and cannot have their ranks or nickname modified.", Color.Red);
            }
        }*/
    }
}
