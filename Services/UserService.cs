using Discord.WebSocket;
using ELO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class UserService
    {
        public async Task<List<string>> UpdateUserAsync(Competition comp, Player player, Rank[] ranks, SocketGuildUser user)
        {
            var noted = new List<string>();
            try
            {
                if (user.Guild.CurrentUser.GuildPermissions.ManageRoles)
                {
                    var rankMatches = ranks.Where(x => x.Points <= player.Points);
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
                                    await user.RemoveRolesAsync(gRoles);
                                    await user.AddRoleAsync(role);

                                    noted.Add($"{user.Mention} received the {(role.IsMentionable ? role.Mention : role.Name)} rank");
                                }
                                else
                                {
                                    noted.Add($"The {(role.IsMentionable ? role.Mention : role.Name)} rank is above ELO bot's highest role and cannot be added to the user");
                                }
                            }
                            else
                            {
                                //TODO: inject db and remove this automatically
                                using (var db = new Database())
                                {
                                    noted.Add($"A rank could not be found in the server and has been automatically removed. [{match.RoleId} w:{match.WinModifier} l:{match.LossModifier} p:{match.Points}]");
                                    var rnk = db.Ranks.Find(match.GuildId, match.RoleId);
                                    db.Ranks.Remove(rnk);
                                }
                            }
                        }
                    }

                    //Ensure the user has the registerd role if it exists.
                    if (comp.RegisteredRankId != null)
                    {
                        if (!user.Roles.Any(x => x.Id == comp.RegisteredRankId))
                        {
                            var role = user.Guild.GetRole(comp.RegisteredRankId.Value);
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
            }
            catch (Exception e)
            {
                noted.Add($"Issue updating {user.Mention} name/roles.");
            }


            return noted;
        }
    }
}
