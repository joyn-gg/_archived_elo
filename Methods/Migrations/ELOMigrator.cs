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
                                if (newComp != null) continue;
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
                                    if (lobby.Maps.Any())
                                    {
                                        newLobby.MapSelector = new MapSelector();
                                        newLobby.MapSelector.Maps = lobby.Maps.ToHashSet();
                                    }
                                    //TODO: Lobby requeue delay      

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