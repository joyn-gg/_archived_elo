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
                await SimpleEmbedAndDeleteAsync("Player is not registered.", Color.Red);
                return;
            }

            if (player.NameLog.Any())
            {
                await SimpleEmbedAndDeleteAsync($"Current: {player.GetDisplayNameSafe()}\n" + string.Join("\n", player.NameLog.Select(x =>
                {
                    var time = new DateTime(x.Key);
                    return $"{time.ToString("dd MMM yyyy")} {time.ToShortTimeString()} - {x.Value}";
                })), Color.Blue);
            }
            else
            {
                await SimpleEmbedAsync("There are no name changes in history for this user.", Color.DarkBlue);
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
                await SimpleEmbedAsync("There aren't any banned players.", Color.Blue);
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
                await SimpleEmbedAndDeleteAsync("Player is not registered.", Color.Red);
                return;
            }

            player.CurrentBan.ManuallyDisabled = true;
            Service.SavePlayer(player);
            await SimpleEmbedAsync("Unbanned player.", Color.Green);
        }

        [Command("BanUser", RunMode = RunMode.Sync)]
        [Alias("Ban")]
        [Summary("Bans the specified user for the specified amount of time, optional reason.")]
        public async Task BanUserAsync(TimeSpan time, SocketGuildUser user, [Remainder]string reason = null)
        {
            var player = Service.GetPlayer(Context.Guild.Id, user.Id);
            if (player == null)
            {
                await SimpleEmbedAndDeleteAsync("User is not registered.", Color.Red);
                return;
            }

            player.BanHistory.Add(new Player.Ban(time, Context.User.Id, reason));
            Service.SavePlayer(player);
            await SimpleEmbedAsync($"{user.Mention} banned from joining games until: {player.CurrentBan.ExpiryTime.ToString("dd MMM yyyy")} {player.CurrentBan.ExpiryTime.ToShortTimeString()} in {player.CurrentBan.RemainingTime.GetReadableLength()}", Color.DarkRed);
        }

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
        }

    }
}