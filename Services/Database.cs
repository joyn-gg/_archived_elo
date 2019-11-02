using ELO.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ELO.Services
{
    public class Database : DbContext
    {
        public static DatabaseConfig Config;

        public DbSet<Rank> Ranks { get; set; }
        public DbSet<CommandPermission> Permissions { get; set; }
        public DbSet<Competition> Competitions { get; set; }
        public DbSet<Lobby> Lobbies { get; set; }
        public DbSet<QueuedPlayer> QueuedPlayers { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Ban> Bans { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(Config.ConnectionString(), mySqlOptions =>
                {
                    mySqlOptions.ServerVersion(Config.Version, ServerType.MySql);
                });
        }

        public Competition GetOrCreateCompetition(ulong guildId)
        {
            var comp = Competitions.Find(guildId);
            if (comp == null)
            {
                comp = new Competition(guildId);
                Competitions.Add(comp);
            }

            return comp;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Rank>(entity =>
            {
                entity.HasKey(e => e.RoleId);
                entity.Property(e => e.Points).IsRequired();
                entity.Property(e => e.WinModifier);
                entity.Property(e => e.LossModifier);
            });

            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.UserId });
                entity.Property(e => e.Points).IsRequired();
                entity.Property(e => e.Wins).IsRequired();
                entity.Property(e => e.Losses).IsRequired();
                entity.Property(e => e.Draws).IsRequired();
                entity.Property(e => e.DisplayName).IsRequired();
                entity.Property(e => e.RegistrationDate).IsRequired();
            });

            modelBuilder.Entity<Competition>(entity =>
            {
                entity.Property(e => e.AdminRole);
                entity.Property(e => e.ModeratorRole);
                entity.Property(e => e.RequeueDelay);
                entity.Property(e => e.RegisteredRankId);
                entity.Property(e => e.RegisterMessageTemplate);
                entity.Property(e => e.NameFormat);
                //entity.Property(e => e.RegistrationCount).IsRequired();
                entity.Property(e => e.AllowMultiQueueing).IsRequired();
                entity.Property(e => e.AllowNegativeScore).IsRequired();
                entity.Property(e => e.AllowReRegister).IsRequired();

                entity.Property(e => e.AllowSelfRename).IsRequired();
                entity.Property(e => e.DefaultWinModifier).IsRequired();
                entity.Property(e => e.DefaultLossModifier).IsRequired();
                entity.Property(e => e.PremiumRedeemer);
            });


            modelBuilder.Entity<CommandPermission>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ComandName });
            });

            modelBuilder.Entity<Ban>(entity =>
            {
            });

            // Game Setup
            modelBuilder.Entity<Lobby>(entity =>
            {
                entity.HasKey(e => e.ChannelId);
                /*entity.Property(e => e.Description);
                entity.Property(e => e.GameReadyAnnouncementChannel);
                entity.Property(e => e.MentionUsersInReadyAnnouncement).IsRequired();
                entity.Property(e => e.GameResultAnnouncementChannel);
                entity.Property(e => e.MinimumPoints);
                entity.Property(e => e.DmUsersOnGameReady).IsRequired();
                //entity.Property(e => e.ReactOnJoinLeave).IsRequired();
                entity.Property(e => e.HideQueue).IsRequired();
                entity.Property(e => e.PlayersPerTeam).IsRequired();

                entity.Property(e => e.TeamPickMode).IsRequired();
                entity.Property(e => e.CaptainPickOrder).IsRequired();*/
            });

            modelBuilder.Entity<GameResult>(entity =>
            {
                entity.HasAlternateKey(e => new { e.GuildId, e.LobbyId, e.GameId });
            });

            modelBuilder.Entity<QueuedPlayer>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ChannelId, e.UserId });
                      
            });

            modelBuilder.Entity<TeamPlayer>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ChannelId, e.UserId });
            });

            modelBuilder.Entity<TeamCaptain>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ChannelId, e.UserId, e.GameNumber, e.TeamNumber });
            });

            modelBuilder.Entity<ScoreUpdate>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ChannelId, e.UserId, e.GameNumber });
            });

            modelBuilder.Entity<ManualGameScoreUpdate>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ManualGameId, e.UserId });
            });

            modelBuilder.Entity<ManualGameResult>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.GameId });
                entity.HasMany(x => x.ScoreUpdates);
            });
        }
    }
}
