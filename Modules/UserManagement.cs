using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    [Preconditions.RequireAdmin]
    public class UserManagement : ReactiveBase
    {
        public UserManagement(ELOService service)
        {
            Service = service;
        }

        public ELOService Service { get; }

        [Command("NameHistory", RunMode = RunMode.Async)]
        [Summary("Displays name update history for the specified user.")]
        public async Task NameHistoryAsync(SocketGuildUser user)
        {
            if (!user.IsRegistered(Service, out var player))
            {
                await ReplyAsync("Player is not registered.");
                return;
            }

            if (player.NameLog.Any())
            {
                await SimpleEmbedAsync($"Current: {player.GetDisplayNameSafe()}\n" + string.Join("\n", player.NameLog.Select(x =>
                {
                    var time = new DateTime(x.Key);
                    return $"{time.ToString("dd MMM yyyy")} {time.ToShortTimeString()} - {x.Value}";
                })));
            }
            else
            {
                await ReplyAsync("There are no name changes in history for this user.");
            }
        }

        [Command("Bans", RunMode = RunMode.Async)]
        [Alias("Banlist")]
        [Summary("Shows all bans for the current server.")]
        public async Task Bans()
        {
            var players = Service.GetPlayers(x => x.GuildId == Context.Guild.Id && x.IsBanned);
            if (players.Length == 0)
            {
                await ReplyAsync("There aren't any banned players.");
                return;
            }
            var pages = players.OrderBy(x => x.CurrentBan.RemainingTime).SplitList(20).Select(x =>
            {
                var page = new ReactivePage();
                page.Description = string.Join("\n", x.Select(p => $"{MentionUtils.MentionUser(p.UserId)} - {p.CurrentBan.ExpiryTime.ToString("dd MMM yyyy")} {p.CurrentBan.ExpiryTime.ToShortTimeString()} in {p.CurrentBan.RemainingTime.GetReadableLength()}"));
                return page;
            });
            var pager = new ReactivePager(pages);
            await PagedReplyAsync(pager.ToCallBack().WithDefaultPagerCallbacks());
        }

        [Command("Unban", RunMode = RunMode.Sync)]
        [Summary("Unbans the specified user.")]
        public async Task Unban(SocketGuildUser user)
        {
            if (!user.IsRegistered(Service, out var player))
            {
                await ReplyAsync("Player is not registered.");
                return;
            }

            player.CurrentBan.ManuallyDisabled = true;
            Service.SavePlayer(player);
            await ReplyAsync("Unbanned player.");
        }

        [Command("BanUser", RunMode = RunMode.Sync)]
        [Alias("Ban")]
        [Summary("Bans the specified user for the specified amount of time, optional reason.")]
        public async Task BanUserAsync(TimeSpan time, SocketGuildUser user, [Remainder]string reason = null)
        {
            var player = Service.GetPlayer(Context.Guild.Id, user.Id);
            if (player == null)
            {
                await ReplyAsync("User is not registered.");
                return;
            }

            player.BanHistory.Add(new Player.Ban(time, Context.User.Id, reason));
            Service.SavePlayer(player);
            await ReplyAsync($"Player banned from joining games until: {player.CurrentBan.ExpiryTime.ToString("dd MMM yyyy")} {player.CurrentBan.ExpiryTime.ToShortTimeString()} in {player.CurrentBan.RemainingTime.GetReadableLength()}");
        }

        [Command("RenameUser", RunMode = RunMode.Sync)]
        [Alias("ForceRename")]
        [Summary("Renames the specified user.")]
        public async Task RenameUserAsync(SocketGuildUser user, [Remainder]string newname)
        {
            if (!user.IsRegistered(Service, out var player))
            {
                await ReplyAsync("User isn't registered.");
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
                await SimpleEmbedAsync("User's profile has been renamed successfully.");
            }
        }

        [Command("DeleteUser", RunMode = RunMode.Sync)]
        [Alias("DelUser")]
        [Summary("Deletes the specified user from the ELO competition, NOTE: Will not affect the LobbyLeaderboard command")]
        public async Task DeleteUserAsync(SocketGuildUser user)
        {
            var player = Service.GetPlayer(Context.Guild.Id, user.Id);
            if (player == null)
            {
                await ReplyAsync("User isn't registered.");
                return;
            }

            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);

            //Remove user ranks, register role and nickname
            Service.RemovePlayer(player);
            await ReplyAsync("User profile deleted.");
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
                await ReplyAsync("The user being deleted has a higher permission level than the bot and cannot have their ranks or nickname modified.");
            }
        }

    }
}