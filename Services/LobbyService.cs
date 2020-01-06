using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using Microsoft.EntityFrameworkCore;
using RavenBOT.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class LobbyService
    {
        public LobbyService(GameService gameService, Random random)
        {
            GameService = gameService;
            Random = random;
        }

        public (ulong, ulong) GetCaptains(ShardedCommandContext context, Lobby lobby, GameResult game, Random rnd)
        {
            using (var db = new Database())
            {
                ulong cap1 = 0;
                ulong cap2 = 0;
                var queue = db.GetQueue(lobby).ToList();
                if (lobby.TeamPickMode == PickMode.Captains_RandomHighestRanked)
                {
                    //Select randomly from the top 4 ranked players in the queue
                    if (queue.Count >= 4)
                    {
                        var players = queue.Select(x => db.Players.Find(context.Guild.Id, x.UserId)).Where(x => x != null).OrderByDescending(x => x.Points).Take(4).OrderBy(x => rnd.Next()).ToList();
                        cap1 = players[0].UserId;
                        cap2 = players[1].UserId;
                    }
                    //Select the two players at random.
                    else
                    {
                        var randomised = queue.OrderBy(x => rnd.Next()).Take(2).ToList();
                        cap1 = randomised[0].UserId;
                        cap2 = randomised[1].UserId;
                    }
                }
                else if (lobby.TeamPickMode == PickMode.Captains_Random)
                {
                    //Select two players at random.
                    var randomised = queue.OrderBy(x => rnd.Next()).Take(2).ToList();
                    cap1 = randomised[0].UserId;
                    cap2 = randomised[1].UserId;
                }
                else if (lobby.TeamPickMode == PickMode.Captains_HighestRanked)
                {
                    //Select top two players
                    var players = queue.Select(x => db.Players.Find(context.Guild.Id, x.UserId)).Where(x => x != null).OrderByDescending(x => x.Points).Take(2).ToList();
                    cap1 = players[0].UserId;
                    cap2 = players[1].UserId;
                }
                else
                {
                    throw new Exception("Unknown captain pick mode.");
                }

                return (cap1, cap2);
            }
        }

        public virtual async Task LobbyFullAsync(ShardedCommandContext context, Lobby lobby)
        {
            using (var db = new Database())
            {
                lobby = db.Lobbies.Find(lobby.ChannelId);
                await context.Channel.SendMessageAsync("", false, "Queue is full. Picking teams...".QuickEmbed(Color.Blue));
                //Increment the game counter as there is now a new game.
                var vals = ((IQueryable<GameResult>)db.GameResults).Where(x => x.LobbyId == lobby.ChannelId).ToArray();
                lobby.CurrentGameCount = vals.Length == 0 ? 1 : vals.Max(x => x.GameId) + 1;
                var game = new GameResult
                {
                    LobbyId = lobby.ChannelId,
                    GuildId = lobby.GuildId,
                    GamePickMode = lobby.TeamPickMode,
                    PickOrder = lobby.CaptainPickOrder,
                    GameId = lobby.CurrentGameCount
                };

                var maps = db.Maps.Where(x => x.ChannelId == lobby.ChannelId).ToArray();
                if (maps.Length != 0)
                {
                    var map = maps.OrderByDescending(x => Random.Next()).First();
                    game.MapName = map.MapName;
                }

                db.Lobbies.Update(lobby);
                db.GameResults.Add(game);
                db.SaveChanges();

                if (lobby.PlayersPerTeam == 1 &&
                    (lobby.TeamPickMode == PickMode.Captains_HighestRanked ||
                        lobby.TeamPickMode == PickMode.Captains_Random ||
                        lobby.TeamPickMode == PickMode.Captains_RandomHighestRanked))
                {
                    //Ensure that there isnt a captain pick mode if the teams only consist of one player
                    await context.Channel.SendMessageAsync("", false, "Lobby sort mode was set to random, you cannot have a captain lobby for solo queues.".QuickEmbed(Color.DarkBlue));
                    lobby.TeamPickMode = PickMode.Random;
                }

                var queue = db.GetQueue(game).ToList();

                var team1Players = new List<ulong>();
                var team2Players = new List<ulong>();
                var t1Added = new List<ulong>();
                var t2Added = new List<ulong>();
                //Set team players/captains based on the team pick mode
                switch (lobby.TeamPickMode)
                {
                    case PickMode.Captains_HighestRanked:
                    case PickMode.Captains_Random:
                    case PickMode.Captains_RandomHighestRanked:
                        game.GameState = GameState.Picking;
                        var captains = GetCaptains(context, lobby, game, Random);
                        db.TeamCaptains.Add(new TeamCaptain
                        {
                            UserId = captains.Item1,
                            ChannelId = lobby.ChannelId,
                            GuildId = context.Guild.Id,
                            TeamNumber = 1,
                            GameNumber = game.GameId
                        });
                        db.TeamCaptains.Add(new TeamCaptain
                        {
                            UserId = captains.Item2,
                            ChannelId = lobby.ChannelId,
                            GuildId = context.Guild.Id,
                            TeamNumber = 2,
                            GameNumber = game.GameId
                        });

                        //TODO: Timer from when captains are mentioned to first pick time. Cancel game if command is not run.
                        var gameEmbed = new EmbedBuilder
                        {
                            Title = $"Game #{game.GameId} - Current Teams."
                        };

                        var remainingPlayers = queue.Where(x => x.UserId != captains.Item1 && x.UserId != captains.Item2).Select(x => MentionUtils.MentionUser(x.UserId));                        
                        if (lobby.HostSelectionMode != HostSelection.None)
                        {
                            var qIds = queue.Select(x => x.UserId).ToList();
                            var players = db.Players.Where(p => qIds.Contains(p.UserId)).ToArray();
                            if (lobby.HostSelectionMode == HostSelection.HighestRanked)
                            {
                                var selected = players.OrderByDescending(x => x.Points).First();
                                gameEmbed.AddField("Selected Host", MentionUtils.MentionUser(selected.UserId));
                            }
                            else if (lobby.HostSelectionMode == HostSelection.Random)
                            {
                                var selected = players.OrderByDescending(x => Random.Next()).First();
                                gameEmbed.AddField("Selected Host", MentionUtils.MentionUser(selected.UserId));
                            }
                        }
                        gameEmbed.AddField("Team 1", $"Captain: {MentionUtils.MentionUser(captains.Item1)}");
                        gameEmbed.AddField("Team 2", $"Captain: {MentionUtils.MentionUser(captains.Item2)}");
                        gameEmbed.AddField("Remaining Players", string.Join("\n", remainingPlayers));

                        await context.Channel.SendMessageAsync($"Captains have been picked. Use the `pick` or `p` command to choose your players.\nCaptain 1: {MentionUtils.MentionUser(captains.Item1)}\nCaptain 2: {MentionUtils.MentionUser(captains.Item2)}", false, gameEmbed.Build());
                        break;
                    case PickMode.Random:
                        game.GameState = GameState.Undecided;
                        var shuffled = queue.OrderBy(x => Random.Next()).ToList();
                        var partyPlayers = db.PartyMembers.Where(x => x.ChannelId == context.Channel.Id).ToArray();
                        var partyPlayerIds = partyPlayers.Select(x => x.UserId);
                        var parties = partyPlayers.GroupBy(x => x.PartyHost).ToArray();

                        var partySelected = new List<ulong>();
                        int partySelection = 0;
                        foreach (var party in parties.OrderBy(x => Random.Next()))
                        {
                            //Skip where a party member is not in the queue.
                            if (party.Any(x => shuffled.All(sh => sh.UserId != x.UserId)))
                            {
                                continue;
                            }

                            //Ignore parties that are too large.
                            if (party.Count() > lobby.PlayersPerTeam)
                            {
                                continue;
                            }
                            var partyIds = party.Select(x => x.UserId);


                            if (partySelection % 2 == 0)
                            {
                                if (t1Added.Count + party.Count() > lobby.PlayersPerTeam) continue;

                                db.TeamPlayers.AddRange(party.Select(x => new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = x.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 1
                                }));
                                t1Added.AddRange(partyIds);
                                team1Players.AddRange(partyIds);
                            }
                            else
                            {
                                if (t2Added.Count + party.Count() > lobby.PlayersPerTeam) continue;

                                db.TeamPlayers.AddRange(party.Select(x => new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = x.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 2
                                }));
                                t2Added.AddRange(partyIds);
                                team2Players.AddRange(partyIds);
                            }
                            partySelected.AddRange(partyIds);
                            partySelection++;

                        }

                        //Add remaining players to teams.
                        foreach (var player in shuffled.Where(x => !partySelected.Contains(x.UserId)))
                        {
                            if (t1Added.Count > t2Added.Count)
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = player.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 2
                                });
                                t2Added.Add(player.UserId);
                                team2Players.Add(player.UserId);
                            }
                            else
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = player.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 1
                                });
                                t1Added.Add(player.UserId);
                                team1Players.Add(player.UserId);
                            }
                        }

                        db.QueuedPlayers.RemoveRange(queue);

                        break;
                    case PickMode.TryBalance:
                        game.GameState = GameState.Undecided;
                        var ordered = queue.OrderByDescending(x => db.Players.Find(context.Guild.Id, x.UserId).Points).ToList();
                        //0-0
                        //1-0
                        //1-1
                        //2-1
                        //2-2
                        //...

                        foreach (var user in ordered)
                        {
                            if (t1Added.Count <= t2Added.Count)
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = user.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 1
                                });
                                t1Added.Add(user.UserId);
                            }
                            else
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = user.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 2
                                });
                                t2Added.Add(user.UserId);
                            }
                        }


                        db.QueuedPlayers.RemoveRange(queue);

                        break;
                }

                db.SaveChanges();

                if (lobby.TeamPickMode == PickMode.TryBalance || lobby.TeamPickMode == PickMode.Random)
                {                    
                    var res = GameService.GetGameMessage(game, $"Game #{game.GameId} Started",
                            GameFlag.lobby,
                            GameFlag.map,
                            GameFlag.time,
                            GameFlag.usermentions,
                            GameFlag.gamestate);

                    if (lobby.HostSelectionMode != HostSelection.None)
                    {
                        var qIds = queue.Select(x => x.UserId).ToList();
                        var players = db.Players.Where(p => qIds.Contains(p.UserId)).ToArray();
                        if (lobby.HostSelectionMode == HostSelection.HighestRanked)
                        {
                            var selected = players.OrderByDescending(x => x.Points).First();
                            res.Item2.AddField("Selected Host", MentionUtils.MentionUser(selected.UserId));
                        }
                        else if (lobby.HostSelectionMode == HostSelection.Random)
                        {
                            var selected = players.OrderByDescending(x => Random.Next()).First();
                            res.Item2.AddField("Selected Host", MentionUtils.MentionUser(selected.UserId));
                        }
                    }

                    await context.Channel.SendMessageAsync(res.Item1, false, res.Item2.Build());
                    if (lobby.GameReadyAnnouncementChannel != null)
                    {
                        var channel = context.Guild.GetTextChannel(lobby.GameReadyAnnouncementChannel.Value);
                        if (channel != null)
                        {
                            if (lobby.MentionUsersInReadyAnnouncement)
                            {
                                await channel.SendMessageAsync(res.Item1, false, res.Item2.Build());
                            }
                            else
                            {
                                var res2 = GameService.GetGameMessage(game, $"Game #{game.GameId} Started",
                                    GameFlag.lobby,
                                    GameFlag.map,
                                    GameFlag.time,
                                    GameFlag.gamestate);
                                await channel.SendMessageAsync(res2.Item1, false, res2.Item2.Build());
                            }
                        }
                    }

                    if (lobby.DmUsersOnGameReady)
                    {
                        foreach (var user in queue.Select(x => x.UserId).ToArray())
                        {
                            try
                            {
                                var t1 = db.GetTeamFull(game, 1);
                                var u = context.Client.GetUser(user);
                                var _ = Task.Run(async () =>
                                    {
                                        await u.SendMessageAsync(MentionUtils.MentionUser(user), false, GetMsg(game, t1.ToList(), user));
                                    });
                            }
                            catch
                            {
                                //
                            }
                        }
                    }
                }

                db.SaveChanges();
            }

        }

        public Embed GetMsg(GameResult game, List<ulong> team1, ulong userid)
        {
            var msg2 = GameService.GetGameMessage(game, $"Game #{game.GameId} Started",
                GameFlag.map,
                GameFlag.time,
                GameFlag.usermentions,
                GameFlag.gamestate);

            var t1 = team1.Any(u => u == userid);
            var name = t1 ? "Team1" : "Team2";


            msg2.Item2.AddField("Game Info", $"Lobby: {MentionUtils.MentionChannel(game.LobbyId)}\nGame: {game.GameId}\nTeam: {name}\n{MentionUtils.MentionChannel(game.LobbyId)} {game.GameId} {name}");
            return msg2.Item2.Build();
        }

        public virtual async Task<(GameResult, string)> PickOneAsync(Database db, ShardedCommandContext context, GameResult game, SocketGuildUser[] users, TeamCaptain cap1, TeamCaptain cap2)
        {
            var uc = users.Count();
            var teamCaptain = game.Picks % 2 == 0 ? cap1 : cap2;
            var offTeamCaptain = game.Picks % 2 == 0 ? cap2 : cap1;

            if (context.User.Id != teamCaptain.UserId)
            {
                await context.Channel.SendMessageAsync("", false, $"{context.User.Mention} - It is currently the other captains turn to pick.".QuickEmbed(Color.Red));
                return (null, null);
            }

            if (uc == 0)
            {
                await context.Channel.SendMessageAsync("", false, $"{context.User.Mention} - You must specify a player to pick.".QuickEmbed(Color.Red));
                return (null, null);
            }
            else if (uc != 1)
            {
                await context.Channel.SendMessageAsync("", false, $"{context.User.Mention} - You can only specify one player for this pick.".QuickEmbed(Color.Red));
                return (null, null);
            }

            db.TeamPlayers.Add(GetPlayer(game, users[0], game.Picks % 2 == 0 ? 1 : 2));
            var pickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
            return (game, pickResponse);
        }


        public GameService GameService { get; }
        public Random Random { get; }

        public TeamPlayer GetPlayer(GameResult game, SocketGuildUser user, int team)
        {
            return new TeamPlayer
            {
                ChannelId = game.LobbyId,
                UserId = user.Id,
                GuildId = game.GuildId,
                TeamNumber = team,
                GameNumber = game.GameId
            };
        }

        public virtual async Task<(GameResult, string)> PickTwoAsync(Database db, ShardedCommandContext context, Lobby lobby, GameResult game, SocketGuildUser[] users, TeamCaptain cap1, TeamCaptain cap2)
        {
            var uc = users.Count();
            //Lay out custom logic for 1-2-2-1-1... pick order.

            var teamCaptain = game.Picks % 2 == 0 ? cap1 : cap2;
            var offTeamCaptain = game.Picks % 2 == 0 ? cap2 : cap1;
            string pickResponse;
            if (game.Picks == 0)
            {
                //captain 1 turn to pick.
                if (context.User.Id != cap1.UserId)
                {
                    await context.Channel.SendMessageAsync("", false, ("It is currently the team 1 captains turn to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                if (uc == 0)
                {
                    await context.Channel.SendMessageAsync("", false, ("You must specify a player to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }
                else if (uc != 1)
                {
                    await context.Channel.SendMessageAsync("", false, ("You can only specify one player for the initial pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                db.TeamPlayers.Add(GetPlayer(game, users[0], 1));
                pickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **2** players for the next pick.";
            }
            else if (game.Picks == 1)
            {
                //cap 2 turn to pick. they get to pick 2 people.
                if (context.User.Id != cap2.UserId)
                {
                    await context.Channel.SendMessageAsync("", false, ("It is currently the other captains turn to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                if (uc != 2)
                {
                    await context.Channel.SendMessageAsync("", false, ("You must specify 2 players for this pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                //Note adding a player multiple times (ie team captain to team 1) will not affect it because the players are a hashset.
                db.TeamPlayers.Add(GetPlayer(game, users[0], 2));
                db.TeamPlayers.Add(GetPlayer(game, users[1], 2));
                pickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **2** players for the next pick.";
            }
            else if (game.Picks == 2)
            {
                //cap 2 turn to pick. they get to pick 2 people.
                if (context.User.Id != cap1.UserId)
                {
                    await context.Channel.SendMessageAsync("", false, ("It is currently the other captains turn to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                if (uc != 2)
                {
                    await context.Channel.SendMessageAsync("", false, ("You must specify 2 players for this pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                //Note adding a player multiple times (ie team captain to team 1) will not affect it because the players are a hashset.
                db.TeamPlayers.Add(GetPlayer(game, users[0], 1));
                db.TeamPlayers.Add(GetPlayer(game, users[1], 1));
                pickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
            }
            else
            {
                if (context.User.Id != teamCaptain.UserId)
                {
                    await context.Channel.SendMessageAsync("", false, ("It is currently the other captains turn to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                if (uc == 0)
                {
                    await context.Channel.SendMessageAsync("", false, ("You must specify a player to pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }
                else if (uc != 1)
                {
                    await context.Channel.SendMessageAsync("", false, ("You can only specify one player for this pick.".QuickEmbed(Color.Red)));
                    return (null, null);
                }

                db.TeamPlayers.Add(GetPlayer(game, users[0], game.Picks % 2 == 0 ? 1 : 2));

                pickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
            }

            return (game, pickResponse);
        }

        public static SocketGuildUser[] GetUserList(SocketGuild guild, IEnumerable<ulong> userIds)
        {
            return userIds.Select(x => guild.GetUser(x)).ToArray();
        }

        public static string[] GetMentionList(IEnumerable<SocketGuildUser> users)
        {
            return users.Where(x => x != null).Select(x => x.Mention).ToArray();
        }
    }
}
