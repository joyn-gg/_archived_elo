using System;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Entities;
using ELO.Models;
using ELO.Services;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ELO.Extensions;

namespace ELO.Modules
{
    [RequireContext(ContextType.Guild)]
    [Preconditions.RequirePermission(PermissionLevel.ELOAdmin)]
    public class LobbySetup : ModuleBase<ShardedCommandContext>
    {
        public PremiumService Premium { get; }

        public DiscordShardedClient Client { get; }

        public LobbySetup(PremiumService premium, DiscordShardedClient client)
        {
            Premium = premium;
            Client = client;
        }

        [Command("CreateLobby", RunMode = RunMode.Sync)]
        [Alias("Create Lobby")]
        [Summary("Creates a lobby with the specified players per team and specified pick mode")]
        public virtual async Task CreateLobbyAsync(int playersPerTeam = 5, PickMode pickMode = PickMode.Captains_RandomHighestRanked)
        {
            using (var db = new Database())
            {
                //Required for foreign key check
                var competition = db.GetOrCreateCompetition(Context.Guild.Id);
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby != null)
                {
                    await Context.SimpleEmbedAndDeleteAsync("This channel is already a lobby.\n" +
                                                            "Remove the existing lobby with command `DeleteLobby` before trying to create a new one in this channel.", Color.Red);
                    return;
                }
                var allLobbies = db.Lobbies.AsQueryable().Where(x => x.GuildId == Context.Guild.Id).ToArray();
                if (allLobbies.Length >= Premium.PremiumConfig.LobbyLimit)
                {
                    if (!Premium.IsPremium(Context.Guild.Id))
                    {
                        await Context.SimpleEmbedAsync($"This server already has {Premium.PremiumConfig.LobbyLimit} lobbies created. " +
                            $"In order to create more you must become an ELO premium subscriber at {Premium.PremiumConfig.AltLink} join the server " +
                            $"{Premium.PremiumConfig.ServerInvite} to receive your role and then run the `ClaimPremium` command in your server.");
                        return;
                    }
                }

                lobby = new Lobby
                {
                    ChannelId = Context.Channel.Id,
                    GuildId = Context.Guild.Id,
                    PlayersPerTeam = playersPerTeam,
                    TeamPickMode = pickMode
                };
                db.Lobbies.Add(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync("New Lobby has been created\n" +
                    $"Players per team: {playersPerTeam}\n" +
                    $"Pick Mode: {pickMode}\n" +
                    $"NOTE: You can play multiple games per lobby. After a game has been created simply join the queue again and another game can be played.", Color.Green);
            }
        }

        [Command("SetPlayerCount", RunMode = RunMode.Sync)]
        [Alias("Set Player Count", "Set PlayerCount", "PlayersPerTeam", "SetPlayersPerTeam", "TeamSize", "SetTeamSize")]
        [Summary("Sets the amount of players per team.")]
        public virtual async Task SetPlayerAsync(int playersPerTeam)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.PlayersPerTeam = playersPerTeam;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"There can now be up to {playersPerTeam} in each team.", Color.Green);
            }
        }

        [Command("SetPickMode", RunMode = RunMode.Sync)]
        [Alias("PickMode", "Set PickMode", "Set Pick Mode")]
        [Summary("Sets how players will be picked for teams in the current lobby.")]
        public virtual async Task SetPickModeAsync(PickMode pickMode)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.TeamPickMode = pickMode;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Pick mode set.", Color.Green);
            }
        }

        /*[Command("ReactOnJoinLeave", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will react or send a message when users join or leave a lobby.")]
        public virtual async Task ReactOnJoinLeaveAsync(bool react)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.ReactOnJoinLeave = react;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"React on join/leave set.", Color.Green);
            }
        }*/

        [Command("PickModes", RunMode = RunMode.Async)]
        [Alias("PickMode")]
        [Summary("Displays all pick modes to use with the SetPickMode command")]
        //[Alias("Pick Modes")] ignore this as it can potentially conflict with the lobby Pick command.
        public virtual async Task DisplayPickModesAsync()
        {
            await Context.SimpleEmbedAsync(string.Join("\n", Extensions.Extensions.EnumNames<PickMode>()), Color.Blue);
        }

        [Command("SetPickOrder", RunMode = RunMode.Sync)]
        [Alias("PickOrder", "Set PickOrder", "Set Pick Order")]
        [Summary("Sets how captains pick players.")]
        public virtual async Task SetPickOrderAsync(CaptainPickOrder orderMode)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.CaptainPickOrder = orderMode;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Captain pick order set.", Color.Green);
            }
        }

        [Command("PickOrders", RunMode = RunMode.Async)]
        [Alias("PickOrder")]
        [Summary("Shows pickorder settings for the SetPickOrder command")]
        public virtual async Task DisplayPickOrdersAsync()
        {
            var res = "`PickOne` - Captains each alternate picking one player until there are none remaining\n" +
                    "`PickTwo` - 1-2-2-1-1... Pick order. Captain 1 gets first pick, then Captain 2 picks 2 players,\n" +
                    "then Captain 1 picks 2 players and then alternate picking 1 player until teams are filled\n" +
                    "This is often used to reduce any advantage given for picking the first player.";
            await Context.SimpleEmbedAsync(res, Color.Blue);
        }

        [Command("SetReadyChannel", RunMode = RunMode.Sync)]
        [Alias("SetGameReadyAnnouncementChannel", "GameReadyAnnouncementsChannel", "GameReadyAnnouncements", "ReadyAnnouncements", "SetReadyAnnouncements")]
        [Summary("Set a channel to send game ready announcements for the current lobby to.")]
        public virtual async Task GameReadyAnnouncementChannel(SocketTextChannel destinationChannel = null)
        {
            await using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                
                if (lobby == null)
                {
                    await Context.SimpleEmbedAndDeleteAsync("This command must be run from within a lobby channel!\n\n" +
                        "Go to your __lobby channel__ and mention the channel you want the game ready announcements sent to.\n\n" +
                        "**Example:** For #lobby ready announcements to be sent to #games\n" +
                        "Go to #lobby and type `SetReadyChannel #games`", Color.Red);
                    return;
                }

                if (destinationChannel == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Game Announcement Channel:** {(lobby.GameReadyAnnouncementChannel == null ? "N/A" : MentionUtils.MentionChannel(lobby.GameReadyAnnouncementChannel.Value))}\n" +
                                                   "Use `SetReadyChannel #channel` to change where announcements are sent to.", Color.DarkBlue);
                    return;
                }

                if (destinationChannel.Id == Context.Channel.Id)
                {
                    await Context.SimpleEmbedAndDeleteAsync("**You cannot send ready announcements to the current channel!**\n\n" + 
                        "Use this command in your __lobby channel__ and mention the channel you want the game ready announcements sent to.\n\n" +
                        "**Example:** If you want #lobby ready announcements sent to #games\n" +
                        "Go to #lobby and type `SetReadyChannel #games`", Color.Red);
                    return;
                }

                lobby.GameReadyAnnouncementChannel = destinationChannel.Id;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Game ready announcements for the current lobby will be sent to {destinationChannel.Mention}", Color.Green);
            }
        }

        [Command("SetResultChannel", RunMode = RunMode.Sync)]
        [Alias("SetGameResultAnnouncementChannel", "SetGameResultAnnouncements", "GameResultAnnouncements")]
        [Summary("Set a channel to send game result announcements for the current lobby to.")]
        public virtual async Task GameResultAnnouncementChannel(SocketTextChannel destinationChannel = null)
        {
            await using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);

                if (lobby == null)
                {
                    await Context.SimpleEmbedAndDeleteAsync("**This command must be run from within a lobby channel!\n\n**" +
                        "Go to your __lobby channel__ and mention the channel you want the game result announcements sent to.\n\n" +
                        "**Example:** For #lobby result announcements to be sent to #results\n" +
                        "Go to #lobby and write `SetResultChannel #results`", Color.Red);
                    return;
                }

                if (destinationChannel == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Result Announcement Channel:** {(lobby.GameResultAnnouncementChannel == null ? "N/A" : MentionUtils.MentionChannel(lobby.GameResultAnnouncementChannel.Value))}\n" + 
                                                   "Use `SetResultChannel #channel` to change where announcements are sent to.", Color.DarkBlue);
                    return;
                }

                if (destinationChannel.Id == Context.Channel.Id)
                {
                    await Context.SimpleEmbedAndDeleteAsync("**You cannot send result announcements to the current channel!**" +
                        "Use this command in your __lobby channel__ and mention the channel you want the game ready announcements sent to.\n\n" +
                        "**Example:** If you want #lobby results announcements sent to #results\n" +
                        "Go to #lobby and write `SetResultChannel #results`", Color.Red);
                    return;
                }

                lobby.GameResultAnnouncementChannel = destinationChannel.Id;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Game result announcements for the current lobby will be sent to {destinationChannel.Mention}", Color.Green);
            }
        }

        [Command("SetMinimumPoints", RunMode = RunMode.Sync)]
        [Alias("MinimumPointsToQueue", "SetMinimumPointsToQueue")]
        [Summary("Set the minimum amount of points required to queue for the current lobby.")]
        public virtual async Task SetMinimumPointsAsync(int? points = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                if (points == null)
                {
                    await Context.SimpleEmbedAsync(
                        $"**Current:** {(lobby.MinimumPoints == null ? "N/A" : $"{lobby.MinimumPoints.Value}")}\n" +
                        $"Use `SetMinimumPoints <points>` or `ResetMinimumPoints` to change this setting.", Color.Blue);
                    return;
                }
                lobby.MinimumPoints = points;

                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Minimum points required to join this lobby is now set to `{points}`.", Color.Green);
            }
        }

        [Command("ResetMinimumPoints", RunMode = RunMode.Sync)]
        [Summary("Resets minimum points to join the lobby, allowing all players to join.")]
        public virtual async Task ResetMinPointsAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                lobby.MinimumPoints = null;

                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Minimum points is now disabled for this lobby.", Color.Green);
            }
        }

        /*
        [Command("MapMode", RunMode = RunMode.Sync)]
        [Summary("Sets the map selection mode for the lobby.")]
        public virtual async Task MapModeAsync(MapMode mode)
        {
            if (mode == MapSelector.MapMode.Vote)
            {
                await Context.SimpleEmbedAsync("That mode is not available currently.", Color.DarkBlue);
                return;

                //Three options:
                //Last known map (if available)
                //Two random maps
                //Players vote on the maps and most voted is announced?
            }

            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                lobby.MapSelector = new MapSelector();
            }

            lobby.MapSelector.Mode = mode;
            Service.SaveLobby(lobby);
            await Context.SimpleEmbedAsync("Mode set.", Color.Green);
        }

        [Command("MapMode", RunMode = RunMode.Async)]
        [Summary("Shows the current map selection mode for the lobby.")]
        public virtual async Task MapModeAsync()
        {
            var lobby = Service.GetLobby(Context.Guild.Id, Context.Channel.Id);
            if (lobby == null)
            {
                await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                return;
            }

            if (lobby.MapSelector == null)
            {
                lobby.MapSelector = new MapSelector();
            }

            await Context.SimpleEmbedAsync($"Current Map Mode: {lobby.MapSelector?.Mode}", Color.Blue);
            return;
        }

        [Command("MapModes", RunMode = RunMode.Async)]
        [Summary("Shows all available map selection modes.")]
        public virtual async Task MapModes()
        {
            await Context.SimpleEmbedAsync(string.Join(", ", Extensions.EnumNames<MapSelector.MapMode>()), Color.Blue);
        }*/

        [Command("ClearMaps", RunMode = RunMode.Sync)]
        [Summary("Removes all maps set for the current lobby.")]
        public virtual async Task MapClear()
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var maps = db.Maps.AsQueryable().Where(x => x.ChannelId == Context.Channel.Id);
                db.RemoveRange(maps);
                db.SaveChanges();
                await Context.SimpleEmbedAsync("Maps cleared.", Color.Green);
            }
        }

        [Command("AddMaps", RunMode = RunMode.Sync)]
        [Alias("Add Maps", "Addmap", "Add map")]
        [Summary("Adds multiple maps to the map list.")]
        [Remarks("Separate the names using commas.")]
        public virtual async Task AddMapsAsync([Remainder]string commaSeparatedMapNames)
        {
            var mapNames = commaSeparatedMapNames.Split(',');
            if (!mapNames.Any()) return;

            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var currentMaps = db.Maps.AsQueryable().Where(x => x.ChannelId == Context.Channel.Id).ToArray();
                var mapViolations = new HashSet<string>(mapNames.Length);
                var addedMaps = new HashSet<string>(mapNames.Length);

                foreach (var map in mapNames)
                {
                    if (currentMaps.Any(x => x.MapName.Equals(map, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        mapViolations.Add(map);
                        continue;
                    }

                    if (!addedMaps.Any(x => x.Equals(map, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        db.Maps.Add(new Map
                        {
                            ChannelId = Context.Channel.Id,
                            MapName = map
                        });
                        addedMaps.Add(map);
                    }
                }
                db.SaveChanges();

                if (mapViolations.Count > 0)
                {
                    await Context.SimpleEmbedAsync($"Added Maps: {string.Join(", ", addedMaps)}\n" +
                                            $"Failed to Add: {string.Join(", ", mapViolations)}\n" +
                                            $"Duplicate maps found and ignored.");
                }
                else if (addedMaps.Count == 1)
                {
                    await Context.SimpleEmbedAsync($"Map added.\n{string.Join("", addedMaps)}", Color.Green);
                }
                else
                {
                    await Context.SimpleEmbedAsync($"Maps added.\n{string.Join(", ", addedMaps)}", Color.Green);
                }
            }
        }

        [Command("DelMap", RunMode = RunMode.Sync)]
        [Summary("Removes the specified map from the map list.")]
        public virtual async Task RemoveMapAsync([Remainder]string mapName)
        {
            using (var db = new Database())
            {
                var lobby = db.GetLobby(Context.Channel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Current channel is not a lobby.", Color.Red);
                    return;
                }

                var maps = db.Maps.AsQueryable().Where(x => x.ChannelId == Context.Channel.Id).ToArray();
                if (maps.Length == 0)
                {
                    await Context.SimpleEmbedAsync("There are no maps to remove.", Color.DarkBlue);
                    return;
                }

                var match = maps.SingleOrDefault(x => x.MapName.Equals(mapName, System.StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    db.Maps.Remove(match);
                    db.SaveChanges();
                    await Context.SimpleEmbedAsync("Map removed.", Color.Green);
                }
                else
                {
                    await Context.SimpleEmbedAsync("There was no map matching that name found.", Color.DarkBlue);
                }
            }
        }

        [Command("ToggleDms", RunMode = RunMode.Sync)]
        [Summary("Sets whether the bot will dm players when a game is ready.")]
        public virtual async Task ToggleDmsAsync()
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                lobby.DmUsersOnGameReady = !lobby.DmUsersOnGameReady;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"DM when games are ready: {lobby.DmUsersOnGameReady}", Color.Blue);
            }
        }

        [Command("SetDescription", RunMode = RunMode.Sync)]
        [Summary("Sets the lobbys description.")]
        public virtual async Task SetDescriptionAsync([Remainder]string description)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                lobby.Description = description;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync("Lobby description set.", Color.Green);
            }
        }

        // Users might end up removing the current lobby instead of the one they are trying to specify. Most users seem to manually delete the channel instead of removing it like this anyways, and asking for PurgeLobbies command later.
        // And since you need to use CreateLobby in the channel you want to make a lobby I don't see the need for this for the public bot.

        /*
        [Command("DeleteLobby", RunMode = RunMode.Sync)]
        [Alias("RemoveLobby")]
        [Summary("Deletes the given lobby and all games played in it.")]
        public virtual async Task DeleteLobbyAsync(SocketTextChannel channel, string confirmCode = null)
        {
            await DeleteLobbyAsync(channel.Id, confirmCode);
        }

        [Command("DeleteLobby", RunMode = RunMode.Sync)]
        [Alias("RemoveLobby")]
        [Summary("Deletes the current lobby and all games played in it.")]
        public virtual async Task DeleteLobbyAsync(string confirmCode = null)
        {
            await DeleteLobbyAsync(Context.Channel.Id, confirmCode);
        }
        */

        [Command("DeleteLobby", RunMode = RunMode.Sync)]
        [Alias("RemoveLobby")]
        [Summary("Deletes the current lobby and all games played in it.")]
        public virtual async Task DeleteLobbyAsync(/*[Summary("(optional)@Channel, channelId")]ulong lobbyId,*/string confirmCode = null)
        {
            string confirmKey = "urh2riz";

            await using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAndDeleteAsync("Current channel is not a lobby.\n\n" +
                                                            "Please use this command in the lobby you are trying to remove.\n" +
                                                            "Alternatively you can use `PurgeLobbies` to remove unused lobbies that no longer have a channel.", Color.Red);
                    return;
                }

                if (confirmCode == null || !confirmCode.Equals(confirmKey, StringComparison.OrdinalIgnoreCase))
                {
                    await Context.SimpleEmbedAndDeleteAsync($"**Are you sure you want to delete the lobby #{Client.GetChannel(lobby.ChannelId)}?**\n\n" +
                        $"This will **completely** remove the lobby, the games played and all leaderboards associated with it from the database.\n" +
                        $"**This can __NOT__ be undone**\n" +
                        $"\nPlease __re-run this command__ with confirmation code `{confirmKey}`\n" +
                        $"`DeleteLobby {confirmKey}`", Color.DarkOrange);
                    return;
                }

                db.Lobbies.Remove(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Lobby and all games played in it has been deleted.", Color.Green);
            }
        }

        [Command("PurgeLobbies", RunMode = RunMode.Sync)]
        [Alias("PurgeLobbys", "RemoveUnusedLobbies", "DeleteUnusedLobbies")]
        [Summary("Deletes all lobbies that no longer have a channel associated with it.")]
        public virtual async Task PurgeLobbiesAsync()
        {
            using (var db = new Database())
            {
                //Find lobbies for the server where there is NO matching channel found
                var lobbies = db.Lobbies.AsQueryable().Where(x => x.GuildId == Context.Guild.Id).ToArray().AsQueryable().Where(x => !Context.Guild.Channels.Any(c => c.Id == x.ChannelId)).ToArray();
                if (lobbies.Length == 0)
                {
                    await Context.SimpleEmbedAsync("There are no lobbies to remove.", Color.Red);
                    return;
                }

                db.Lobbies.RemoveRange(lobbies);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"{lobbies.Length} unused {(lobbies.Length > 1 ? "lobbies" : "lobby")} removed.", Color.Green);
            }
        }

        [Command("HideQueue", RunMode = RunMode.Sync)]
        [Summary("Sets whether players in queue are shown.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public virtual async Task AllowNegativeAsync(bool? hideQueue = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                if (hideQueue == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Hide Queue Setting:** {lobby.HideQueue}\n" +
                                           $"Use `HideQueue True` or `HideQueue False` to change this setting.", Color.Blue);
                    return;
                }
                lobby.HideQueue = hideQueue.Value;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"**Hide Queue:** {hideQueue.Value}", Color.Green);
            }
        }

        [Command("MentionUsersGameReady", RunMode = RunMode.Sync)]
        [Alias("MentionUsersInReadyAnnouncement", "MentionUsersInGameAnnouncement", "MentionUsersInGameAnnouncement")]
        [Summary("Sets whether players are pinged in the ready announcement.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public virtual async Task MentionUsersReadyAsync(bool? mentionUsers = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }
                if (mentionUsers == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Mention Users Setting:** {lobby.MentionUsersInReadyAnnouncement}\n" +
                                           $"Use `MentionUsersGameReady True` or `MentionUsersGameReady False` to change this setting.", Color.Blue);
                    return;
                }
                lobby.MentionUsersInReadyAnnouncement = mentionUsers.Value;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"**Mention Users In Game Ready Announcements**: {mentionUsers.Value}", Color.Green);
            }
        }

        [Command("SetMultiplier", RunMode = RunMode.Sync)]
        [Alias("SetLobbyMultiplier", "LobbyMultiplier")]
        [Summary("Sets or displays the lobby score multiplier.")]
        public virtual async Task SetLobbyMultiplier(double? multiplier = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                /*if (multiplier == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Lobby Multiplier:** {lobby.MentionUsersInReadyAnnouncement}\n" +
                                                   $"Use `SetMultiplier X` to change this setting.", Color.Blue);
                    return;
                }*/

                lobby.LobbyMultiplier = multiplier ?? 1;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Lobby Multiplier has been {(multiplier != null ? "set" : "reset")} to `{multiplier ?? 1}` {(multiplier == null ? "(default)" : "")}", Color.Green);
            }
        }

        [Command("LobbyMultiplierLoss", RunMode = RunMode.Sync)]
        [Summary("Sets whether the lobby multiplier also affects the amount of points removed from users.")]
        public virtual async Task SetLossToggleMultiplier(bool? value = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                if (value == null)
                {
                    await Context.SimpleEmbedAsync($"**Current Multiply Loss Value Setting:** {lobby.MultiplyLossValue}\n" +
                                                   $"Use `LobbyMultiplierLoss True` or `LobbyMultiplierLoss False` to change this setting.", Color.Blue);
                    return;
                }

                lobby.MultiplyLossValue = value.Value;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Multiplier will affect the loss amount: {lobby.MultiplyLossValue}", Color.Green);
            }
        }

        [Command("SetHighLimitMultiplier", RunMode = RunMode.Sync)]
        [Summary("Sets a multiplier for users who have a higher amount of points than what is defined in the SetHighLimit command.")]
        public virtual async Task SetHighLimitMultiplier(double multiplier = 0.5)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.ReductionPercent = multiplier;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"High Limit multiplier set, when users exceed `{(lobby.HighLimit.HasValue ? lobby.HighLimit.Value.ToString() : "N/A (Configure using the SetHighLimit command)")}` points, " +
                    $"their points received will be multiplied by `{multiplier}`.\n" +
                    $"It is recommended to set this to a value such as `0.5` for lower ranked lobbies " +
                    $"so higher ranked players are not rewarded as much for winning in lower ranked lobbies.", Color.Green);
            }
        }

        [Command("SetHighLimit", RunMode = RunMode.Sync)]
        [Summary("Sets max user points before point reduction multiplier is applied.")]
        public virtual async Task SetHighLimit(int? highLimit = null)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.HighLimit = highLimit;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                //await Context.SimpleEmbedAsync($"Max user points before point reduction set.", Color.Green);
                await Context.SimpleEmbedAsync($"Max user points before point reduction has been {(highLimit != null ? $"set to `{highLimit}`" : "`Disabled` (default)")}", Color.Green);
            }
        }

        [Command("HostModes")]
        [Summary("Displays a list of host selection modes available")]
        public virtual async Task ShowHostSelectionModes()
        {
            await Context.SimpleEmbedAsync(string.Join("\n", Extensions.Extensions.EnumNames<HostSelection>()));
        }

        [Command("SetHostMode", RunMode = RunMode.Sync)]
        [Alias("HostMode")]
        [Summary("Sets if and how the host is selected.")]
        public virtual async Task SetHostModeAsync(HostSelection hostMode)
        {
            using (var db = new Database())
            {
                var lobby = db.Lobbies.FirstOrDefault(x => x.ChannelId == Context.Channel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Channel is not a lobby.", Color.Red);
                    return;
                }

                lobby.HostSelectionMode = hostMode;
                db.Lobbies.Update(lobby);
                db.SaveChanges();
                await Context.SimpleEmbedAsync($"Host Selection Mode set to: {lobby.HostSelectionMode}", Color.Green);
            }
        }
    }
}