using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;

namespace RavenBOT.ELO.Modules.Modules
{
    public partial class LobbyManagement
    {
        public async Task LobbyFullAsync()
        {
            await ReplyAsync("Queue is full. Picking teams...");
            //Increment the game counter as there is now a new game.
            CurrentLobby.CurrentGameCount++;
            var game = new GameResult(CurrentLobby.CurrentGameCount, Context.Channel.Id, Context.Guild.Id, CurrentLobby.TeamPickMode);
            game.Queue = CurrentLobby.Queue;

            if (CurrentLobby.MapSelector != null)
            {
                switch (CurrentLobby.MapSelector.Mode)
                {
                    case MapSelector.MapMode.Random:
                        game.MapName = CurrentLobby.MapSelector.RandomMap(Random, true);
                        break;
                    case MapSelector.MapMode.Cycle:
                        game.MapName = CurrentLobby.MapSelector.NextMap(true);
                        break;
                    default:
                        break;
                }
            }

            foreach (var userId in game.Queue)
            {
                //TODO: Fetch and update players later as some could be retrieved later like in the captains function.
                var player = Service.GetPlayer(Context.Guild.Id, Context.User.Id);
                if (player == null) continue;
                player.AddGame(game.GameId);
                Service.SavePlayer(player);
            }

            CurrentLobby.Queue = new HashSet<ulong>();

            if (CurrentLobby.PlayersPerTeam == 1 &&
                (CurrentLobby.TeamPickMode == Lobby.PickMode.Captains_HighestRanked ||
                    CurrentLobby.TeamPickMode == Lobby.PickMode.Captains_Random ||
                    CurrentLobby.TeamPickMode == Lobby.PickMode.Captains_RandomHighestRanked))
            {
                //Ensure that there isnt a captain pick mode if the teams only consist of one player
                await ReplyAsync("Lobby sort mode was set to random, you cannot have a captain lobby for solo queues.");
                CurrentLobby.TeamPickMode = Lobby.PickMode.Random;
            }

            //Set team players/captains based on the team pick mode
            switch (CurrentLobby.TeamPickMode)
            {
                case Lobby.PickMode.Captains_HighestRanked:
                case Lobby.PickMode.Captains_Random:
                case Lobby.PickMode.Captains_RandomHighestRanked:
                    game.GameState = GameResult.State.Picking;
                    var captains = Service.GetCaptains(CurrentLobby, game, Random);
                    game.Team1.Captain = captains.Item1;
                    game.Team2.Captain = captains.Item2;

                    //TODO: Timer from when captains are mentioned to first pick time. Cancel game if command is not run.
                    var gameEmbed = new EmbedBuilder
                    {
                        Title = $"Current Teams."
                    };

                    var t1Users = GetMentionList(GetUserList(Context.Guild, game.Team1.Players));
                    var t2Users = GetMentionList(GetUserList(Context.Guild, game.Team2.Players));
                    var remainingPlayers = GetMentionList(GetUserList(Context.Guild, game.Queue.Where(x => !game.Team1.Players.Contains(x) && !game.Team2.Players.Contains(x))));
                    gameEmbed.AddField("Team 1", $"Captain: {MentionUtils.MentionUser(game.Team1.Captain)}\n{string.Join("\n", t1Users)}");
                    gameEmbed.AddField("Team 2", $"Captain: {MentionUtils.MentionUser(game.Team2.Captain)}\n{string.Join("\n", t2Users)}");
                    gameEmbed.AddField("Remaining Players", string.Join("\n", remainingPlayers));
                    await ReplyAsync($"Captains have been picked. Use the `pick` or `p` command to choose your players.\nCaptain 1: {MentionUtils.MentionUser(game.Team1.Captain)}\nCaptain 2: {MentionUtils.MentionUser(game.Team2.Captain)}", false, gameEmbed.Build());
                    break;
                case Lobby.PickMode.Random:
                    game.GameState = GameResult.State.Undecided;
                    var shuffled = game.Queue.OrderBy(x => Random.Next()).ToList();
                    game.Team1.Players = shuffled.Take(CurrentLobby.PlayersPerTeam).ToHashSet();
                    game.Team2.Players = shuffled.Skip(CurrentLobby.PlayersPerTeam).Take(CurrentLobby.PlayersPerTeam).ToHashSet();
                    break;
                case Lobby.PickMode.TryBalance:
                    game.GameState = GameResult.State.Undecided;
                    var ordered = game.Queue.Select(x => Service.GetPlayer(Context.Guild.Id, x)).Where(x => x != null).OrderByDescending(x => x.Points).ToList();
                    foreach (var user in ordered)
                    {
                        if (game.Team1.Players.Count > game.Team2.Players.Count)
                        {
                            game.Team2.Players.Add(user.UserId);
                        }
                        else
                        {
                            game.Team1.Players.Add(user.UserId);
                        }
                    }
                    break;
            }

            //TODO: Assign team members to specific roles and create a channel for chat within.
            if (CurrentLobby.TeamPickMode == Lobby.PickMode.TryBalance || CurrentLobby.TeamPickMode == Lobby.PickMode.Random)
            {
                var res = Service.GetGameMessage(Context, game, $"Game #{game.GameId} Started", 
                        ELOService.GameFlag.lobby,
                        ELOService.GameFlag.map,
                        ELOService.GameFlag.time,
                        ELOService.GameFlag.usermentions,
                        ELOService.GameFlag.gamestate);

                await ReplyAsync(res.Item1, false, res.Item2.Build());
                if (CurrentLobby.GameReadyAnnouncementChannel != 0)
                {
                    var channel = Context.Guild.GetTextChannel(CurrentLobby.GameReadyAnnouncementChannel);
                    if (channel != null)
                    {
                        if (CurrentLobby.MentionUsersInReadyAnnouncement)
                        {
                            await channel.SendMessageAsync(res.Item1, false, res.Item2.Build());
                        }
                        else
                        {
                            var res2 = Service.GetGameMessage(Context, game, $"Game #{game.GameId} Started", 
                                ELOService.GameFlag.lobby,
                                ELOService.GameFlag.map,
                                ELOService.GameFlag.time,
                                ELOService.GameFlag.gamestate);
                            await channel.SendMessageAsync(res2.Item1, false, res2.Item2.Build());
                        }
                    }
                }

                if (CurrentLobby.DmUsersOnGameReady)
                {
                    await MessageUsersAsync(game.Queue.ToArray(), x => MentionUtils.MentionUser(x), x =>
                    {
                        var msg2 = Service.GetGameMessage(Context, game, $"Game #{game.GameId} Started",
                        ELOService.GameFlag.map,
                        ELOService.GameFlag.time,
                        ELOService.GameFlag.usermentions,
                        ELOService.GameFlag.gamestate);

                        string teamName;
                        if (game.Team1.Players.Contains(x))
                        {
                            teamName = "Team1";
                        }
                        else
                        {
                            teamName = "Team2";
                        }

                        msg2.Item2.AddField("Game Info", $"Lobby: {MentionUtils.MentionChannel(game.LobbyId)}\nGame: {game.GameId}\nTeam: {teamName}\n{MentionUtils.MentionChannel(game.LobbyId)} {game.GameId} {teamName}");
                        return msg2.Item2.Build();
                    });
                }
            }

            Service.SaveGame(game);
        }

        public async Task<GameResult> PickOneAsync(GameResult game, SocketGuildUser[] users)
        {
            var uc = users.Count();
            var team = game.GetTeam();

            if (Context.User.Id != team.Captain)
            {
                await ReplyAsync("It is currently the other captains turn to pick.");
                return null;
            }

            if (uc == 0)
            {
                await ReplyAsync("You must specify a player to pick.");
                return null;
            }
            else if (uc != 1)
            {
                await ReplyAsync("You can only specify one player for this pick.");
                return null;
            }

            team.Players.Add(users.First().Id);
            PickResponse = $"{MentionUtils.MentionUser(game.GetOffTeam().Captain)} can select **1** player for the next pick.";
            game.Picks++;

            return game;
        }

        private string PickResponse = null;

        public async Task<GameResult> PickTwoAsync(GameResult game, SocketGuildUser[] users)
        {
            var uc = users.Count();
            //Lay out custom logic for 1-2-2-1-1... pick order.

            var team = game.GetTeam();
            var offTeam = game.GetOffTeam();

            if (game.Picks == 0)
            {
                //captain 1 turn to pick.
                if (Context.User.Id != team.Captain)
                {
                    await ReplyAsync("It is currently the team 1 captains turn to pick.");
                    return null;
                }

                if (uc == 0)
                {
                    await ReplyAsync("You must specify a player to pick.");
                    return null;
                }
                else if (uc != 1)
                {
                    await ReplyAsync("You can only specify one player for the initial pick.");
                    return null;
                }

                team.Players.Add(team.Captain);
                offTeam.Players.Add(offTeam.Captain);
                team.Players.Add(users.First().Id);
                game.Picks++;
                PickResponse = $"{MentionUtils.MentionUser(offTeam.Captain)} can select **2** players for the next pick.";
            }
            else if (game.Picks <= 2)
            {
                //cap 2 turn to pick. they get to pick 2 people.
                if (Context.User.Id != team.Captain)
                {
                    await ReplyAsync("It is currently the other captains turn to pick.");
                    return null;
                }

                if (uc != 2)
                {
                    await ReplyAsync("You must specify 2 players for this pick.");
                    return null;
                }

                //Note adding a player multiple times (ie team captain to team 1) will not affect it because the players are a hashset.
                team.Players.Add(team.Captain);
                team.Players.UnionWith(users.Select(x => x.Id));
                PickResponse = $"{MentionUtils.MentionUser(offTeam.Captain)} can select **2** players for the next pick.";
                game.Picks++;
            }
            else
            {
                if (Context.User.Id != team.Captain)
                {
                    await ReplyAsync("It is currently the other captains turn to pick.");
                    return null;
                }

                if (uc == 0)
                {
                    await ReplyAsync("You must specify a player to pick.");
                    return null;
                }
                else if (uc != 1)
                {
                    await ReplyAsync("You can only specify one player for this pick.");
                    return null;
                }

                team.Players.Add(users.First().Id);
                PickResponse = $"{MentionUtils.MentionUser(team.Captain)} can select **1** player for the next pick.";
                game.Picks++;
            }

            return game;
        }

        public ulong[] RemainingPlayers(GameResult game)
        {
            return game.Queue.Where(x => !game.Team1.Players.Contains(x) && !game.Team2.Players.Contains(x) &&
                x != game.Team1.Captain && x != game.Team2.Captain).ToArray();
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