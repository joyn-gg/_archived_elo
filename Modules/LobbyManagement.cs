using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public partial class LobbyManagement : ReactiveBase
    {
        public LobbyManagement(Random random, GameService gameService)
        {
            Random = random;
            GameService = gameService;
        }

        //TODO: Player queuing via reactions to a message.

        public Random Random { get; }
        public GameService GameService { get; }

        //TODO: Replace command
        //TODO: Map stuff
        //TODO: Assign teams to temp roles until game result is decided.
        //TODO: Assign a game to a specific channel until game result is decided.
        //TODO: Allow players to party up for a lobby

        [Command("ClearQueue", RunMode = RunMode.Sync)]
        [Alias("Clear Queue", "clearq", "clearque")]
        [Summary("Clears the current queue.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task ClearQueueAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var queuedPlayers = db.QueuedPlayers.Where(x => x.ChannelId == Context.Channel.Id);
                db.QueuedPlayers.RemoveRange(queuedPlayers);

                var latestGame = db.GameResults.Where(x => x.LobbyId == Context.Channel.Id).OrderByDescending(x => x.GameId).FirstOrDefault();
                if (latestGame != null && latestGame.GameState == GameState.Picking)
                {
                    latestGame.GameState = GameState.Canceled;
                    db.GameResults.Update(latestGame);

                    //Announce game cancelled.
                    await SimpleEmbedAsync($"Queue Cleared. Game #{latestGame.GameId} was cancelled as a result.", Color.Green);
                }
                else
                {
                    await SimpleEmbedAsync($"Queue Cleared.", Color.Green);
                }

                db.SaveChanges();
            }
        }

        [Command("ForceJoin", RunMode = RunMode.Sync)]
        [Summary("Forcefully adds a user to queue, bypasses minimum points")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task ForceJoinAsync(params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                var userIds = users.Select(x => x.Id).ToList();
                var userPlayers = db.Players.Where(x => x.GuildId == Context.Guild.Id && userIds.Contains(x.UserId));
                var queue = db.QueuedPlayers.Where(x => x.GuildId == Context.Guild.Id && x.ChannelId == Context.Channel.Id).ToList();
                int queueCount = queue.Count;
                var latestGame = db.GameResults.Where(x => x.LobbyId == Context.Channel.Id).OrderByDescending(x => x.GameId).FirstOrDefault();
                if (latestGame != null && latestGame.GameState == GameState.Picking)
                {
                    await SimpleEmbedAndDeleteAsync("Current game is picking teams, wait until this is completed.", Color.Red);
                    return;
                }

                var added = new List<ulong>();
                foreach (var player in userPlayers)
                {
                    if (queueCount >= lobby.PlayersPerTeam * 2)
                    {
                        //Queue will be reset after teams are completely picked.
                        await SimpleEmbedAsync("Queue is full, wait for teams to be chosen before joining.", Color.DarkBlue);
                        break;
                    }

                    if (queue.Any(x => x.UserId == player.UserId))
                    {
                        await SimpleEmbedAsync($"{MentionUtils.MentionUser(player.UserId)} is already queued.", Color.DarkBlue);
                        continue;
                    }

                    added.Add(player.UserId);
                    db.QueuedPlayers.Add(new QueuedPlayer
                    {
                        UserId = player.UserId,
                        GuildId = Context.Guild.Id,
                        ChannelId = Context.Channel.Id
                    });
                    queueCount++;
                }
                db.SaveChanges();
                await SimpleEmbedAsync($"{string.Join("", added.Select(MentionUtils.MentionUser))} - added to queue.", Color.Green);

                if (queueCount >= lobby.PlayersPerTeam * 2)
                {
                    db.SaveChanges();

                    await LobbyFullAsync(lobby);
                    return;
                }
            }
        }

        [Command("Sub")]
        [Summary("Replace a user in the specified game with another.")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task SubUserAsync(int gameNumber, SocketGuildUser user, SocketGuildUser replacedWith)
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.SingleOrDefault(x => x.GameId == gameNumber && x.LobbyId == lobby.ChannelId);
                if (game == null)
                {
                    await SimpleEmbedAsync("Invalid game number.", Color.Red);
                    return;
                }

                if (game.GameState != GameState.Undecided && game.GameState != GameState.Picking)
                {
                    await SimpleEmbedAsync("This command can only be used with undecided games.", Color.Red);
                    return;
                }

                var replaceUser = db.GetUser(replacedWith);
                if (replaceUser == null)
                {
                    await SimpleEmbedAsync($"{replacedWith.Mention} is not registered.");
                    return;
                }

                var team1 = db.GetTeam1(game).ToArray();
                var team2 = db.GetTeam2(game).ToArray();
                var t1c = db.GetTeamCaptain(game, 1);
                var t2c = db.GetTeamCaptain(game, 2);
                if (team1.Any(x => x.UserId == replacedWith.Id) || team2.Any(x => x.UserId == replacedWith.Id) || t1c?.UserId == replacedWith.Id || t2c?.UserId == replacedWith.Id)
                {
                    await SimpleEmbedAsync($"{replacedWith.Mention} is already in this game.");
                    return;
                }

                if (game.GameState == GameState.Picking)
                {
                    var queuedUsers = db.QueuedPlayers.Where(x => x.ChannelId == Context.Channel.Id);
                    var current = queuedUsers.SingleOrDefault(x => x.UserId == user.Id);
                    var replacer = queuedUsers.SingleOrDefault(x => x.UserId == replacedWith.Id);
                    if (replacer != null)
                    {
                        await SimpleEmbedAsync($"{replacedWith.Mention} is already queued for this game.");
                        return;
                    }

                    if (current != null)
                    {
                        db.QueuedPlayers.Remove(current);
                        db.QueuedPlayers.Add(new QueuedPlayer
                        {
                            GuildId = Context.Guild.Id,
                            ChannelId = lobby.ChannelId,
                            UserId = replacedWith.Id
                        });
                        db.SaveChanges();
                    }
                }

                var player = team1.SingleOrDefault(x => x.UserId == user.Id);
                if (player == null)
                {
                    player = team2.SingleOrDefault(x => x.UserId == user.Id);
                }

                if (player != null)
                {
                    db.TeamPlayers.Remove(player);
                    player.UserId = replacedWith.Id;
                    db.TeamPlayers.Add(player);
                    db.SaveChanges();

                    await SimpleEmbedAsync($"Team player {user.Mention} was replaced with {replacedWith.Mention}");
                }
                else
                {
                    //Check team captains
                    if (t1c != null && t1c.UserId == user.Id)
                    {
                        t1c.UserId = replacedWith.Id;
                        db.TeamCaptains.Update(t1c);
                        db.SaveChanges();
                    }
                    else if (t2c != null && t2c.UserId == user.Id)
                    {
                        t2c.UserId = replacedWith.Id;
                        db.TeamCaptains.Update(t2c);
                        db.SaveChanges();
                    }
                    else
                    {
                        await SimpleEmbedAsync($"{user.Mention} is not a member in the current game.");
                        return;
                    }

                    await SimpleEmbedAsync($"{user.Mention} as a team captain was replaced with {replacedWith.Mention}");
                }
            }
        }

        [Command("ForceJoin", RunMode = RunMode.Sync)]
        [Alias("FJ")]
        [Summary("Forcefully adds a user to queue, bypasses minimum points")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task ForceJoinAsync(SocketGuildUser user)
        {
            await ForceJoinAsync(new[] { user });
        }

        [Command("ForceRemove", RunMode = RunMode.Sync)]
        [Alias("FR")]
        [Summary("Forcefully removes a player for the queue")]
        [Preconditions.RequirePermission(PermissionLevel.Moderator)]
        public async Task ForceRemoveAsync(SocketGuildUser user)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var latestGame = db.GameResults.Where(x => x.LobbyId == Context.Channel.Id).OrderByDescending(x => x.GameId).FirstOrDefault();
                if (latestGame != null && latestGame.GameState == GameState.Picking)
                {
                    await SimpleEmbedAsync("You cannot remove a player from a game that is still being picked, try cancelling the game instead.", Color.DarkBlue);
                    return;
                }

                var queuedUser = db.QueuedPlayers.Find(Context.Channel.Id, user.Id);
                if (queuedUser != null)
                {
                    db.QueuedPlayers.Remove(queuedUser);
                    await SimpleEmbedAsync("Player was removed from queue.", Color.DarkBlue);
                    db.SaveChanges();
                }
                else
                {
                    await SimpleEmbedAsync("Player is not in queue and cannot be removed.", Color.DarkBlue);
                    return;
                }
            }
        }


        [Command("Pick", RunMode = RunMode.Sync)]
        [Alias("p")]
        [Summary("Picks the specified player(s) for your team.")]
        public async Task PickPlayerAsync(params SocketGuildUser[] users)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(Context.Channel.Id);
                if (lobby == null)
                {
                    await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                var latestGame = db.GameResults.Where(x => x.LobbyId == Context.Channel.Id).OrderByDescending(x => x.GameId).FirstOrDefault();
                if (latestGame == null)
                {
                    await SimpleEmbedAsync("There is no game to pick for.", Color.DarkBlue);
                    return;
                }

                if (latestGame.GameState != GameState.Picking)
                {
                    await SimpleEmbedAsync("Lobby is currently not picking teams.", Color.DarkBlue);
                    return;
                }

                var queue = db.GetQueuedPlayers(Context.Guild.Id, Context.Channel.Id).ToArray();
                var team1 = db.GetTeamPlayers(Context.Guild.Id, Context.Channel.Id, latestGame.GameId, 1).ToArray();
                var team2 = db.GetTeamPlayers(Context.Guild.Id, Context.Channel.Id, latestGame.GameId, 2).ToArray();
                var cap1 = db.GetTeamCaptain(Context.Guild.Id, Context.Channel.Id, latestGame.GameId, 1);
                var cap2 = db.GetTeamCaptain(Context.Guild.Id, Context.Channel.Id, latestGame.GameId, 2);
                //Ensure the player is eligible to join a team
                if (users.Any(user => !queue.Any(x => x.UserId == user.Id)))
                {
                    if (users.Length == 2)
                        await SimpleEmbedAndDeleteAsync("A selected player is not queued for this game.", Color.Red);
                    else
                        await SimpleEmbedAndDeleteAsync("Player is not queued for this game.", Color.Red);
                    return;
                }
                else if (users.Any(u => team1.Any(x => x.UserId == u.Id) || team2.Any(x => x.UserId == u.Id)))
                {
                    if (users.Length == 2)
                        await SimpleEmbedAndDeleteAsync("A selected player is already picked for a team.", Color.Red);
                    else
                        await SimpleEmbedAndDeleteAsync("Player is already picked for a team.", Color.Red);
                    return;
                }
                else if (users.Any(u => u.Id == cap1.UserId || u.Id == cap2.UserId))
                {
                    await SimpleEmbedAndDeleteAsync("You cannot select a captain for picking.", Color.Red);
                    return;
                }

                if (latestGame.PickOrder == CaptainPickOrder.PickTwo)
                {
                    latestGame = await PickTwoAsync(latestGame, users, cap1, cap2);
                }
                else if (latestGame.PickOrder == CaptainPickOrder.PickOne)
                {
                    latestGame = await PickOneAsync(latestGame, users, cap1, cap2);
                }
                else
                {
                    await SimpleEmbedAsync("There was an error picking your game.", Color.DarkRed);
                    return;
                }

                //game will be returned null from pickone/picktwo if there was an issue with a pick. The function already replies to just return.
                if (latestGame == null)
                {
                    return;
                }
                else
                {
                    var remaining = queue.Where(x => team1.All(u => u.UserId != x.UserId) && team2.All(u => u.UserId != x.UserId)).ToList();
                    if (remaining.Count == 1)
                    {
                        var lastUser = remaining.First();
                        db.TeamPlayers.Add(new TeamPlayer
                        {
                            GuildId = Context.Guild.Id,
                            ChannelId = Context.Channel.Id,
                            UserId = lastUser.UserId,
                            GameNumber = latestGame.GameId,
                            TeamNumber = 2
                        });
                    }
                }

                if (team1.Length + team2.Length + 2 >= queue.Length)
                {
                    //Teams have been filled.
                    latestGame.GameState = GameState.Undecided;

                    var res = GameService.GetGameMessage(latestGame, $"Game #{latestGame.GameId} Started",
                            GameFlag.gamestate,
                            GameFlag.lobby,
                            GameFlag.map,
                            GameFlag.usermentions,
                            GameFlag.time);

                    await ReplyAsync(res.Item1, false, res.Item2.Build());

                    if (lobby.GameReadyAnnouncementChannel != null)
                    {
                        var channel = Context.Guild.GetTextChannel(lobby.GameReadyAnnouncementChannel.Value);
                        if (channel != null)
                        {
                            await channel.SendMessageAsync(res.Item1, false, res.Item2.Build());
                        }
                    }

                    await MessageUsersAsync(queue.Select(x => x.UserId).ToArray(), x => MentionUtils.MentionUser(x), res.Item2.Build());
                }
                else
                {
                    var res = GameService.GetGameMessage(latestGame, "Player(s) picked.",
                            GameFlag.gamestate);
                    await ReplyAsync(PickResponse ?? "", false, res.Item2.Build());
                }

                db.SaveChanges();
            }
        }

        //TODO: if more than x maps are added to the lobby, announce 3 (or so) and allow users to vote on them to pick
        //Would have 1 minute timeout, then either picks the most voted map or randomly chooses from the most voted.
        //Would need to have a way of reducing the amount of repeats as well.
    }
}
