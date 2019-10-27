using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Models;

namespace RavenBOT.ELO.Modules.Modules
{
    public partial class LobbyManagement
    {
        [Command("Join", RunMode = RunMode.Sync)]
        [Alias("JoinLobby", "Join Lobby", "j", "sign", "play", "ready")]
        [Summary("Join the queue in the current lobby.")]
        public async Task JoinLobbyAsync()
        {
            if (!await CheckLobbyAsync() || !await CheckRegisteredAsync())
            {
                return;
            }

            if (CurrentLobby.HideQueue)
            {
                await HiddenJoin();
                return;
            }

            if (CurrentPlayer.IsBanned)
            {
                await ReplyAsync($"You are still banned from matchmaking for another: {CurrentPlayer.CurrentBan.RemainingTime.GetReadableLength()}");
                return;
            }

            //Not sure if this is actually needed.
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                //Queue will be reset after teams are completely picked.
                await ReplyAsync("Queue is full, wait for teams to be chosen before joining.");
                return;
            }

            if (Service.GetOrCreateCompetition(Context.Guild.Id).BlockMultiQueueing)
            {
                var lobbies = Service.GetLobbies(Context.Guild.Id);
                var lobbyMatches = lobbies.Where(x => x.Queue.Contains(Context.User.Id));
                if (lobbyMatches.Any())
                {
                    var guildChannels = lobbyMatches.Select(x => MentionUtils.MentionChannel(x.ChannelId));
                    await ReplyAsync($"MultiQueuing is not enabled in this server.\nPlease leave: {string.Join("\n", guildChannels)}");
                    return;
                }
            }

            if (CurrentLobby.MinimumPoints != null)
            {
                if (CurrentPlayer.Points < CurrentLobby.MinimumPoints)
                {
                    await ReplyAsync($"You need a minimum of {CurrentLobby.MinimumPoints} points to join this lobby.");
                    return;
                }
            }

            var currentGame = Service.GetCurrentGame(CurrentLobby);
            if (currentGame != null)
            {
                if (currentGame.GameState == Models.GameResult.State.Picking)
                {
                    await ReplyAsync("Current game is picking teams, wait until this is completed.");
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                await ReplyAsync("You are already queued.");
                return;
            }

            CurrentLobby.Queue.Add(Context.User.Id);
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                await LobbyFullAsync();
            }
            else
            {
                if (Context.Guild.CurrentUser.GetPermissions(Context.Channel as SocketTextChannel).AddReactions && CurrentLobby.ReactOnJoinLeave)
                {
                    try
                    {
                        await Context.Message.AddReactionAsync(new Emoji("✅"));
                    }
                    catch
                    {
                        await ReplyAsync($"Added {CurrentPlayer.GetDisplayNameSafe()} to queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
                    }
                }
                else
                {
                    await ReplyAsync($"Added {CurrentPlayer.GetDisplayNameSafe()} to queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
                }
            }

            Service.SaveLobby(CurrentLobby);
        }

        private async Task HiddenJoin()
        {
            if (CurrentPlayer.IsBanned)
            {
                await Context.Message.DeleteAsync();
                await ReplyAndDeleteAsync($"You are still banned from matchmaking for another: {CurrentPlayer.CurrentBan.RemainingTime.GetReadableLength()}", null, TimeSpan.FromSeconds(5));
                return;
            }

            //Not sure if this is actually needed.
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                //Queue will be reset after teams are completely picked.
                await Context.Message.DeleteAsync();
                await ReplyAndDeleteAsync("Queue is full, wait for teams to be chosen before joining.");
                return;
            }

            if (Service.GetOrCreateCompetition(Context.Guild.Id).BlockMultiQueueing)
            {
                var lobbies = Service.GetLobbies(Context.Guild.Id);
                var lobbyMatches = lobbies.Where(x => x.Queue.Contains(Context.User.Id));
                if (lobbyMatches.Any())
                {
                    var guildChannels = lobbyMatches.Select(x => MentionUtils.MentionChannel(x.ChannelId));
                    await Context.Message.DeleteAsync();
                    await ReplyAndDeleteAsync($"MultiQueuing is not enabled in this server.\nPlease leave: {string.Join("\n", guildChannels)}", null, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            if (CurrentLobby.MinimumPoints != null)
            {
                if (CurrentPlayer.Points < CurrentLobby.MinimumPoints)
                {
                    await Context.Message.DeleteAsync();
                    await ReplyAndDeleteAsync($"You need a minimum of {CurrentLobby.MinimumPoints} points to join this lobby.", null, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            var currentGame = Service.GetCurrentGame(CurrentLobby);
            if (currentGame != null)
            {
                if (currentGame.GameState == Models.GameResult.State.Picking)
                {
                    await Context.Message.DeleteAsync();
                    await ReplyAndDeleteAsync("Current game is picking teams, wait until this is completed.", null, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                await Context.Message.DeleteAsync();
                await ReplyAndDeleteAsync("You are already queued.", null, TimeSpan.FromSeconds(5));
                return;
            }

            CurrentLobby.Queue.Add(Context.User.Id);
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                await LobbyFullAsync();
            }
            else
            {
                await Context.Message.DeleteAsync();
                await ReplyAsync($"A player has joined the queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
            }

            Service.SaveLobby(CurrentLobby);
        }

        [Command("Leave", RunMode = RunMode.Sync)]
        [Alias("LeaveLobby", "Leave Lobby", "l", "out", "unsign", "remove", "unready")]
        [Summary("Leave the queue in the current lobby.")]
        public async Task LeaveLobbyAsync()
        {
            if (!await CheckLobbyAsync() || !await CheckRegisteredAsync())
            {
                return;
            }

            if (CurrentLobby.HideQueue)
            {
                await HiddenLeave();
                return;
            }

            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                var game = Service.GetCurrentGame(CurrentLobby);
                if (game != null)
                {
                    if (game.GameState == GameResult.State.Picking)
                    {
                        await ReplyAsync("Lobby is currently picking teams. You cannot leave a queue while this is happening.");
                        return;
                    }
                }
                CurrentLobby.Queue.Remove(Context.User.Id);
                Service.SaveLobby(CurrentLobby);

                if (Context.Guild.CurrentUser.GetPermissions(Context.Channel as SocketTextChannel).AddReactions && CurrentLobby.ReactOnJoinLeave)
                {
                    try
                    {
                        await Context.Message.AddReactionAsync(new Emoji("✅"));
                    }
                    catch
                    {
                        await ReplyAsync($"Removed {CurrentPlayer.GetDisplayNameSafe()} from queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
                    }
                }
                else
                {
                    await ReplyAsync($"Removed {CurrentPlayer.GetDisplayNameSafe()} from queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
                }
            }
            else
            {
                await ReplyAsync("You are not queued for the next game.");
            }
        }

        private async Task HiddenLeave()
        {
            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                var game = Service.GetCurrentGame(CurrentLobby);
                if (game != null)
                {
                    if (game.GameState == GameResult.State.Picking)
                    {
                        await Context.Message.DeleteAsync();
                        await ReplyAndDeleteAsync("Lobby is currently picking teams. You cannot leave a queue while this is happening.", null, TimeSpan.FromSeconds(5));
                        return;
                    }
                }
                CurrentLobby.Queue.Remove(Context.User.Id);
                Service.SaveLobby(CurrentLobby);

                await Context.Message.DeleteAsync();
                await ReplyAsync($"Removed a player. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam*2}]**");
            }
            else
            {
                await Context.Message.DeleteAsync();
                await ReplyAndDeleteAsync("You are not queued for the next game.", null, TimeSpan.FromSeconds(5));
            }
        }
    }
}