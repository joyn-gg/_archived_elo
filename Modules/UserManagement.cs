using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RequirePermission(PermissionLevel.Moderator)]
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
        public virtual async Task Bans()
        {
            using (var db = new Database())
            {
                var bans = db.Bans.Where(x => x.GuildId == Context.Guild.Id).ToList();
                if (bans.Count == 0)
                {
                    await SimpleEmbedAsync("There aren't any banned players.", Color.Blue);
                    return;
                }

                //Show bans in order of which is soonest to expire
                var pages2 = bans.OrderBy(x => x.RemainingTime).Where(x => x.IsExpired == false).SplitList(5).Select(x =>
                {
                    var page = new ReactivePage();

                    page.Fields = x.Select(p =>
                    {
                        var user = db.Players.Find(Context.Guild.Id, p.UserId);
                        var field = new EmbedFieldBuilder
                        {
                            Name = user?.GetDisplayNameSafe() ?? p.UserId.ToString(),
                            Value = $"**User:** {MentionUtils.MentionUser(p.UserId)}\n" +
                            $"**Banned at:**  {p.ExpiryTime.ToString("dd MMM yyyy")}\n" +
                            $"**Ban Length:** {p.Length.GetReadableLength()}\n" +
                            $"**Expires in:** {p.RemainingTime.GetReadableLength()}\n" +
                            $"**Banned by:** {MentionUtils.MentionUser(p.Moderator)}\n" +
                            $"**Reason:** {p.Comment ?? "N/A"}"
                        };

                        return field;
                    }).ToList();
                    return page;
                });
                if (!pages2.Any())
                {
                    await SimpleEmbedAsync("There are no players currently banned. Use the `Allbans` command to lookup all player bans.");
                    return;
                }
                var pager2 = new ReactivePager(pages2);
                await PagedReplyAsync(pager2.ToCallBack().WithDefaultPagerCallbacks());
            }
        }

        [Command("AllBans", RunMode = RunMode.Async)]
        [Summary("Shows all bans for the current server.")]
        public virtual async Task AllBans()
        {
            using (var db = new Database())
            {
                var bans = db.Bans.Where(x => x.GuildId == Context.Guild.Id).ToList();
                if (bans.Count == 0)
                {
                    await SimpleEmbedAsync("There aren't any banned players.", Color.Blue);
                    return;
                }

                var pages2 = bans.OrderBy(x => x.RemainingTime).SplitList(5).Select(x =>
                {
                    var page = new ReactivePage();

                    page.Fields = x.Select(p =>
                    {
                        var user = db.Players.Find(Context.Guild.Id, p.UserId);
                        var field = new EmbedFieldBuilder
                        {
                            Name = user?.DisplayName ?? p.UserId.ToString(),
                            Value = $"**User:** {MentionUtils.MentionUser(p.UserId)}\n" +
                            $"**Banned at:**  {p.ExpiryTime.ToString("dd MMM yyyy")}\n" +
                            $"**Ban Length:** {p.Length.GetReadableLength()}\n" +
                            $"**Banned By:** {MentionUtils.MentionUser(p.Moderator)}\n" +
                            $"**Manually Disabled:** {p.ManuallyDisabled}\n" +
                            $"**Reason:** {p.Comment ?? "N/A"}\n" +
                            $"**Expired:** {p.IsExpired}"
                        };

                        return field;
                    }).ToList();
                    return page;
                });
                var pager2 = new ReactivePager(pages2);
                await PagedReplyAsync(pager2.ToCallBack().WithDefaultPagerCallbacks());
            }
        }

        [Command("Unban", RunMode = RunMode.Sync)]
        [Summary("Unbans the specified user.")]
        public virtual async Task Unban(SocketGuildUser user)
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
        public virtual async Task BanUserAsync(SocketGuildUser user, TimeSpan time, [Remainder]string reason = null)
        {
            await BanUserAsync(time, user, reason);
        }

        [Command("BanUser", RunMode = RunMode.Sync)]
        [Alias("Ban")]
        [Summary("Bans the specified user for the specified amount of time, optional reason.")]
        public virtual async Task BanUserAsync(TimeSpan time, SocketGuildUser user, [Remainder]string reason = null)
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
    }
}
