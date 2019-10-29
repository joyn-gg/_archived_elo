using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenBOT.ELO.Modules.Modules
{
    public partial class LobbyManagement
    {
        //This needs to be static as module contexts are disposed of between commands.
        public static Dictionary<ulong, Dictionary<ulong, DateTime>> QueueDelays = new Dictionary<ulong, Dictionary<ulong, DateTime>>();


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
                await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - You are still banned from matchmaking for another: {CurrentPlayer.CurrentBan.RemainingTime.GetReadableLength()}", Color.Red);
                return;
            }

            //Not sure if this is actually needed.
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                //Queue will be reset after teams are completely picked.
                await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - Queue is full, wait for teams to be chosen before joining.", Color.DarkBlue);
                return;
            }

            var comp = Service.GetOrCreateCompetition(Context.Guild.Id);
            /*if (comp.BlockMultiQueueing)
            {
                var lobbies = Service.GetLobbies(Context.Guild.Id);
                var lobbyMatches = lobbies.Where(x => x.Queue.Contains(Context.User.Id));
                if (lobbyMatches.Any())
                {
                    var guildChannels = lobbyMatches.Select(x => MentionUtils.MentionChannel(x.ChannelId));
                    await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - MultiQueuing is not enabled in this server.\nPlease leave: {string.Join("\n", guildChannels)}", Color.Red);
                    return;
                }
            }*/

            if (CurrentLobby.MinimumPoints != null)
            {
                if (CurrentPlayer.Points < CurrentLobby.MinimumPoints)
                {
                    await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - You need a minimum of {CurrentLobby.MinimumPoints} points to join this lobby.", Color.Red);
                    return;
                }
            }

            var currentGame = Service.GetCurrentGame(CurrentLobby);
            if (currentGame != null)
            {
                if (currentGame.GameState == GameResult.State.Picking)
                {
                    await SimpleEmbedAndDeleteAsync("Current game is picking teams, wait until this is completed.", Color.DarkBlue);
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - You are already queued.", Color.DarkBlue);
                return;
            }

            if (comp.RequeueDelay.HasValue)
            {
                if (QueueDelays.ContainsKey(Context.Guild.Id))
                {
                    var currentGuild = QueueDelays[Context.Guild.Id];
                    if (currentGuild.ContainsKey(Context.User.Id))
                    {
                        var currentUserLastJoin = currentGuild[Context.User.Id];
                        if (currentUserLastJoin + comp.RequeueDelay.Value > DateTime.UtcNow)
                        {
                            var remaining = currentUserLastJoin + comp.RequeueDelay.Value - DateTime.UtcNow;
                            await SimpleEmbedAndDeleteAsync($"{Context.User.Mention} - You cannot requeue for another {remaining.GetReadableLength()}", Color.Red);
                            return;
                        }
                        else
                        {
                            currentUserLastJoin = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        currentGuild.Add(Context.User.Id, DateTime.UtcNow);
                    }
                }
                else
                {
                    var newDict = new Dictionary<ulong, DateTime>();
                    newDict.Add(Context.User.Id, DateTime.UtcNow);
                    QueueDelays.Add(Context.Guild.Id, newDict);
                }
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
                        await SimpleEmbedAsync($"{CurrentPlayer.GetDisplayNameSafe()} joined the queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**", Color.Green);
                    }
                }
                else
                {
                    await SimpleEmbedAsync($"{CurrentPlayer.GetDisplayNameSafe()} joined the queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**", Color.Green);
                }
            }

            Service.SaveLobby(CurrentLobby);
        }

        private async Task HiddenJoin()
        {
            if (CurrentPlayer.IsBanned)
            {
                await Context.Message.DeleteAsync();
                await SimpleEmbedAndDeleteAsync($"You are still banned from matchmaking for another: {CurrentPlayer.CurrentBan.RemainingTime.GetReadableLength()}", Color.Red, TimeSpan.FromSeconds(5));
                return;
            }

            //Not sure if this is actually needed.
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                //Queue will be reset after teams are completely picked.
                await Context.Message.DeleteAsync();
                await SimpleEmbedAndDeleteAsync("Queue is full, wait for teams to be chosen before joining.", Color.Red, TimeSpan.FromSeconds(5));
                return;
            }

            var comp = Service.GetOrCreateCompetition(Context.Guild.Id);

            if (comp.BlockMultiQueueing)
            {
                var lobbies = Service.GetLobbies(Context.Guild.Id);
                var lobbyMatches = lobbies.Where(x => x.Queue.Contains(Context.User.Id));
                if (lobbyMatches.Any())
                {
                    var guildChannels = lobbyMatches.Select(x => MentionUtils.MentionChannel(x.ChannelId));
                    await Context.Message.DeleteAsync();
                    await SimpleEmbedAndDeleteAsync($"MultiQueuing is not enabled in this server.\nPlease leave: {string.Join("\n", guildChannels)}", Color.Red, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            if (CurrentLobby.MinimumPoints != null)
            {
                if (CurrentPlayer.Points < CurrentLobby.MinimumPoints)
                {
                    await Context.Message.DeleteAsync();
                    await SimpleEmbedAndDeleteAsync($"You need a minimum of {CurrentLobby.MinimumPoints} points to join this lobby.", Color.Red, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            var currentGame = Service.GetCurrentGame(CurrentLobby);
            if (currentGame != null)
            {
                if (currentGame.GameState == Models.GameResult.State.Picking)
                {
                    await Context.Message.DeleteAsync();
                    await SimpleEmbedAndDeleteAsync("Current game is picking teams, wait until this is completed.", Color.DarkBlue, TimeSpan.FromSeconds(5));
                    return;
                }
            }

            if (CurrentLobby.Queue.Contains(Context.User.Id))
            {
                await Context.Message.DeleteAsync();
                await SimpleEmbedAndDeleteAsync("You are already queued.", Color.DarkBlue, TimeSpan.FromSeconds(5));
                return;
            }

            if (comp.RequeueDelay.HasValue)
            {
                if (QueueDelays.ContainsKey(Context.Guild.Id))
                {
                    var currentGuild = QueueDelays[Context.Guild.Id];
                    if (currentGuild.ContainsKey(Context.User.Id))
                    {
                        var currentUserLastJoin = currentGuild[Context.User.Id];
                        if (currentUserLastJoin + comp.RequeueDelay.Value > DateTime.UtcNow)
                        {
                            var remaining = currentUserLastJoin + comp.RequeueDelay.Value - DateTime.UtcNow;
                            await SimpleEmbedAndDeleteAsync($"You cannot requeue for another {remaining.GetReadableLength()}", Color.Red, TimeSpan.FromSeconds(5));
                            return;
                        }
                        else
                        {
                            currentUserLastJoin = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        currentGuild.Add(Context.User.Id, DateTime.UtcNow);
                    }
                }
                else
                {
                    var newDict = new Dictionary<ulong, DateTime>();
                    newDict.Add(Context.User.Id, DateTime.UtcNow);
                    QueueDelays.Add(Context.Guild.Id, newDict);
                }
            }


            CurrentLobby.Queue.Add(Context.User.Id);
            if (CurrentLobby.Queue.Count >= CurrentLobby.PlayersPerTeam * 2)
            {
                await LobbyFullAsync();
            }
            else
            {
                await Context.Message.DeleteAsync();
                await SimpleEmbedAsync($"A player has joined the queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**");
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
                        await SimpleEmbedAsync("Lobby is currently picking teams. You cannot leave a queue while this is happening.", Color.Red);
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
                        await SimpleEmbedAsync($"Removed {CurrentPlayer.GetDisplayNameSafe()} from queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**", Color.DarkBlue);
                    }
                }
                else
                {
                    await SimpleEmbedAsync($"Removed {CurrentPlayer.GetDisplayNameSafe()} from queue. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**", Color.DarkBlue);
                }
            }
            else
            {
                await SimpleEmbedAsync("You are not queued for the next game.", Color.DarkBlue);
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
                        await SimpleEmbedAndDeleteAsync("Lobby is currently picking teams. You cannot leave a queue while this is happening.", Color.Red, TimeSpan.FromSeconds(5));
                        return;
                    }
                }
                CurrentLobby.Queue.Remove(Context.User.Id);
                Service.SaveLobby(CurrentLobby);

                await Context.Message.DeleteAsync();
                await SimpleEmbedAsync($"Removed a player. **[{CurrentLobby.Queue.Count}/{CurrentLobby.PlayersPerTeam * 2}]**");
            }
            else
            {
                await Context.Message.DeleteAsync();
                await SimpleEmbedAndDeleteAsync("You are not queued for the next game.", Color.DarkBlue, TimeSpan.FromSeconds(5));
            }
        }
    }
}