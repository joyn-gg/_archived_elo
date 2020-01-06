using Discord;
using ELO.Entities;
using ELO.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ELO.Services
{
    public class GameService
    {
        public string GetTeamInfo(TeamCaptain captain, IEnumerable<ulong> players)
        {
            var resStr = "";
            if (captain != null)
            {
                resStr += $"Captain: {MentionUtils.MentionUser(captain.UserId)}\n";
                players = players.Where(x => x != captain.UserId).ToArray();
                if (players.Any())
                {
                    resStr += $"Players: {string.Join("\n", RavenBOT.Common.Extensions.GetUserMentionList(players))}";
                }

            }
            else
            {
                resStr += string.Join("\n", RavenBOT.Common.Extensions.GetUserMentionList(players));
            }

            if (string.IsNullOrWhiteSpace(resStr))
            {
                resStr = "UwU";
            }
            return resStr;
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

            using (var db = new Database())
            {
                var queue = db.GetQueuedPlayers(game.GuildId, game.LobbyId).Select(x => x.UserId);
                var team1p = db.GetTeamFull(game, 1);
                var team2p = db.GetTeamFull(game, 2);

                var message = usermentions ? string.Join(" ", queue.Union(team1p).Union(team2p).Distinct().Select(x => MentionUtils.MentionUser(x))) : "";

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
                        case GameState.Canceled:
                            desc += "**State:** Cancelled\n";
                            embed.Color = Color.DarkOrange;
                            break;
                        case GameState.Draw:
                            desc += "**State:** Draw\n";
                            embed.Color = Color.Gold;
                            break;
                        case GameState.Picking:
                            remainingPlayers = true;
                            embed.Color = Color.Magenta;
                            break;
                        case GameState.Decided:
                            winningteam = true;
                            embed.Color = Color.Green;
                            break;
                        case GameState.Undecided:
                            break;
                    }
                }

                var cap1 = db.GetTeamCaptain(game.GuildId, game.LobbyId, game.GameId, 1);
                var cap2 = db.GetTeamCaptain(game.GuildId, game.LobbyId, game.GameId, 2);

                if (winningteam)
                {
                    var teamInfo = game.WinningTeam == 1 ? team1p : team2p;
                    var captainInfo = game.WinningTeam == 1 ? cap1 : cap2;
                    embed.AddField($"Winning Team, Team #{game.WinningTeam}", GetTeamInfo(captainInfo, team1p));
                    if (game.WinningTeam == 1)
                    {
                        team1 = false;
                    }
                    else if (game.WinningTeam == 2)
                    {
                        team2 = false;
                    }
                }

                if (team1)
                {
                    embed.AddField("Team 1", GetTeamInfo(cap1, team1p));
                }

                if (team2)
                {
                    embed.AddField("Team 2", GetTeamInfo(cap2, team2p));
                }

                if (remainingPlayers)
                {
                    var remaining = queue.Where(x => team1p.All(y => y == x) && team2p.All(y => y == x));
                    if (remaining.Any())
                    {
                        embed.AddField("Remaining Players", string.Join(" ", remaining.Select(x => MentionUtils.MentionUser(x))));
                    }
                }

                embed.Description = desc;



                return (message, embed);
            }
        }
        public (string, EmbedBuilder) GetGameMessage(GameResult game, HashSet<ulong> queue, HashSet<ulong> team1p, HashSet<ulong> team2p, string title = null, params GameFlag[] flags)
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

            var message = usermentions ? string.Join(" ", queue.Union(team1p).Union(team2p).Distinct().Select(x => MentionUtils.MentionUser(x))) : "";

            var embed = new EmbedBuilder
            {
                Color = Color.Blue
            };
            embed.Title = title ?? $"Game #{game.GameId}";
            var desc = "";

            if (time)
            {
                desc += $"**Creation Time:** {game.CreationTime.ToLocalTime().ToString("dd MMM yyyy")} {game.CreationTime.ToLocalTime().ToShortTimeString()}\n";
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
                    case GameState.Canceled:
                        desc += "**State:** Cancelled\n";
                        embed.Color = Color.DarkOrange;
                        break;
                    case GameState.Draw:
                        desc += "**State:** Draw\n";
                        embed.Color = Color.Gold;
                        break;
                    case GameState.Picking:
                        remainingPlayers = true;
                        embed.Color = Color.Magenta;
                        break;
                    case GameState.Decided:
                        winningteam = true;
                        embed.Color = Color.Green;
                        break;
                    case GameState.Undecided:
                        break;
                }
            }

            if (winningteam)
            {
                var teamInfo = game.WinningTeam == 1 ? team1p : team2p;
                embed.AddField($"Winning Team, Team #{game.WinningTeam}", GetTeamInfo(null, team1p));
                if (game.WinningTeam == 1)
                {
                    team1 = false;
                }
                else if (game.WinningTeam == 2)
                {
                    team2 = false;
                }
            }

            if (team1)
            {
                embed.AddField("Team 1", GetTeamInfo(null, team1p));
            }

            if (team2)
            {
                embed.AddField("Team 2", GetTeamInfo(null, team2p));
            }

            if (remainingPlayers)
            {
                var remaining = queue.Where(x => team1p.All(y => y == x) && team2p.All(y => y == x));
                if (remaining.Any())
                {
                    embed.AddField("Remaining Players", string.Join(" ", remaining.Select(x => MentionUtils.MentionUser(x))));
                }
            }

            embed.Description = desc;



            return (message, embed);

        }

        public EmbedBuilder GetGameEmbed(ManualGameResult game)
        {
            var embed = new EmbedBuilder();

            if (game.GameState == ManualGameState.Win)
            {
                embed.Color = Color.Green;
                embed.Title = "Win";
            }
            else if (game.GameState == ManualGameState.Lose)
            {
                embed.Color = Color.Red;
                embed.Title = "Lose";
            }
            else if (game.GameState == ManualGameState.Draw)
            {
                embed.Color = Color.Gold;
                embed.Title = "Draw";
            }
            else
            {
                embed.Color = Color.Blue;
                embed.Title = "Legacy";
            }

            using (var db = new Database())
            {
                var scoreUpdates = db.ManualGameScoreUpdates.Where(x => x.GuildId == game.GuildId && x.ManualGameId == game.GameId);
                embed.Description = $"**GameId:** {game.GameId}\n" +
                                    $"**Creation Time:** {game.CreationTime.ToString("dd MMM yyyy")} {game.CreationTime.ToShortTimeString()}\n" +
                                    $"**Comment:** {game.Comment ?? "N/A"}\n" +
                                    $"**Submitted By:** {MentionUtils.MentionUser(game.Submitter)}\n" +
                                    string.Join("\n", scoreUpdates.Select(x => $"{MentionUtils.MentionUser(x.UserId)} {(x.ModifyAmount >= 0 ? $"`+{x.ModifyAmount}`" : $"`{x.ModifyAmount}`")}"));
            }
            return embed;
        }

        public EmbedBuilder GetGameEmbed(GameResult game)
        {
            var embed = new EmbedBuilder();

            using (var db = new Database())
            {
                embed.AddField("Info",
                    $"**GameId:** {game.GameId}\n" +
                    $"**Lobby:** {MentionUtils.MentionChannel(game.LobbyId)}\n" +
                    $"**Creation Time:** {game.CreationTime.ToString("dd MMM yyyy")} {game.CreationTime.ToShortTimeString()}\n" +
                    $"**Pick Mode:** {game.GamePickMode}\n" +
                    $"{(game.MapName == null ? "" : $"**Map:** {game.MapName}\n")}" +
                    $"{(game.Comment == null ? "" : $"**Comment:** {game.Comment}\n")}");

                var queue = db.GetQueuedPlayers(game.GuildId, game.LobbyId);
                var team1p = db.GetTeamFull(game, 1);
                var team2p = db.GetTeamFull(game, 2);
                var cap1 = db.GetTeamCaptain(game.GuildId, game.LobbyId, game.GameId, 1);
                var cap2 = db.GetTeamCaptain(game.GuildId, game.LobbyId, game.GameId, 2);
                var queueRemaining = queue.Where(x => team1p.All(y => y != x.UserId) && team2p.All(y => y != x.UserId));

                var winningCap = game.WinningTeam == 1 ? cap1 : cap2;
                var winningPlayers = game.WinningTeam == 1 ? team1p : team2p;
                var losingCap = game.WinningTeam == 2 ? cap1 : cap2;
                var losingPlayers = game.WinningTeam == 2 ? team1p : team2p;
                var scoreUpdates = db.GetScoreUpdates(game.GuildId, game.LobbyId, game.GameId);

                if (game.GameState == GameState.Picking)
                {
                    embed.AddField("Picking Teams",
                                    $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                                    $"Team 2:\n{GetTeamInfo(cap2, team2p)}\n" +
                                    $"Remaining Players:\n{string.Join(" ", queueRemaining.Select(x => MentionUtils.MentionUser(x.UserId)))}");
                    embed.Color = Color.Magenta;
                }
                else if (game.GameState == GameState.Canceled)
                {
                    if (Lobby.IsCaptains(game.GamePickMode))
                    {
                        if (queueRemaining.Any())
                        {
                            embed.AddField("Canceled",
                                $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                                $"Team 2:\n{GetTeamInfo(cap2, team2p)}\n" +
                                $"Remaining Players:\n{string.Join(" ", queueRemaining.Select(x => MentionUtils.MentionUser(x.UserId)))}");
                        }
                        else
                        {
                            embed.AddField("Canceled",
                                $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                                $"Team 2:\n{GetTeamInfo(cap2, team2p)}");
                        }
                    }
                    else
                    {
                        embed.AddField("Canceled",
                            $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                            $"Team 2:\n{GetTeamInfo(cap2, team2p)}");
                    }

                    embed.Color = Color.DarkOrange;
                }
                else if (game.GameState == GameState.Draw)
                {
                    embed.AddField("Draw",
                         $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                         $"Team 2:\n{GetTeamInfo(cap2, team2p)}");
                    embed.Color = Color.Gold;
                }
                else if (game.GameState == GameState.Undecided)
                {
                    embed.AddField("Undecided",
                        $"Team 1:\n{GetTeamInfo(cap1, team1p)}\n" +
                        $"Team 2:\n{GetTeamInfo(cap2, team2p)}");
                    embed.Color = Color.Blue;
                }
                else if (game.GameState == GameState.Decided)
                {
                    var pointsAwarded = new List<string>();
                    pointsAwarded.Add($"**Team {game.WinningTeam}**");

                    foreach (var player in winningPlayers)
                    {
                        var eUser = db.Players.Find(game.GuildId, player);
                        if (eUser == null) continue;

                        var pointUpdate = scoreUpdates.FirstOrDefault(x => x.UserId == player);
                        pointsAwarded.Add($"{eUser.GetDisplayNameSafe()} - `+{pointUpdate.ModifyAmount}`");
                    }

                    pointsAwarded.Add($"**Team {(game.WinningTeam == 1 ? 2 : 1)}**");
                    foreach (var player in losingPlayers)
                    {
                        var eUser = db.Players.Find(game.GuildId, player);
                        if (eUser == null) continue;

                        var pointUpdate = scoreUpdates.FirstOrDefault(x => x.UserId == player);
                        pointsAwarded.Add($"{eUser.GetDisplayNameSafe()} - `{pointUpdate.ModifyAmount}`");
                    }
                    embed.AddField($"Winning Team, Team {game.WinningTeam}", GetTeamInfo(winningCap, winningPlayers));
                    embed.AddField($"Losing Team", GetTeamInfo(losingCap, losingPlayers));
                    embed.AddField("Score Updates", string.Join("\n", pointsAwarded));//.FixLength(1023));
                    embed.Color = Color.Green;
                }

                return embed;
            }
        }
    }
}
