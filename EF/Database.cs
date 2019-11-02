using ELO.EF.Models;
using ELO.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ELO.EF
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Rank>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.RoleId });
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
                entity.HasKey(e => e.GuildId);
                entity.Property(e => e.AdminRole).HasDefaultValue();
                entity.Property(e => e.ModeratorRole).HasDefaultValue();
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


            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.UserId });
            });

            // Game Setup
            modelBuilder.Entity<Lobby>(entity =>
            {
                entity.HasKey(e => new { e.GuildId, e.ChannelId });
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
                entity.HasKey(e => new { e.GuildId, e.LobbyId, e.GameId });
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
        }
    }
}
