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
    //TODO: Potential different permissions for creating lobby
    [Preconditions.RequireAdmin]
    public class LobbySetup : ReactiveBase
    {
        public ELOService Service { get; }

        public LobbySetup(ELOService service)
        {
            Service = service;
        }

        [Command("CreateLobby", RunMode = RunMode.Sync)]
        [Alias("Create Lobby")]
        [Summary("Creates a lobby with the specified players per team and specified pick mode")]
        public async Task CreateLobbyAsync(int playersPerTeam = 5, Lobby.PickMode pickMode = Lobby.PickMode.Captains_RandomHighestRanked)
        {
            if (Service.GetLobby(Context.Guild.Id, Context.Channel.Id) != null)
            {
                await SimpleEmbedAndDeleteAsync("This channel is already a lobby. Remove the lobby before trying top create a new one here.", Color.Red);
                return;
            }

            var lobby = Service.CreateLobby(Context.Guild.Id, Context.Channel.Id);
            lobby.PlayersPerTeam = playersPerTeam;
            lobby.TeamPickMode = pickMode;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync("New Lobby has been created\n" +
                $"Players per team: {playersPerTeam}\n" +
                $"Pick Mode: {pickMode}", Color.Green);
        }

        [Command("SetPlayerCount", RunMode = RunMode.Sync)]
        [Alias("Set Player Count", "Set PlayerCount")]
        [Summary("Sets the amount of players per team.")]
        public async Task SetPlayerAsync(int playersPerTeam)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            lobby.PlayersPerTeam = playersPerTeam;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"There can now be up to {playersPerTeam} in each team.", Color.Green);
        }

        [Command("SetPickMode", RunMode = RunMode.Sync)]
        [Alias("Set PickMode", "Set Pick Mode")]
        [Summary("Sets how players will be picked for teams in the current lobby.")]
        public async Task SetPickModeAsync(Lobby.PickMode pickMode)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            lobby.TeamPickMode = pickMode;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Pick mode set.", Color.Green);
        }


        [Command("ReactOnJoinLeave", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will react or send a message when users join or leave a lobby.")]
        public async Task ReactOnJoinLeaveAsync(bool react)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            lobby.ReactOnJoinLeave = react;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"React on join/leave set.", Color.Green);
        }

        [Command("PickModes", RunMode = RunMode.Async)]
        [Summary("Displays all pick modes to use with the SetPickMode command")]
        //[Alias("Pick Modes")] ignore this as it can potentially conflict with the lobby Pick command.
        public async Task DisplayPickModesAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", Extensions.EnumNames<Lobby.PickMode>()), Color.Blue);
        }

        [Command("SetPickOrder", RunMode = RunMode.Sync)]
        [Alias("Set PickOrder", "Set Pick Order")]
        [Summary("Sets how captains pick players.")]
        public async Task SetPickOrderAsync(GameResult.CaptainPickOrder orderMode)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                return;
            }

            lobby.CaptainPickOrder = orderMode;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Captain pick order set.", Color.Green);
        }


        [Command("PickOrders", RunMode = RunMode.Async)]
        [Summary("Shows pickorder settings for the SetPickOrder command")]
        public async Task DisplayPickOrdersAsync()
        {
            var res = "`PickOne` - Captains each alternate picking one player until there are none remaining\n" +
                    "`PickTwo` - 1-2-2-1-1... Pick order. Captain 1 gets first pick, then Captain 2 picks 2 players,\n" +
                    "then Captain 1 picks 2 players and then alternate picking 1 player until teams are filled\n" +
                    "This is often used to reduce any advantage given for picking the first player.";
            await SimpleEmbedAsync(res, Color.Blue);
        }

        [Command("SetGameReadyAnnouncementChannel", RunMode = RunMode.Sync)]
        [Alias("GameReadyAnnouncementsChannel", "GameReadyAnnouncements", "ReadyAnnouncements", "SetReadyAnnouncements")]
        [Summary("Set a channel to send game ready announcements for the current lobby to.")]
        public async Task GameReadyAnnouncementChannel(SocketTextChannel destinationChannel = null)
        {
            if (destinationChannel == null)
            {
                await SimpleEmbedAsync("You need to specify a channel for the announcements to be sent to.", Color.DarkBlue);
                return;
            }

            if (destinationChannel.Id == Context.Channel.Id)
            {
                await SimpleEmbedAsync("You cannot send announcements to the current channel.", Color.Red);
                return;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            lobby.GameReadyAnnouncementChannel = destinationChannel.Id;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Game ready announcements for the current lobby will be sent to {destinationChannel.Mention}", Color.Green);
        }

        [Command("SetGameResultAnnouncementChannel", RunMode = RunMode.Sync)]
        [Alias("SetGameResultAnnouncements", "GameResultAnnouncements")]
        [Summary("Set a channel to send game result announcements for the current lobby to.")]
        public async Task GameResultAnnouncementChannel(SocketTextChannel destinationChannel = null)
        {
            if (destinationChannel == null)
            {
                await SimpleEmbedAsync("You need to specify a channel for the announcements to be sent to.", Color.DarkBlue);
                return;
            }

            if (destinationChannel.Id == Context.Channel.Id)
            {
                await SimpleEmbedAsync("You cannot send announcements to the current channel.", Color.Red);
                return;
            }

            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            lobby.GameResultAnnouncementChannel = destinationChannel.Id;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Game results for the current lobby will be sent to {destinationChannel.Mention}", Color.Green);
        }

        [Command("SetMinimumPoints", RunMode = RunMode.Sync)]
        [Summary("Set the minimum amount of points required to queue for the current lobby.")]
        public async Task SetMinimumPointsAsync(int points)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            lobby.MinimumPoints = points;

            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Minimum points required to join this lobby is now set to {points}.", Color.Green);
        }

        [Command("ResetMinimumPoints", RunMode = RunMode.Sync)]
        [Summary("Resets minimum points to join the lobby, allowing all players to join.")]
        public async Task ResetMinPointsAsync()
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            lobby.MinimumPoints = null;

            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"Minimum points is now disabled for this lobby.", Color.Green);
        }

        [Command("MapMode", RunMode = RunMode.Sync)]
        [Summary("Sets the map selection mode for the lobby.")]
        public async Task MapModeAsync(MapSelector.MapMode mode)
        {
            if (mode == MapSelector.MapMode.Vote)
            {
                await SimpleEmbedAsync("That mode is not available currently.", Color.DarkBlue);
                return;

                //Three options:
                //Last known map (if available)
                //Two random maps
                //Players vote on the maps and most voted is announced?
            }

            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                lobby.MapSelector = new MapSelector();
            }



            lobby.MapSelector.Mode = mode;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync("Mode set.", Color.Green);
        }

        [Command("MapMode", RunMode = RunMode.Async)]
        [Summary("Shows the current map selection mode for the lobby.")]
        public async Task MapModeAsync()
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                lobby.MapSelector = new MapSelector();
            }

            await SimpleEmbedAsync($"Current Map Mode: {lobby.MapSelector?.Mode}", Color.Blue);
            return;
        }

        [Command("MapModes", RunMode = RunMode.Async)]
        [Summary("Shows all available map selection modes.")]
        public async Task MapModes()
        {
            await SimpleEmbedAsync(string.Join(", ", Extensions.EnumNames<MapSelector.MapMode>()), Color.Blue);
        }

        [Command("ClearMaps", RunMode = RunMode.Sync)]
        [Summary("Removes all maps set for the current lobby.")]
        public async Task MapClear()
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                await SimpleEmbedAsync("There are no maps to clear.", Color.DarkBlue);
                return;
            }

            lobby.MapSelector.Maps.Clear();
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync("Map added.", Color.Green);
        }


        [Command("AddMaps", RunMode = RunMode.Sync)]
        [Alias("Add Maps", "Addmap", "Add map")]
        [Summary("Adds multiple maps to the map list.")]
        [Remarks("Separate the names using commas.")]
        public async Task AddMapsAsync([Remainder]string commaSeparatedMapNames)
        {
            var mapNames = commaSeparatedMapNames.Split(',');
            if (!mapNames.Any()) return;

            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                lobby.MapSelector = new MapSelector();
            }

            foreach (var map in mapNames)
            {
                lobby.MapSelector.Maps.Add(map);
            }
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync("Map(s) added.", Color.Green);
        }

        [Command("DelMap", RunMode = RunMode.Sync)]
        [Summary("Removes the specified map from the map list.")]
        public async Task RemoveMapAsync([Remainder]string mapName)
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                await SimpleEmbedAsync("There are no maps to remove.", Color.DarkBlue);
                return;
            }

            if (lobby.MapSelector.Maps.Remove(mapName))
            {
                Service.SaveLobby(lobby);
                await SimpleEmbedAsync("Map removed.", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("There was no map matching that name found.", Color.DarkBlue);
            }
        }

        [Command("ToggleDms", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will dm players when a game is ready.")]
        public async Task ToggleDmsAsync()
        {
            if (!Context.Channel.IsLobby(Service, out var lobby)) return;

            lobby.DmUsersOnGameReady = !lobby.DmUsersOnGameReady;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync($"DM when games are ready: {lobby.DmUsersOnGameReady}", Color.Blue);
        }

        [Command("SetDescription", RunMode = RunMode.Sync)]
        [Summary("Sets the lobbys description.")]
        public async Task SetDescriptionAsync([Remainder]string description)
        {
            if (!Context.Channel.IsLobby(Service, out var lobby)) return;

            lobby.Description = description;
            Service.SaveLobby(lobby);
            await SimpleEmbedAsync("Lobby description set.", Color.Blue);
        }

        [Command("DeleteLobby", RunMode = RunMode.Sync)]
        [Summary("Deletes the current lobby and all game played in it.")]
        public async Task DeleteLobbyAsync()
        {
            if (Context.Channel.IsLobby(Service, out var lobby))
            {
                Service.DeleteLobby(Context.Guild.Id, Context.Channel.Id);
                await SimpleEmbedAsync("Lobby and all games played in it have been removed.", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
            }
        }

        [Command("HideQueue", RunMode = RunMode.Sync)]
        [Summary("Sets whether players in queue are shown.")]
        [RavenRequireBotPermission(GuildPermission.ManageMessages)]
        public async Task AllowNegativeAsync(bool? hideQueue = null)
        {
            if (Context.Channel.IsLobby(Service, out var lobby))
            {
                if (hideQueue == null)
                {
                    await SimpleEmbedAsync($"Current Hide Queue Setting: {lobby.HideQueue}", Color.Blue);
                    return;
                }
                lobby.HideQueue = hideQueue.Value;
                Service.SaveLobby(lobby);
                await SimpleEmbedAsync($"Hide Queue: {hideQueue.Value}", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
            }
        }

        [Command("MentionUsersInReadyAnnouncement", RunMode = RunMode.Sync)]
        [Summary("Sets whether players are pinged in the ready announcement.")]
        [RavenRequireBotPermission(GuildPermission.ManageMessages)]
        public async Task MentionUsersReadyAsync(bool? mentionUsers = null)
        {
            if (Context.Channel.IsLobby(Service, out var lobby))
            {
                if (mentionUsers == null)
                {
                    await SimpleEmbedAsync($"Current Mention Users Setting: {lobby.MentionUsersInReadyAnnouncement}", Color.Blue);
                    return;
                }
                lobby.MentionUsersInReadyAnnouncement = mentionUsers.Value;
                Service.SaveLobby(lobby);
                await SimpleEmbedAsync($"Mention Users: {mentionUsers.Value}", Color.Green);
            }
            else
            {
                await SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
            }
        }
    }
}