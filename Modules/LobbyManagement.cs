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
    public partial class LobbyManagement : ReactiveBase
    {
        public ELOService Service { get; }

        public LobbyManagement(ELOService service, Random random)
        {
            Service = service;
            Random = random;
        }

        public Lobby CurrentLobby;

        public async Task<bool> CheckLobbyAsync()
        {
            var response = Service.IsLobby(Context.Guild.Id, Context.Channel.Id);
            if (response.Item1)
            {
                CurrentLobby = response.Item2;
                return true;
            }

            await SimpleEmbedAndDeleteAsync("Current channel is not a lobby.", Color.Red);
            return false;
        }

        public Player CurrentPlayer;

        public async Task<bool> CheckRegisteredAsync()
        {
            var response = Service.GetPlayer(Context.Guild.Id, Context.User.Id);
            if (response != null)
            {
                CurrentPlayer = response;
                return true;
            }

            await SimpleEmbedAndDeleteAsync("You are not registered.", Color.Red);
            return false;
        }

        //TODO: Player queuing via reactions to a message.

        public Random Random { get; }

        //TODO: Replace command
        //TODO: Map stuff
        //TODO: Assign teams to temp roles until game result is decided.
        //TODO: Assign a game to a specific channel until game result is decided.
        //TODO: Allow players to party up for a lobby

        [Command("ClearQueue", RunMode = RunMode.Sync)]
        [Alias("Clear Queue", "clearq", "clearque")]
        [Summary("Clears the current queue.")]
        [Preconditions.RequireModerator]
        public async Task ClearQueueAsync()
        {
            if (!await CheckLobbyAsync())
            {
                return;
            }

            var game = Service.GetCurrentGame(CurrentLobby);
            if (game != null)
            {
                if (game.GameState == GameResult.State.Picking)
                {
                    await SimpleEmbedAndDeleteAsync("Current game is being picked, cannot clear queue.", Color.Red);
                    return;
                }
            }
            CurrentLobby.Queue.Clear();
            Service.SaveLobby(CurrentLobby);
            await SimpleEmbedAsync("Queue Cleared.", Color.Green);
        }

        [Command("ForceJoin", RunMode = RunMode.Sync)]
        [Summary("Forcefully adds a user to queue, bypasses minimum points")]
        [Preconditions.RequireModerator]
        public async Task ForceJoinAsync(SocketGuildUser user)
        {
            if (!await CheckLobbyAsync())
            {
                return;
            }

            if (!user.IsRegistered(Service, out var response))
            {
                await SimpleEmbedAndDeleteAsync("User is not registered.", Color.Red);
                return;
            }

            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                //Queue will be reset after teams are completely picked.
                await SimpleEmbedAndDeleteAsync("Queue is full, wait for teams to be chosen before joining.", Color.DarkBlue);
                return;
            }

            if (Service.GetOrCreateCompetition(Context.Guild.Id).BlockMultiQueueing)
            {
                var lobbies = Service.GetLobbies(Context.Guild.Id);
                var lobbyMatches = lobbies.Where(x => x.Queue.Contains(user.Id));
                if (lobbyMatches.Any())
                {
                    var guildChannels = lobbyMatches.Select(x => MentionUtils.MentionChannel(x.ChannelId));
                    await SimpleEmbedAndDeleteAsync($"MultiQueuing is not enabled in this server.\nUser must leave: {string.Join("\n", guildChannels)}", Color.Red);
                    return;
                }
            }

            var currentGame = Service.GetCurrentGame(CurrentLobby);
            if (currentGame != null)
            {
                if (currentGame.GameState == GameResult.State.Picking)
                {
                    await SimpleEmbedAndDeleteAsync("Current game is picking teams, wait until this is completed.", Color.Red);
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(user.Id))
            {
                await SimpleEmbedAndDeleteAsync("User is already queued.", Color.DarkBlue);
                return;
            }

            CurrentLobby.Queue.Add(user.Id);
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                await LobbyFullAsync();
            }
            else
            {
                if (Context.Guild.CurrentUser.GuildPermissions.AddReactions && CurrentLobby.ReactOnJoinLeave)
                {
                    await Context.Message.AddReactionAsync(new Emoji("✅"));
                }
                else
                {
                    await SimpleEmbedAsync("Added to queue.", Color.Green);
                }
            }

            Service.SaveLobby(CurrentLobby);
        }

        [Command("ForceRemove", RunMode = RunMode.Sync)]
        [Summary("Forcefully removes a player for the queue")]
        [Preconditions.RequireModerator]
        public async Task ForceRemoveAsync(SocketGuildUser user)
        {
            if (!await CheckLobbyAsync())
            {
                return;
            }

            var game = Service.GetCurrentGame(CurrentLobby);
            if (game != null)
            {
                if (game.GameState == GameResult.State.Picking)
                {
                    await SimpleEmbedAndDeleteAsync("You cannot remove a player from a game that is still being picked, try cancelling the game instead.", Color.DarkBlue);
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(user.Id))
            {
                CurrentLobby.Queue.Remove(user.Id);
                await SimpleEmbedAsync("Player was removed from queue.", Color.DarkBlue);
                Service.SaveLobby(CurrentLobby);
            }
            else
            {
                await SimpleEmbedAsync("Player is not in queue and cannot be removed.", Color.DarkBlue);
                return;
            }
        }

        [Command("Pick", RunMode = RunMode.Sync)]
        [Alias("p")]
        [Summary("Picks the specified player(s) for your team.")]
        public async Task PickPlayerAsync(params SocketGuildUser[] users)
        {
            if (!await CheckLobbyAsync() || !await CheckRegisteredAsync())
            {
                return;
            }

            var game = Service.GetCurrentGame(CurrentLobby);
            if (game.GameState != GameResult.State.Picking)
            {
                await SimpleEmbedAndDeleteAsync("Lobby is currently not picking teams.", Color.DarkBlue);
                return;
            }

            //Ensure the player is eligible to join a team
            if (users.Any(user => !game.Queue.Contains(user.Id)))
            {
                if (users.Length == 2)
                    await SimpleEmbedAndDeleteAsync("A selected player is not queued for this game.", Color.Red);
                else
                    await SimpleEmbedAndDeleteAsync("Player is not queued for this game.", Color.Red);
                return;
            }
            else if (users.Any(u => game.Team1.Players.Contains(u.Id) || game.Team2.Players.Contains(u.Id)))
            {
                if (users.Length == 2)
                    await SimpleEmbedAndDeleteAsync("A selected player is already picked for a team.", Color.Red);
                else
                    await SimpleEmbedAndDeleteAsync("Player is already picked for a team.", Color.Red);
                return;
            }
            else if (users.Any(u => u.Id == game.Team1.Captain || u.Id == game.Team2.Captain))
            {
                await SimpleEmbedAndDeleteAsync("You cannot select a captain for picking.", Color.Red);
                return;
            }

            if (game.PickOrder == GameResult.CaptainPickOrder.PickTwo)
            {
                game = await PickTwoAsync(game, users);
            }
            else if (game.PickOrder == GameResult.CaptainPickOrder.PickOne)
            {
                game = await PickOneAsync(game, users);
            }
            else
            {
                await SimpleEmbedAsync("There was an error picking your game.", Color.DarkRed);
                return;
            }

            //game will be returned null from pickone/picktwo if there was an issue with a pick. The function already replies to just return.
            if (game == null)
            {
                return;
            }
            else
            {
                var remaining = game.GetQueueRemainingPlayers();
                if (remaining.Count() == 1)
                {
                    game.GetTeam().Players.Add(remaining.First());
                }
            }

            if (game.Team1.Players.Count + game.Team2.Players.Count >= game.Queue.Count)
            {
                //Teams have been filled.
                game.GameState = GameResult.State.Undecided;

                var res = Service.GetGameMessage(Context, game, $"Game #{game.GameId} Started",
                        ELOService.GameFlag.gamestate,
                        ELOService.GameFlag.lobby,
                        ELOService.GameFlag.map,
                        ELOService.GameFlag.usermentions,
                        ELOService.GameFlag.time);

                await ReplyAsync(res.Item1, false, res.Item2.Build());

                if (CurrentLobby.GameReadyAnnouncementChannel != 0)
                {
                    var channel = Context.Guild.GetTextChannel(CurrentLobby.GameReadyAnnouncementChannel);
                    if (channel != null)
                    {
                        await channel.SendMessageAsync(res.Item1, false, res.Item2.Build());
                    }
                }

                await MessageUsersAsync(game.Queue.ToArray(), x => MentionUtils.MentionUser(x), res.Item2.Build());
            }
            else
            {
                var res = Service.GetGameMessage(Context, game, "Player(s) picked.",
                        ELOService.GameFlag.gamestate);
                await ReplyAsync(PickResponse ?? "", false, res.Item2.Build());
            }

            Service.SaveGame(game);
        }

        //TODO: if more than x maps are added to the lobby, announce 3 (or so) and allow users to vote on them to pick
        //Would have 1 minute timeout, then either picks the most voted map or randomly chooses from the most voted.
        //Would need to have a way of reducing the amount of repeats as well.
    }
}