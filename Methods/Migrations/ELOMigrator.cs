using Discord.Commands;
using ELO.Models;
using RavenBOT.Common;
using RavenBOT.Common.Interfaces.Database;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Premium;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenBOT.ELO.Modules.Methods.Migrations
{
    public class ELOMigrator : IServiceable
    {
        private IDatabase currentDatabase;
        private RavenDatabase oldDatabase = null;

        public ELOMigrator(IDatabase currentDatabase, LegacyIntegration legacy)
        {
            this.currentDatabase = currentDatabase;
            Legacy = legacy;
        }

        public bool RedeemToken(ulong guildId, string token)
        {
            var model = GetTokenModel();
            var match = model.TokenList.FirstOrDefault(x => x.Token == token);
            if (match == null) return false;

            var configSave = Legacy.GetPremiumConfig(guildId) ?? new LegacyIntegration.LegacyPremium
            {
                GuildId = guildId,
                ExpiryDate = DateTime.UtcNow - TimeSpan.FromMinutes(5)
            };

            if (configSave.ExpiryDate < DateTime.UtcNow - TimeSpan.FromHours(1))
            {
                configSave.ExpiryDate = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            }
            configSave.ExpiryDate = configSave.ExpiryDate + TimeSpan.FromDays(match.Days);
            Legacy.SaveConfig(configSave);
            model.TokenList.Remove(match);
            SaveTokenModel(model);
            return true;
        }

        public TokenModel GetTokenModel()
        {
            var model = currentDatabase.Load<TokenModel>("legacyELOTokens");
            return model;
        }

        public void SaveTokenModel(TokenModel model)
        {
            currentDatabase.Store(model, "legacyELOTokens");
        }

        public class TokenModel
        {
            public List<TokenClass> TokenList { get; set; } = new List<TokenClass>();

            public class TokenClass
            {
                /// <summary>
                ///     Gets or sets the Token
                /// </summary>
                public string Token { get; set; }

                /// <summary>
                ///     Gets or sets the days till expires
                /// </summary>
                public int Days { get; set; }
            }
        }

        public LegacyIntegration Legacy { get; }

        public void RunClear(ShardedCommandContext context)
        {
            var guildIds = context.Client.Guilds.Select(x => x.Id).ToArray();
            var removePlayers = currentDatabase.Query<Player>(x => !guildIds.Contains(x.GuildId));
            var removeLobbies = currentDatabase.Query<Lobby>(x => !guildIds.Contains(x.GuildId));
            var removeGuilds = currentDatabase.Query<CompetitionConfig>(x => !guildIds.Contains(x.GuildId));

            foreach (var player in removePlayers)
            {
                currentDatabase.Remove<Player>(Player.DocumentName(player.GuildId, player.UserId));
            }

            foreach (var lobby in removeLobbies)
            {
                currentDatabase.Remove<Lobby>(Lobby.DocumentName(lobby.GuildId, lobby.ChannelId));
            }

            foreach (var guild in removeGuilds)
            {
                currentDatabase.Remove<CompetitionConfig>(CompetitionConfig.DocumentName(guild.GuildId));
            }
        }

        public void RunMigration(LocalManagementService local)
        {
            if (oldDatabase == null)
            {
                oldDatabase = new RavenDatabase(local);
            }

            try
            {
                var tokenModel = oldDatabase.Load<TokenModel>("tokens");
                if (tokenModel != null)
                {
                    currentDatabase.Store(tokenModel, "legacyELOTokens");
                }


                using (var session = oldDatabase.DocumentStore.OpenSession())
                {
                    var query = session.Query<GuildModel>();

                    using (var enumerator = session.Advanced.Stream(query))
                    {
                        while (enumerator.MoveNext())
                        {
                            var config = enumerator.Current.Document;
                            try
                            {
                                //Do not use if new competition already exists
                                var newComp = currentDatabase.Load<CompetitionConfig>(CompetitionConfig.DocumentName(config.ID));
                                if (newComp != null)
                                {
                                    if (config.Settings.Premium.Expiry > DateTime.UtcNow)
                                    {
                                        Legacy.SaveConfig(new LegacyIntegration.LegacyPremium
                                        {
                                            GuildId = config.ID,
                                            ExpiryDate = config.Settings.Premium.Expiry
                                        });
                                    }
                                    continue;
                                }

                                newComp = new CompetitionConfig();

                                //Do not set this due to incompatibilities with new replacements
                                //newComp.NameFormat = config.Settings.Registration.NameFormat;
                                newComp.UpdateNames = true;
                                newComp.RegisterMessageTemplate = config.Settings.Registration.Message;
                                newComp.RegisteredRankId = config.Ranks.FirstOrDefault(x => x.IsDefault)?.RoleID ?? 0;
                                newComp.Ranks = config.Ranks.Select(x => new Rank
                                {
                                    RoleId = x.RoleID,
                                    WinModifier = x.WinModifier,
                                    LossModifier = x.LossModifier,
                                    Points = x.Threshold
                                }).ToList();
                                newComp.GuildId = config.ID;
                                newComp.DefaultWinModifier = config.Settings.Registration.DefaultWinModifier;
                                newComp.DefaultLossModifier = config.Settings.Registration.DefaultLossModifier;
                                newComp.AllowReRegister = config.Settings.Registration.AllowMultiRegistration;
                                newComp.AllowSelfRename = config.Settings.Registration.AllowMultiRegistration;
                                newComp.AllowNegativeScore = config.Settings.GameSettings.AllowNegativeScore;
                                newComp.BlockMultiQueueing = config.Settings.GameSettings.BlockMultiQueuing;
                                newComp.AdminRole = config.Settings.Moderation.AdminRoles.FirstOrDefault();
                                newComp.ModeratorRole = config.Settings.Moderation.ModRoles.FirstOrDefault();
                                newComp.RegistrationCount = config.Users.Count;
                                if (config.Settings.GameSettings.ReQueueDelay != TimeSpan.Zero)
                                {
                                    newComp.RequeueDelay = config.Settings.GameSettings.ReQueueDelay;
                                }
                                //TODO: Remove user on afk   

                                if (config.Settings.Premium.Expiry > DateTime.UtcNow)
                                {
                                    Legacy.SaveConfig(new LegacyIntegration.LegacyPremium
                                    {
                                        GuildId = config.ID,
                                        ExpiryDate = config.Settings.Premium.Expiry
                                    });
                                }

                                currentDatabase.Store(newComp, CompetitionConfig.DocumentName(config.ID));

                                foreach (var game in config.Results)
                                {
                                    //Skip games for lobbys that dont exist
                                    var lobby = config.Lobbies.FirstOrDefault(x => x.ChannelID == game.LobbyID);
                                    if (lobby == null) continue;

                                    var newResult = new GameResult();
                                    newResult.GameId = game.GameNumber;
                                    newResult.LobbyId = game.LobbyID;
                                    newResult.GuildId = config.ID;
                                    newResult.CreationTime = game.Time;
                                    
                                    if (game.Result == GuildModel.GameResult._Result.Canceled)
                                    {
                                        newResult.GameState = GameResult.State.Canceled;
                                    }
                                    else if (game.Result == GuildModel.GameResult._Result.Team1)
                                    {
                                        newResult.GameState = GameResult.State.Decided;
                                        newResult.WinningTeam = 1;
                                    }
                                    else if (game.Result == GuildModel.GameResult._Result.Team2)
                                    {
                                        newResult.GameState = GameResult.State.Decided;
                                        newResult.WinningTeam = 2;
                                    }
                                    else if (game.Result == GuildModel.GameResult._Result.Undecided)
                                    {
                                        newResult.GameState = GameResult.State.Undecided;
                                    }

                                    newResult.Team1.Players = game.Team1.ToHashSet();
                                    newResult.Team2.Players = game.Team2.ToHashSet();

                                    if (lobby.PickMode == GuildModel.Lobby._PickMode.Captains)
                                    {
                                        newResult.GamePickMode = Lobby.PickMode.Captains_HighestRanked;
                                        newResult.PickOrder = GameResult.CaptainPickOrder.PickOne;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.Pick2)
                                    {
                                        newResult.GamePickMode = Lobby.PickMode.Captains_HighestRanked;
                                        newResult.PickOrder = GameResult.CaptainPickOrder.PickTwo;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.CompleteRandom)
                                    {
                                        newResult.GamePickMode = Lobby.PickMode.Random;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.SortByScore)
                                    {
                                        newResult.GamePickMode = Lobby.PickMode.TryBalance;
                                    }

                                    newResult.LegacyGame = true;
                                    currentDatabase.Store(newResult, GameResult.DocumentName(newResult.GameId, newResult.LobbyId, newResult.GuildId));
                                    

                                    //Untransferrable info: Captains
                                    //Unfortunately, the old ELO bot didn't store captains
                                    //Also the undogame method will not work on these
                                }

                                foreach (var lobby in config.Lobbies)
                                {
                                    var newLobby = new Lobby();
                                    newLobby.GuildId = config.ID;
                                    newLobby.ChannelId = lobby.ChannelID;
                                    newLobby.DmUsersOnGameReady = config.Settings.GameSettings.DMAnnouncements;
                                    newLobby.GameResultAnnouncementChannel = config.Settings.GameSettings.AnnouncementsChannel;
                                    newLobby.GameReadyAnnouncementChannel = config.Settings.GameSettings.AnnouncementsChannel;
                                    newLobby.PlayersPerTeam = lobby.UserLimit / 2;
                                    newLobby.Description = lobby.Description;
                                    var results = config.Results.Where(x => x.LobbyID == lobby.ChannelID).ToArray();
                                    if (results.Length == 0)
                                    {
                                        newLobby.CurrentGameCount = 0;
                                    }
                                    else
                                    {
                                        newLobby.CurrentGameCount = results.Max(x => x.GameNumber) + 1;
                                    }

                                    if (lobby.Maps.Any())
                                    {
                                        newLobby.MapSelector = new MapSelector();
                                        newLobby.MapSelector.Maps = lobby.Maps.ToHashSet();
                                    }
                                    //TODO: Lobby requeue delay     

                                    
                                    if (lobby.PickMode == GuildModel.Lobby._PickMode.Captains)
                                    {
                                        newLobby.TeamPickMode = Lobby.PickMode.Captains_HighestRanked;
                                        newLobby.CaptainPickOrder = GameResult.CaptainPickOrder.PickOne;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.CompleteRandom)
                                    {
                                        newLobby.TeamPickMode = Lobby.PickMode.Random;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.Pick2)
                                    {
                                        newLobby.CaptainPickOrder = GameResult.CaptainPickOrder.PickTwo;
                                        newLobby.TeamPickMode = Lobby.PickMode.Captains_HighestRanked;
                                    }
                                    else if (lobby.PickMode == GuildModel.Lobby._PickMode.SortByScore)
                                    {
                                        newLobby.TeamPickMode = Lobby.PickMode.TryBalance;
                                    }

                                    currentDatabase.Store(newLobby, Lobby.DocumentName(config.ID, lobby.ChannelID));
                                }

                                foreach (var user in config.Users)
                                {
                                    var newUser = new Player(user.UserID, config.ID, user.Username);
                                    newUser.Points = user.Stats.Points;
                                    newUser.Wins = user.Stats.Wins;
                                    newUser.Losses = user.Stats.Losses;
                                    newUser.Draws = user.Stats.Draws;

                                    //TODO: Kills/Deaths/Assists

                                    if (user.Banned != null && user.Banned.Banned)
                                    {
                                        var length = user.Banned.ExpiryTime - DateTime.UtcNow;
                                        newUser.BanHistory.Add(new Player.Ban(length, user.Banned.Moderator, user.Banned.Reason));
                                    }

                                    currentDatabase.Store(newUser, Player.DocumentName(config.ID, user.UserID));
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}