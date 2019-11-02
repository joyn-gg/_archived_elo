using Discord;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Modules
{
    public partial class LobbyManagement
    {
        public (ulong, ulong) GetCaptains(Lobby lobby, GameResult game, Random rnd)
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
                        var players = queue.Select(x => db.Players.Find(Context.Guild.Id, x.UserId)).Where(x => x != null).OrderByDescending(x => x.Points).Take(4).OrderBy(x => rnd.Next()).ToList();
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
                    var players = queue.Select(x => db.Players.Find(Context.Guild.Id, x.UserId)).Where(x => x != null).OrderByDescending(x => x.Points).Take(2).ToList();
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

        public async Task LobbyFullAsync(Lobby lobby)
        {
            using (var db = new Database())
            {
                await SimpleEmbedAsync("Queue is full. Picking teams...", Color.Blue);
                //Increment the game counter as there is now a new game.
                lobby.CurrentGameCount++;
                var game = new GameResult
                {
                    LobbyId = lobby.ChannelId,
                    GuildId = lobby.GuildId,
                    GamePickMode = lobby.TeamPickMode
                };
                db.GameResults.Add(game);
                db.SaveChanges();
                game = db.GetLatestGame(lobby);
                /*
                if (lobby.MapSelector != null && lobby.MapSelector.Maps.Count > 0)
                {
                    switch (lobby.MapSelector.Mode)
                    {
                        case MapSelector.MapMode.Random:
                            game.MapName = lobby.MapSelector.RandomMap(Random, true);
                            break;
                        case MapSelector.MapMode.Cycle:
                            game.MapName = lobby.MapSelector.NextMap(true);
                            break;
                        default:
                            break;
                    }
                }*/

                /*
                foreach (var queueUser in queue)
                {
                    //TODO: Fetch and update players later as some could be retrieved later like in the captains function.
                    var player = Service.GetPlayer(Context.Guild.Id, Context.User.Id);
                    if (player == null) continue;
                    player.AddGame(game.GameId);
                    Service.SavePlayer(player);
                }*/


                if (lobby.PlayersPerTeam == 1 &&
                    (lobby.TeamPickMode == PickMode.Captains_HighestRanked ||
                        lobby.TeamPickMode == PickMode.Captains_Random ||
                        lobby.TeamPickMode == PickMode.Captains_RandomHighestRanked))
                {
                    //Ensure that there isnt a captain pick mode if the teams only consist of one player
                    await SimpleEmbedAsync("Lobby sort mode was set to random, you cannot have a captain lobby for solo queues.", Color.DarkBlue);
                    lobby.TeamPickMode = PickMode.Random;
                }

                var team1 = db.GetTeam1(game).ToList();
                var team2 = db.GetTeam2(game).ToList();
                var queue = db.GetQueue(game).ToList();

                //Set team players/captains based on the team pick mode
                switch (lobby.TeamPickMode)
                {
                    case PickMode.Captains_HighestRanked:
                    case PickMode.Captains_Random:
                    case PickMode.Captains_RandomHighestRanked:
                        game.GameState = GameState.Picking;
                        var captains = GetCaptains(lobby, game, Random);
                        db.TeamCaptains.Add(new TeamCaptain
                        {
                            UserId = captains.Item1,
                            ChannelId = lobby.ChannelId,
                            GuildId = Context.Guild.Id,
                            TeamNumber = 1,
                            GameNumber = game.GameId
                        });
                        db.TeamCaptains.Add(new TeamCaptain
                        {
                            UserId = captains.Item2,
                            ChannelId = lobby.ChannelId,
                            GuildId = Context.Guild.Id,
                            TeamNumber = 2,
                            GameNumber = game.GameId
                        });

                        //TODO: Timer from when captains are mentioned to first pick time. Cancel game if command is not run.
                        var gameEmbed = new EmbedBuilder
                        {
                            Title = $"Current Teams."
                        };

                        var t1Users = GetMentionList(GetUserList(Context.Guild, team1.Select(x => x.UserId)));
                        var t2Users = GetMentionList(GetUserList(Context.Guild, team2.Select(x => x.UserId)));
                        var remainingPlayers = queue.Select(x => MentionUtils.MentionUser(x.UserId));
                        gameEmbed.AddField("Team 1", $"Captain: {MentionUtils.MentionUser(captains.Item1)}\n{string.Join("\n", t1Users)}");
                        gameEmbed.AddField("Team 2", $"Captain: {MentionUtils.MentionUser(captains.Item2)}\n{string.Join("\n", t2Users)}");
                        gameEmbed.AddField("Remaining Players", string.Join("\n", remainingPlayers));
                        await ReplyAsync($"Captains have been picked. Use the `pick` or `p` command to choose your players.\nCaptain 1: {MentionUtils.MentionUser(captains.Item1)}\nCaptain 2: {MentionUtils.MentionUser(captains.Item2)}", false, gameEmbed.Build());
                        break;
                    case PickMode.Random:
                        game.GameState = GameState.Undecided;
                        var shuffled = queue.OrderBy(x => Random.Next()).ToList();
                        db.TeamPlayers.AddRange(shuffled.Take(lobby.PlayersPerTeam).Select(x => new TeamPlayer
                        {
                            GuildId = Context.Guild.Id,
                            ChannelId = lobby.ChannelId,
                            UserId = x.UserId,
                            GameNumber = game.GameId,
                            TeamNumber = 1
                        }));
                        db.TeamPlayers.AddRange(shuffled.Skip(lobby.PlayersPerTeam).Take(lobby.PlayersPerTeam).Select(x => new TeamPlayer
                        {
                            GuildId = Context.Guild.Id,
                            ChannelId = lobby.ChannelId,
                            UserId = x.UserId,
                            GameNumber = game.GameId,
                            TeamNumber = 2
                        }));
                        break;
                    case PickMode.TryBalance:
                        game.GameState = GameState.Undecided;
                        var ordered = queue.OrderByDescending(x => db.Players.Find(Context.Guild.Id, x.UserId).Points).ToList();
                        foreach (var user in ordered)
                        {
                            if (team1.Count > team2.Count)
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = Context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = user.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 1
                                });
                            }
                            else
                            {
                                db.TeamPlayers.Add(new TeamPlayer
                                {
                                    GuildId = Context.Guild.Id,
                                    ChannelId = lobby.ChannelId,
                                    UserId = user.UserId,
                                    GameNumber = game.GameId,
                                    TeamNumber = 2
                                });
                            }
                        }
                        break;
                }

                db.QueuedPlayers.RemoveRange(queue);

                //TODO: Assign team members to specific roles and create a channel for chat within.
                if (lobby.TeamPickMode == PickMode.TryBalance || lobby.TeamPickMode == PickMode.Random)
                {
                    var res = GameService.GetGameMessage(game, $"Game #{game.GameId} Started",
                            GameFlag.lobby,
                            GameFlag.map,
                            GameFlag.time,
                            GameFlag.usermentions,
                            GameFlag.gamestate);

                    await ReplyAsync(res.Item1, false, res.Item2.Build());
                    if (lobby.GameReadyAnnouncementChannel != null)
                    {
                        var channel = Context.Guild.GetTextChannel(lobby.GameReadyAnnouncementChannel.Value);
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
                        await MessageUsersAsync(queue.Select(x => x.UserId).ToArray(), x => MentionUtils.MentionUser(x), x => GetMsg(game, team1, x));
                    }
                }

                db.SaveChanges();
            }

        }

        public Embed GetMsg(GameResult game, List<TeamPlayer> team1, ulong userid)
        {
            var msg2 = GameService.GetGameMessage(game, $"Game #{game.GameId} Started",
                GameFlag.map,
                GameFlag.time,
                GameFlag.usermentions,
                GameFlag.gamestate);

            var t1 = team1.Any(u => u.UserId == userid);
            var name = t1 ? "Team1" : "Team2";


            msg2.Item2.AddField("Game Info", $"Lobby: {MentionUtils.MentionChannel(game.LobbyId)}\nGame: {game.GameId}\nTeam: {name}\n{MentionUtils.MentionChannel(game.LobbyId)} {game.GameId} {name}");
            return msg2.Item2.Build();
        }

        public async Task<GameResult> PickOneAsync(GameResult game, SocketGuildUser[] users, TeamCaptain cap1, TeamCaptain cap2)
        {
            using (var db = new Database())
            {
                var uc = users.Count();
                var teamCaptain = game.Picks % 2 == 0 ? cap1 : cap2;
                var offTeamCaptain = game.Picks % 2 == 0 ? cap2 : cap2;

                if (Context.User.Id != teamCaptain.UserId)
                {
                    await SimpleEmbedAsync($"{Context.User.Mention} - It is currently the other captains turn to pick.", Color.Red);
                    return null;
                }

                if (uc == 0)
                {
                    await SimpleEmbedAsync($"{Context.User.Mention} - You must specify a player to pick.", Color.Red);
                    return null;
                }
                else if (uc != 1)
                {
                    await SimpleEmbedAsync($"{Context.User.Mention} - You can only specify one player for this pick.", Color.Red);
                    return null;
                }

                db.TeamPlayers.Add(GetPlayer(game, users[0], game.Picks % 2 == 0 ? 1 : 2));
                PickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
                game.Picks++;

                return game;
            }
        }

        private string PickResponse = null;

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

        public async Task<GameResult> PickTwoAsync(GameResult game, SocketGuildUser[] users, TeamCaptain cap1, TeamCaptain cap2)
        {
            using (var db = new Database())
            {
                var uc = users.Count();
                //Lay out custom logic for 1-2-2-1-1... pick order.

                var teamCaptain = game.Picks % 2 == 0 ? cap1 : cap2;
                var offTeamCaptain = game.Picks % 2 == 0 ? cap2 : cap2;

                if (game.Picks == 0)
                {
                    //captain 1 turn to pick.
                    if (Context.User.Id != cap1.UserId)
                    {
                        await SimpleEmbedAndDeleteAsync("It is currently the team 1 captains turn to pick.", Color.Red);
                        return null;
                    }

                    if (uc == 0)
                    {
                        await SimpleEmbedAndDeleteAsync("You must specify a player to pick.", Color.Red);
                        return null;
                    }
                    else if (uc != 1)
                    {
                        await SimpleEmbedAndDeleteAsync("You can only specify one player for the initial pick.", Color.Red);
                        return null;
                    }

                    db.TeamPlayers.Add(GetPlayer(game, users[0], 1));
                    game.Picks++;
                    PickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **2** players for the next pick.";
                }
                else if (game.Picks == 1)
                {
                    //cap 2 turn to pick. they get to pick 2 people.
                    if (Context.User.Id != cap2.UserId)
                    {
                        await SimpleEmbedAndDeleteAsync("It is currently the other captains turn to pick.", Color.Red);
                        return null;
                    }

                    if (uc != 2)
                    {
                        await SimpleEmbedAndDeleteAsync("You must specify 2 players for this pick.", Color.Red);
                        return null;
                    }

                    //Note adding a player multiple times (ie team captain to team 1) will not affect it because the players are a hashset.
                    db.TeamPlayers.Add(GetPlayer(game, users[0], 2));
                    db.TeamPlayers.Add(GetPlayer(game, users[1], 2));
                    game.Picks++;
                    PickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **2** players for the next pick.";
                    game.Picks++;
                }
                else if (game.Picks == 2)
                {
                    //cap 2 turn to pick. they get to pick 2 people.
                    if (Context.User.Id != cap1.UserId)
                    {
                        await SimpleEmbedAndDeleteAsync("It is currently the other captains turn to pick.", Color.Red);
                        return null;
                    }

                    if (uc != 2)
                    {
                        await SimpleEmbedAndDeleteAsync("You must specify 2 players for this pick.", Color.Red);
                        return null;
                    }

                    //Note adding a player multiple times (ie team captain to team 1) will not affect it because the players are a hashset.
                    db.TeamPlayers.Add(GetPlayer(game, users[0], 1));
                    db.TeamPlayers.Add(GetPlayer(game, users[1], 1));
                    game.Picks++;
                    PickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
                    game.Picks++;
                }
                else
                {
                    if (Context.User.Id != teamCaptain.UserId)
                    {
                        await SimpleEmbedAndDeleteAsync("It is currently the other captains turn to pick.", Color.Red);
                        return null;
                    }

                    if (uc == 0)
                    {
                        await SimpleEmbedAndDeleteAsync("You must specify a player to pick.", Color.Red);
                        return null;
                    }
                    else if (uc != 1)
                    {
                        await SimpleEmbedAndDeleteAsync("You can only specify one player for this pick.", Color.Red);
                        return null;
                    }

                    db.TeamPlayers.Add(GetPlayer(game, users[0], game.Picks % 2 == 0 ? 1 : 2));
                    game.Picks++;
                    PickResponse = $"{MentionUtils.MentionUser(offTeamCaptain.UserId)} can select **1** player for the next pick.";
                    game.Picks++;
                }

                db.SaveChanges();
                return game;
            }
        }
        
        public SocketGuildUser[] GetUserList(SocketGuild guild, IEnumerable<ulong> userIds)
        {
            return userIds.Select(x => guild.GetUser(x)).ToArray();
        }

        public string[] GetMentionList(IEnumerable<SocketGuildUser> users)
        {
            return users.Where(x => x != null).Select(x => x.Mention).ToArray();
        }

    }
}
