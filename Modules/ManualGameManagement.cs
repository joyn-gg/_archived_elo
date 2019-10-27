using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Methods;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Preconditions;
using static RavenBOT.ELO.Modules.Modules.GameManagement;

namespace RavenBOT.ELO.Modules.Modules
{
    [RequireModerator]
    [RavenRequireContext(ContextType.Guild)]
    public class ManualGameManagement : ReactiveBase
    {
        public ELOService Service { get; }

        public ManualGameManagement(ELOService service)
        {
            Service = service;
        }

        [Command("Win", RunMode = RunMode.Sync)]
        [Summary("Adds a win and updates points for the specified users.")]
        public async Task WinAsync(params SocketGuildUser[] users)
        {
            await UpdateTeamScoresAsync(true, users.Select(x => x.Id).ToHashSet());

            var cmds = new CommandService();
        }

        [Command("Lose", RunMode = RunMode.Sync)]
        [Summary("Adds a loss and updates points for the specified users.")]
        public async Task LoseAsync(params SocketGuildUser[] users)
        {
            await UpdateTeamScoresAsync(false, users.Select(x => x.Id).ToHashSet());
        }

        //TODO: Undo manual game
        //TODO: Display manual game info/stats

        public async Task UpdateTeamScoresAsync(bool win, HashSet<ulong> userIds)
        {
            var competition = Service.GetOrCreateCompetition(Context.Guild.Id);
            var updates = new List < (Player, int, Rank, RankChangeState, Rank) > ();

            var embed = new EmbedBuilder
            {
                Title = (win ? "Win" : "Lose") + $" Manual Game: #{competition.ManualGameCounter + 1}",
                Color = win ? Color.Green : Color.Red,
            };
            var sb = new StringBuilder();
            foreach (var userId in userIds)
            {
                var player = Service.GetPlayer(Context.Guild.Id, userId);
                if (player == null) continue;

                //This represents the current user's rank
                var maxRank = competition.MaxRank(player.Points);

                int updateVal;
                RankChangeState state = RankChangeState.None;
                Rank newRank = null;

                if (win)
                {
                    updateVal = maxRank?.WinModifier ?? competition.DefaultWinModifier;
                    player.SetPoints(competition, player.Points + updateVal);
                    player.Wins++;
                    newRank = competition.MaxRank(player.Points);
                    if (newRank != null)
                    {
                        if (maxRank == null)
                        {
                            state = RankChangeState.RankUp;
                        }
                        else if (newRank.RoleId != maxRank.RoleId)
                        {
                            state = RankChangeState.RankUp;
                        }
                    }
                }
                else
                {
                    //Loss modifier is always positive so subtract it
                    updateVal = maxRank?.LossModifier ?? competition.DefaultLossModifier;
                    player.SetPoints(competition, player.Points - updateVal);
                    player.Losses++;
                    //Set the update value to a negative value for returning purposes.
                    updateVal = -updateVal;

                    if (maxRank != null)
                    {
                        if (player.Points < maxRank.Points)
                        {
                            state = RankChangeState.DeRank;
                            newRank = competition.MaxRank(player.Points);
                        }
                    }
                }

                updates.Add((player, updateVal, maxRank, state, newRank));

                //TODO: Rank checking?
                //I forget what this means honestly
                Service.SavePlayer(player);

                //Ignore user updates if they aren't found in the server.
                var gUser = Context.Guild.GetUser(userId);
                if (gUser == null) continue;

                await Service.UpdateUserAsync(competition, player, gUser);

                var rankUpdate = "";
                if (maxRank != null || newRank != null)
                {
                    var oldRoleMention = maxRank == null ? "N/A" : MentionUtils.MentionRole(maxRank.RoleId);
                    var newRoleMention = newRank == null ? "N/A" : MentionUtils.MentionRole(newRank.RoleId);
                    rankUpdate = $" Rank: {oldRoleMention} => {newRoleMention}";
                }

                sb.AppendLine($"{gUser.Mention} Points: {player.Points} {(win ? "Added:" : "Removed:")} {updateVal}{rankUpdate}");
            }

            //Update counter and save new competition config
            competition.ManualGameCounter++;
            Service.SaveCompetition(competition);

            //Create new game info
            var game = new ManualGameResult(competition.ManualGameCounter, Context.Guild.Id);
            game.Submitter = Context.User.Id;
            game.GameState = win ? ManualGameResult.ManualGameState.Win : ManualGameResult.ManualGameState.Lose;
            game.ScoreUpdates = updates.ToDictionary(x => x.Item1.UserId, x => x.Item2);
            embed.Description = sb.ToString();
            Service.SaveManualGame(game);
            await ReplyAsync("", false, embed.Build());
        }
    }
}