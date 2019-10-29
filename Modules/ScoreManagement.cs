using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace RavenBOT.ELO.Modules.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    //TODO: Moderator permission instead of just admin
    [Preconditions.RequirePermission(CompetitionConfig.PermissionLevel.Moderator)]
    public class ScoreManagement : ReactiveBase
    {
        public ELOService Service { get; }

        public ScoreManagement(ELOService service)
        {
            Service = service;
        }

        [Command("ModifyStates", RunMode = RunMode.Async)]
        [Summary("Shows modifier values for score management commands")]
        public async Task ModifyStatesAsync()
        {
            await SimpleEmbedAsync(string.Join("\n", Extensions.EnumNames<Player.ModifyState>()), Color.Blue);
        }

        //TODO: Consider whether it's necessary to have the single user command as multi user already is able to accept only one.
        [Command("Points", RunMode = RunMode.Sync)]
        [Summary("Modifies points for the specified user")]
        public async Task PointsAsync(SocketGuildUser user, Player.ModifyState state, int amount)
        {
            await PointsAsync(state, amount, user);
        }

        [Command("Points", RunMode = RunMode.Sync)]
        [Summary("Modifies points for the specified users.")]
        public async Task PointsAsync(Player.ModifyState state, int amount, params SocketGuildUser[] users)
        {
            var players = Service.GetPlayersSafe(users.Select(x => x.Id), Context.Guild.Id);
            var responseString = "";
            foreach (var player in players)
            {
                var newVal = Player.ModifyValue(state, player.Points, amount);
                responseString += $"{player.GetDisplayNameSafe()}: {player.Points} => {newVal}\n";
                player.Points = newVal;
            }
            Service.SavePlayers(players);
            await SimpleEmbedAsync(responseString, Color.Blue);
        }

        [Command("Wins", RunMode = RunMode.Sync)]
        [Summary("Modifies wins for the specified user.")]
        public async Task WinsAsync(SocketGuildUser user, Player.ModifyState state, int amount)
        {
            await WinsAsync(state, amount, user);
        }

        [Command("Wins", RunMode = RunMode.Sync)]
        [Summary("Modifies wins for the specified users.")]
        public async Task WinsAsync(Player.ModifyState state, int amount, params SocketGuildUser[] users)
        {
            var players = Service.GetPlayersSafe(users.Select(x => x.Id), Context.Guild.Id);
            var responseString = "";
            foreach (var player in players)
            {
                var newVal = Player.ModifyValue(state, player.Wins, amount);
                responseString += $"{player.GetDisplayNameSafe()}: {player.Wins} => {newVal}\n";
                player.Wins = newVal;
            }
            Service.SavePlayers(players);
            await SimpleEmbedAsync(responseString, Color.Blue);
        }

        [Command("Losses", RunMode = RunMode.Sync)]
        [Summary("Modifies losses for the specified user.")]
        public async Task LossesAsync(SocketGuildUser user, Player.ModifyState state, int amount)
        {
            await LossesAsync(state, amount, user);
        }

        [Command("Losses", RunMode = RunMode.Sync)]
        [Summary("Modifies losses for the specified users.")]
        public async Task LossesAsync(Player.ModifyState state, int amount, params SocketGuildUser[] users)
        {
            var players = Service.GetPlayersSafe(users.Select(x => x.Id), Context.Guild.Id);
            var responseString = "";
            foreach (var player in players)
            {
                var newVal = Player.ModifyValue(state, player.Losses, amount);
                responseString += $"{player.GetDisplayNameSafe()}: {player.Losses} => {newVal}\n";
                player.Losses = newVal;
            }
            Service.SavePlayers(players);
            await SimpleEmbedAsync(responseString, Color.Blue);
        }

        [Command("Draws", RunMode = RunMode.Sync)]
        [Summary("Modifies draws for the specified user.")]
        public async Task DrawsAsync(SocketGuildUser user, Player.ModifyState state, int amount)
        {
            await DrawsAsync(state, amount, user);
        }

        [Command("Draws", RunMode = RunMode.Sync)]
        [Summary("Modifies draws for the specified users.")]
        public async Task DrawsAsync(Player.ModifyState state, int amount, params SocketGuildUser[] users)
        {
            var players = Service.GetPlayersSafe(users.Select(x => x.Id), Context.Guild.Id);
            var responseString = "";
            foreach (var player in players)
            {
                var newVal = Player.ModifyValue(state, player.Draws, amount);
                responseString += $"{player.GetDisplayNameSafe()}: {player.Draws} => {newVal}\n";
                player.Draws = newVal;
            }
            Service.SavePlayers(players);
            await SimpleEmbedAsync(responseString, Color.Blue);
        }

        /*
        [Command("PlayerModify", RunMode = RunMode.Sync)]
        public async Task PlayerModifyAsync(SocketGuildUser user, string value, Player.ModifyState state, int amount)
        {
            await PlayersModifyAsync(value, state, amount, user);
        }

        

        
        [Command("PlayersModify", RunMode = RunMode.Sync)]
        public async Task PlayersModifyAsync(string value, Player.ModifyState state, int amount, params SocketGuildUser[] users)
        {
            
            var players = Service.GetPlayersSafe(users.Select(x => x.Id), Context.Guild.Id);
            var responseString = "";
            foreach (var player in players)
            {
                var response = player.UpdateValue(value, state, amount);
                responseString += $"{player.DisplayName}: {response.Item1} => {response.Item2}\n";
            }
            Service.SavePlayers(players);
            await ReplyAsync("", false, responseString.QuickEmbed());
        }
        */
    }
}