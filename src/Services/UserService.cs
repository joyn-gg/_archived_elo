using Discord;
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
        public virtual async Task<List<string>> UpdateUserAsync(Competition comp, Player player, Rank[] ranks, SocketGuildUser user)
        {
            var noted = new List<string>();
            try
            {
                var currentRoles = user.Roles.ToList();

                // Track the removed roles (if any)
                List<SocketRole> toRemove = new List<SocketRole>();
                bool modifyRoles = false;

                // Track the newly added role (if added)
                Rank toAdd = null;
                ulong? addRegisterRole = null;

                if (user.Guild.CurrentUser.GuildPermissions.ManageRoles)
                {
                    // Find Ranks that have less points than the current user.
                    var rankMatches = ranks.Where(x => x.Points <= player.Points);
                    if (rankMatches.Any())
                    {
                        var maxRank = rankMatches.Max(x => x.Points);
                        var match = rankMatches.First(x => x.Points == maxRank);

                        // Ensure user has their max role.
                        var roleToAdd = user.Guild.GetRole(match.RoleId);

                        if (roleToAdd != null && roleToAdd.Position < user.Guild.CurrentUser.Hierarchy)
                        {
                            modifyRoles = true;
                            toAdd = match;
                        }
                    }

                    //Ensure the user has the registerd role if it exists.
                    if (comp.RegisteredRankId.HasValue)
                    {
                        if (comp.RegisteredRankId.Value != toAdd?.RoleId)
                        {
                            var role = user.Guild.GetRole(comp.RegisteredRankId.Value);
                            if (role != null && role.Position < user.Guild.CurrentUser.Hierarchy)
                            {
                                modifyRoles = true;
                                addRegisterRole = comp.RegisteredRankId.Value;
                            }
                        }
                    }

                    // Remove all ranks that are not the max
                    foreach (var rank in ranks)
                    {
                        if (rank.RoleId == toAdd?.RoleId || rank.RoleId == comp.RegisteredRankId) continue;
                        var roleMatch = currentRoles.FirstOrDefault(x => x.Id == rank.RoleId);
                        if (roleMatch == null) continue;

                        if (roleMatch.Position >= user.Guild.CurrentUser.Hierarchy)
                        {
                            // Cannot remove the role if it is higher than the bot's position.
                            continue;
                        }

                        if (roleMatch.IsEveryone || roleMatch.IsManaged)
                        {
                            // Cannot remove/add managed or everyone role from user.
                            continue;
                        }

                        if (currentRoles.RemoveAll(x => x.Id == rank.RoleId) > 0)
                        {
                            modifyRoles = true;
                            toRemove.Add(roleMatch);
                        }
                    }
                }
                else
                {
                    noted.Add("The bot requires manage roles permissions in order to modify user roles.");
                }

                bool updateName = false;
                string newName = null;
                if (comp.UpdateNames)
                {
                    newName = comp.GetNickname(player);
                    var currentName = user.Nickname ?? user.Username;

                    if (newName != null && !currentName.Equals(newName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //Use heirachy check to ensure that the bot can actually set the nickname
                        if (user.Guild.CurrentUser.GuildPermissions.ManageNicknames)
                        {
                            if (user.Hierarchy < user.Guild.CurrentUser.Hierarchy)
                            {
                                updateName = true;
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

                if (modifyRoles || updateName)
                {
                    try
                    {
                        var finalRoles = currentRoles.Where(x => !x.IsEveryone).Select(x => x.Id).ToList();

                        bool removedRanks = false;
                        if (toRemove.Any())
                        {
                            removedRanks = true;
                            foreach (var role in toRemove)
                            {
                                finalRoles.Remove(role.Id);
                            }
                        }

                        bool newRankAdded = false;
                        if (toAdd != null && !finalRoles.Contains(toAdd.RoleId))
                        {
                            newRankAdded = true;
                            finalRoles.Add(toAdd.RoleId);
                        }

                        bool registerRankAdded = false;
                        if (addRegisterRole != null && !finalRoles.Contains(addRegisterRole.Value))
                        {
                            registerRankAdded = true;
                            finalRoles.Add(addRegisterRole.Value);
                        }

                        if (removedRanks || newRankAdded || registerRankAdded || updateName)
                        {
                            await user.ModifyAsync(x =>
                            {
                                if (removedRanks || newRankAdded || registerRankAdded)
                                {
                                    x.RoleIds = finalRoles;
                                }

                                if (updateName)
                                {
                                    x.Nickname = newName;
                                }
                            });

                            if (removedRanks)
                            {
                                noted.Add($"Removed rank(s): {string.Join(" ", toRemove.Select(x => MentionUtils.MentionRole(x.Id)))}");
                            }

                            if (newRankAdded)
                            {
                                noted.Add($"Added rank: {MentionUtils.MentionRole(toAdd.RoleId)}");
                            }

                            if (registerRankAdded)
                            {
                                noted.Add($"{user.Mention} received the {MentionUtils.MentionRole(addRegisterRole.Value)} rank");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        noted.Add($"There was an error updaing your roles.");
                        Console.WriteLine(e);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                noted.Add($"Issue updating {user.Mention} name/roles.");
            }

            return noted;
        }
    }
}