using Discord;
using Discord.Commands;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Models;
using System.Collections.Generic;
using System.Linq;

namespace RavenBOT.ELO.Modules.Methods
{
    public partial class ELOService
    {
        public enum GameFlag
        {
            gamestate,
            map,
            time,
            lobby,
            pickmode,
            usermentions,
            submitter
        }
        public (string, EmbedBuilder) GetGameMessage(GameResult game, string title = null, params GameFlag[] flags)
        {
            bool usermentions = flags.Contains(GameFlag.usermentions);

            bool gamestate = flags.Contains(GameFlag.gamestate);
            bool map = flags.Contains(GameFlag.map);
            bool time = flags.Contains(GameFlag.time);
            bool lobby = flags.Contains(GameFlag.lobby);
            bool pickmode = flags.Contains(GameFlag.pickmode);
            bool submitter = flags.Contains(GameFlag.submitter);
            bool remainingPlayers = false;
            bool winningteam = false;
            bool team1 = false;
            bool team2 = false;

            var message = usermentions ? string.Join(" ", game.Queue.Select(x => MentionUtils.MentionUser(x))) : "";

            var embed = new EmbedBuilder
            {
                Color = Color.Blue
            };
            embed.Title = title ?? $"Game #{game.GameId}";
            var desc = "";

            if (time)
            {
                desc += $"**Creation Time:** {game.CreationTime.ToString("dd MMM yyyy")} {game.CreationTime.ToShortTimeString()}\n";
            }

            if (pickmode)
            {
                desc += $"**Pick Mode:** {game.GamePickMode}\n";
            }

            if (lobby)
            {
                desc += $"**Lobby:** {MentionUtils.MentionChannel(game.LobbyId)}\n";
            }

            if (map && game.MapName != null)
            {
                desc += $"**Map:** {game.MapName}\n";
            }

            if (gamestate)
            {
                team1 = true;
                team2 = true;

                switch (game.GameState)
                {
                    case GameResult.State.Canceled:
                        desc += "**State:** Cancelled\n";
                        embed.Color = Color.DarkOrange;
                        break;
                    case GameResult.State.Draw:
                        desc += "**State:** Draw\n";
                        embed.Color = Color.Gold;
                        break;
                    case GameResult.State.Picking:
                        remainingPlayers = true;
                        embed.Color = Color.Magenta;
                        break;
                    case GameResult.State.Decided:
                        winningteam = true;
                        embed.Color = Color.Green;
                        break;
                    case GameResult.State.Undecided:
                        break;
                }
            }

            if (winningteam)
            {
                var teamInfo = game.GetWinningTeam();
                embed.AddField($"Winning Team, Team #{teamInfo.Item1}", teamInfo.Item2.GetTeamInfo());
                if (teamInfo.Item1 == 1)
                {
                    team1 = false;
                }
                else if (teamInfo.Item1 == 2)
                {
                    team2 = false;
                }
            }

            if (team1)
            {
                embed.AddField("Team 1", game.Team1.GetTeamInfo());
            }

            if (team2)
            {
                embed.AddField("Team 2", game.Team2.GetTeamInfo());
            }

            if (remainingPlayers)
            {
                var remaining = game.GetQueueRemainingPlayers();
                if (remaining.Any())
                {
                    embed.AddField("Remaining Players", string.Join(" ", game.GetQueueRemainingPlayers().Select(MentionUtils.MentionUser)));
                }
            }

            embed.Description = desc;



            return (message, embed);
        }

        public EmbedBuilder GetGameEmbed(ManualGameResult game)
        {
            var embed = new EmbedBuilder();

            if (game.GameState == ManualGameResult.ManualGameState.Win)
            {
                embed.Color = Color.Green;
                embed.Title = "Win";
            }
            else if (game.GameState == ManualGameResult.ManualGameState.Lose)
            {
                embed.Color = Color.Red;
                embed.Title = "Lose";
            }
            else if (game.GameState == ManualGameResult.ManualGameState.Draw)
            {
                embed.Color = Color.Gold;
                embed.Title = "Draw";
            }
            else
            {
                embed.Color = Color.Blue;
                embed.Title = "Legacy";
            }


            embed.Description = $"**GameId:** {game.GameId}\n" +
                                $"**Creation Time:** {game.CreationTime.ToString("dd MMM yyyy")} {game.CreationTime.ToShortTimeString()}\n" +
                                $"**Comment:** {game.Comment ?? "N/A"}\n" +
                                $"**Submitted By:** {MentionUtils.MentionUser(game.Submitter)}\n" +
                                string.Join("\n", game.ScoreUpdates.Select(x => $"{MentionUtils.MentionUser(x.Key)} {(x.Value >= 0 ? $"+{x.Value}" : x.Value.ToString())}")).FixLength(1024);

            return embed;
        }

        public EmbedBuilder GetGameEmbed(SocketCommandContext context, GameResult game)
        {
            var embed = new EmbedBuilder();

            embed.AddField("Info",
                $"**GameId:** {game.GameId}\n" +
                $"**Lobby:** {MentionUtils.MentionChannel(game.LobbyId)}\n" +
                $"**Creation Time:** {game.CreationTime.ToString("dd MMM yyyy")} {game.CreationTime.ToShortTimeString()}\n" +
                $"**Pick Mode:** {game.GamePickMode}\n" +
                $"{(game.MapName == null ? "" : $"**Map:** {game.MapName}\n")}" +
                $"{(game.Comment == null ? "" : $"**Comment:** {game.Comment}\n")}");

            if (game.GameState == GameResult.State.Picking)
            {
                embed.AddField("Picking Teams", 
                                $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                                $"Team 2:\n{game.Team2.GetTeamInfo()}\n" +
                                $"Remaining Players:\n{game.GetQueueRemainingPlayersString()}");
                embed.Color = Color.Magenta;
            }
            else if (game.GameState == GameResult.State.Canceled)
            {
                if (Lobby.IsCaptains(game.GamePickMode))
                {
                    var remainingPlayers = game.GetQueueRemainingPlayers();

                    if (remainingPlayers.Any())
                    {
                        embed.AddField("Canceled", 
                            $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                            $"Team 2:\n{game.Team2.GetTeamInfo()}\n" +
                            $"Remaining Players:\n{string.Join("\n", Extensions.GetUserMentionList(remainingPlayers))}");
                    }
                    else
                    {
                        //TODO: Address repeat response below
                        embed.AddField("Canceled", 
                            $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                            $"Team 2:\n{game.Team2.GetTeamInfo()}");
                    }
                }
                else
                {
                    embed.AddField("Canceled", 
                        $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                        $"Team 2:\n{game.Team2.GetTeamInfo()}");
                }

                embed.Color = Color.DarkOrange;
            }
            else if (game.GameState == GameResult.State.Draw)
            {
               embed.AddField("Draw",
                    $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                    $"Team 2:\n{game.Team2.GetTeamInfo()}");
                embed.Color = Color.Gold;
            }
            else if (game.GameState == GameResult.State.Undecided)
            {
                embed.AddField("Undecided",
                    $"Team 1:\n{game.Team1.GetTeamInfo()}\n" +
                    $"Team 2:\n{game.Team2.GetTeamInfo()}");
                embed.Color = Color.Blue;
            }
            else if (game.GameState == GameResult.State.Decided)
            {
                //TODO: Null check getwinning/losing team methods
                var pointsAwarded = new List<string>();
                var winners = game.GetWinningTeam();
                pointsAwarded.Add($"**Team {winners.Item1}**");

                foreach (var player in winners.Item2.Players)
                {
                    var eUser = GetPlayer(context.Guild.Id, player);
                    if (eUser == null) continue;

                    var pointUpdate = game.ScoreUpdates.FirstOrDefault(x => x.Key == player);
                    pointsAwarded.Add($"{eUser.GetDisplayNameSafe()} - +{pointUpdate.Value}");
                }

                var losers = game.GetLosingTeam();
                pointsAwarded.Add($"**Team {losers.Item1}**");
                foreach (var player in losers.Item2.Players)
                {
                    var eUser = GetPlayer(context.Guild.Id, player);
                    if (eUser == null) continue;

                    var pointUpdate = game.ScoreUpdates.FirstOrDefault(x => x.Key == player);
                    pointsAwarded.Add($"{eUser.GetDisplayNameSafe()} - {pointUpdate.Value}");
                }
                embed.AddField($"Winning Team, Team {game.WinningTeam}", game.GetWinningTeam().Item2.GetTeamInfo());
                embed.AddField($"Losing Team", game.GetLosingTeam().Item2.GetTeamInfo());
                embed.AddField("Score Updates", string.Join("\n", pointsAwarded).FixLength(1023));
                embed.Color = Color.Green;
            }


            return embed;
        }
    }
}