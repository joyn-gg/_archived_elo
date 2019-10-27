using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using RavenBOT.Common;
using RavenBOT.ELO.Modules.Models;

namespace RavenBOT.ELO.Modules.Methods
{
    public partial class ELOService
    {
        public async Task<List<string>> UpdateUserAsync(CompetitionConfig comp, Player player, SocketGuildUser user)
        {
            var noted = new List<string>();
            if (user.Guild.CurrentUser.GuildPermissions.ManageRoles)
            {
                var rankMatches = comp.Ranks.Where(x => x.Points <= player.Points);
                if (rankMatches.Any())
                {
                    //Get the highest rank that the user can receive from the bot.
                    var maxRank = rankMatches.Max(x => x.Points);
                    var match = rankMatches.First(x => x.Points == maxRank);

                    //Remove other rank roles.
                    var gRoles = user.Guild.Roles.Where(x => rankMatches.Any(r => r.RoleId == x.Id) && x.Id != match.RoleId && x.IsEveryone == false && x.IsManaged == false && x.Position < user.Guild.CurrentUser.Hierarchy).ToList();

                    //Check to see if the player already has the role
                    if (!user.Roles.Any(x => x.Id == match.RoleId))
                    {
                        //Try to retrieve the role in the server
                        var role = user.Guild.GetRole(match.RoleId);
                        if (role != null)
                        {
                            if (role.Position < user.Guild.CurrentUser.Hierarchy)
                            {
                                await user.AddRoleAsync(role);
                                await user.ModifyAsync(x =>
                                {
                                    var ids = user.Roles.Select(r => r.Id).ToList();
                                    ids.RemoveAll(r => gRoles.Any(g => g.Id == r));
                                    ids.Remove(user.Guild.EveryoneRole.Id);
                                    ids.Add(role.Id);

                                    x.RoleIds = ids;
                                });
                                noted.Add($"{user.Mention} received the {(role.IsMentionable ? role.Mention : role.Name)} rank");
                            }
                            else
                            {
                                noted.Add($"The {(role.IsMentionable ? role.Mention : role.Name)} rank is above ELO bot's highest role and cannot be added to the user");
                            }
                        }
                        else
                        {
                            comp.Ranks.Remove(match);
                            noted.Add($"A rank could not be found in the server and was subsequently deleted from the server config [{match.RoleId} w:{match.WinModifier} l:{match.LossModifier} p:{match.Points}]");
                            SaveCompetition(comp);
                        }
                    }
                }

                //Ensure the user has the registerd role if it exists.
                if (comp.RegisteredRankId != 0)
                {
                    if (!user.Roles.Any(x => x.Id == comp.RegisteredRankId))
                    {
                        var role = user.Guild.GetRole(comp.RegisteredRankId);
                        if (role != null)
                        {
                            if (role.Position < user.Guild.CurrentUser.Hierarchy)
                            {
                                await user.AddRoleAsync(role);
                            }
                        }
                    }
                }
            }
            else
            {
                noted.Add("The bot requires manage roles permissions in order to modify user roles.");
            }

            if (comp.UpdateNames)
            {
                var newName = comp.GetNickname(player);
                var currentName = user.Nickname ?? user.Username;
                //TODO: Investigate null ref here?
                //Not sure if newname or current name could be null.
                if (!currentName.Equals(newName, StringComparison.InvariantCultureIgnoreCase))
                {
                    //Use heirachy check to ensure that the bot can actually set the nickname
                    if (user.Guild.CurrentUser.GuildPermissions.ManageNicknames)
                    {
                        if (user.Hierarchy < user.Guild.CurrentUser.Hierarchy)
                        {
                            await user.ModifyAsync(x => x.Nickname = newName);
                        }
                        else
                        {
                            noted.Add("You have a higher permission level than the bot and therefore it cannot edit your nickname.");
                        }
                    }
                    else
                    {
                        noted.Add("The bot cannot edit your nickname as it does not have the `ManageNicknames` permission");
                    }
                }
            }

            return noted;
        }

        public(ulong, ulong) GetCaptains(Lobby lobby, GameResult game, Random rnd)
        {
            ulong cap1 = 0;
            ulong cap2 = 0;
            if (lobby.TeamPickMode == Lobby.PickMode.Captains_RandomHighestRanked)
            {
                //Select randomly from the top 4 ranked players in the queue
                if (game.Queue.Count >= 4)
                {
                    var players = game.Queue.Select(x => GetPlayer(game.GuildId, x)).Where(x => x != null).OrderByDescending(x => x.Points).Take(4).OrderBy(x => rnd.Next()).ToList();
                    cap1 = players[0].UserId;
                    cap2 = players[1].UserId;
                }
                //Select the two players at random.
                else
                {
                    var randomised = game.Queue.OrderBy(x => rnd.Next()).Take(2).ToList();
                    cap1 = randomised[0];
                    cap2 = randomised[1];
                }
            }
            else if (lobby.TeamPickMode == Lobby.PickMode.Captains_Random)
            {
                //Select two players at random.
                var randomised = game.Queue.OrderBy(x => rnd.Next()).Take(2).ToList();
                cap1 = randomised[0];
                cap2 = randomised[1];
            }
            else if (lobby.TeamPickMode == Lobby.PickMode.Captains_HighestRanked)
            {
                //Select top two players
                var players = game.Queue.Select(x => GetPlayer(game.GuildId, x)).Where(x => x != null).OrderByDescending(x => x.Points).Take(2).ToList();
                cap1 = players[0].UserId;
                cap2 = players[1].UserId;
            }
            else
            {
                throw new Exception("Unknown captain pick mode.");
            }

            return (cap1, cap2);
        }
    }
}