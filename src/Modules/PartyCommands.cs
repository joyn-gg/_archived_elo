using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO;
using ELO.Extensions;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using RavenBOT.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public class PartyCommands : ReactiveBase
    {
        [Command("Duo", RunMode = RunMode.Sync)]
        [Summary("Join the queue with another user in the current lobby.")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task JoinLobbyAsync(SocketGuildUser user)
        {
            using (var db = new Database())
            {
                if (!(Context.User as SocketGuildUser).IsRegistered())
                {
                    await SimpleEmbedAsync("You are not registered.");
                    return;
                }
                if (!user.IsRegistered())
                {
                    await SimpleEmbedAsync("User is not registered.");
                    return;
                }

                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var partyHost = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == Context.User.Id);
                var userMember = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == user.Id);
                if (partyHost != null)
                {
                    await SimpleEmbedAsync($"You are already in a party with someone. Use the `disband` command to leave it.");
                    return;
                }

                if (userMember != null)
                {
                    await SimpleEmbedAsync($"{user.Mention} is already in a party with someone, they must leave it by using the `disband` command in order to join a new one.");
                    return;
                }

                await PagedReplyAsync(new ReactivePager
                {
                    Description = $"{user.Mention} React to this message to accept the party request"
                }.ToCallBack()
                .WithCallback(new Emoji("✅"), async (x, y) =>
                {
                    if (y.UserId == user.Id)
                    {
                        using (var db = new Database())
                        {
                            var partyHost2 = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == Context.User.Id);
                            var userMember2 = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == user.Id);
                            if (partyHost2 != null)
                            {
                                await SimpleEmbedAsync($"Host is already in a party with someone. Use the `disband` command to leave it.");
                                return true;
                            }

                            if (userMember2 != null)
                            {
                                await SimpleEmbedAsync($"{user.Mention} is already in a party with someone, they must leave it by using the `disband` command in order to join a new one.");
                                return true;
                            }

                            var host = new PartyMember
                            {
                                UserId = Context.User.Id,
                                PartyHost = Context.User.Id,
                                ChannelId = Context.Channel.Id,
                                GuildId = Context.Guild.Id
                            };

                            var member = new PartyMember
                            {
                                UserId = user.Id,
                                PartyHost = Context.User.Id,
                                ChannelId = Context.Channel.Id,
                                GuildId = Context.Guild.Id
                            };
                            db.PartyMembers.Add(host);
                            db.PartyMembers.Add(member);
                            await SimpleEmbedAsync($"Duo created.");
                            db.SaveChanges();
                        }

                        return true;
                    }

                    return false;
                }));
            }
        }

        [Command("ForceDuo", RunMode = RunMode.Sync)]
        [Summary("Join the queue with another user in the current lobby.")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public virtual async Task JoinLobbyAsync(SocketGuildUser host, SocketGuildUser user)
        {
            using (var db = new Database())
            {
                if (!host.IsRegistered())
                {
                    await SimpleEmbedAsync("Host is not registered.");
                    return;
                }
                if (!user.IsRegistered())
                {
                    await SimpleEmbedAsync("User is not registered.");
                    return;
                }

                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var partyHost = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == host.Id);
                var userMember = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == user.Id);
                if (partyHost != null)
                {
                    await SimpleEmbedAsync($"{host.Mention} is already in a party with someone. They can use the `disband` command to leave it.");
                    return;
                }

                if (userMember != null)
                {
                    await SimpleEmbedAsync($"{user.Mention} is already in a party with someone, they must leave it by using the `disband` command in order to join a new one.");
                    return;
                }

                var hostPlayer = new PartyMember
                {
                    UserId = host.Id,
                    PartyHost = host.Id,
                    ChannelId = Context.Channel.Id,
                    GuildId = Context.Guild.Id
                };

                var member = new PartyMember
                {
                    UserId = user.Id,
                    PartyHost = host.Id,
                    ChannelId = Context.Channel.Id,
                    GuildId = Context.Guild.Id
                };
                db.PartyMembers.Add(hostPlayer);
                db.PartyMembers.Add(member);
                await SimpleEmbedAsync($"Duo created.");
                db.SaveChanges();
            }
        }

        [Command("Disband", RunMode = RunMode.Sync)]
        [Summary("Leave the current duo you're in.")]
        public virtual async Task DuoLeaveAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var member = db.PartyMembers.FirstOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == Context.User.Id);
                if (member == null)
                {
                    await SimpleEmbedAsync($"You are not in a party.");
                    return;
                }

                var hostMatches = db.PartyMembers.Where(x => x.ChannelId == Context.Channel.Id && x.PartyHost == member.PartyHost);
                db.PartyMembers.Remove(member);
                db.PartyMembers.RemoveRange(hostMatches);

                db.SaveChanges();
                await SimpleEmbedAsync($"Duo has disbanded.");
            }
        }

        [Command("Party", RunMode = RunMode.Sync)]
        [Summary("Check you current party info.")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task PartyInfoAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var member = db.PartyMembers.SingleOrDefault(x => x.ChannelId == Context.Channel.Id && x.UserId == Context.User.Id);
                if (member == null)
                {
                    await SimpleEmbedAsync($"You are not in a party.");
                    return;
                }

                var hostMatches = db.PartyMembers.Where(x => x.ChannelId == Context.Channel.Id && x.PartyHost == member.PartyHost);

                await SimpleEmbedAsync($"Party Members: {string.Join(" ", hostMatches.Select(x => MentionUtils.MentionUser(x.UserId)))}");
            }
        }

        [Command("Parties", RunMode = RunMode.Sync)]
        [Summary("Check you current party info.")]
        [RequirePermission(PermissionLevel.Registered)]
        public virtual async Task PartiesInfoAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var parties = db.PartyMembers.Where(x => x.ChannelId == Context.Channel.Id).ToArray().GroupBy(x => x.PartyHost);
                if (parties.Any())
                {
                    //await SimpleEmbedAsync(string.Join("\n", parties.Select(x => $"Host: {MentionUtils.MentionUser(x.Key)} Members: {string.Join(" ", x.Select(m => MentionUtils.MentionUser(m.UserId)))}")));
                    var pages = parties.SplitList(10);
                    var pageList = new List<ReactivePage>();
                    foreach (var page in pages)
                    {
                        pageList.Add(new ReactivePage
                        {
                            Description = string.Join("\n", page.Select(x => $"Host: {MentionUtils.MentionUser(x.Key)} Members: {string.Join(" ", x.Select(m => MentionUtils.MentionUser(m.UserId)))}")).FixLength(1024)
                        });
                    }
                    await PagedReplyAsync(new ReactivePager(pageList).ToCallBack().WithDefaultPagerCallbacks());
                }
                else
                    await SimpleEmbedAsync("There are no parties.");
            }
        }

        [Command("DisbandAll", RunMode = RunMode.Sync)]
        [Summary("Disbands all parties for the current lobby.")]
        [RequirePermission(PermissionLevel.ELOAdmin)]
        public virtual async Task DisbandAllParties()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("This channel is not a lobby.");
                    return;
                }

                var partyMembers = db.PartyMembers.Where(x => x.ChannelId == Context.Channel.Id).ToArray();
                db.PartyMembers.RemoveRange(partyMembers);
                await SimpleEmbedAsync("All duos have been disbanded.");
                db.SaveChanges();
            }
        }
    }
}