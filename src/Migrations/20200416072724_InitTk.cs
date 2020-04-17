using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace ELO.Migrations
{
    public partial class InitTk : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(nullable: false),
                    Prefix = table.Column<string>(nullable: true),
                    AdminRole = table.Column<decimal>(nullable: true),
                    ModeratorRole = table.Column<decimal>(nullable: true),
                    RequeueDelay = table.Column<TimeSpan>(nullable: true),
                    RegisteredRankId = table.Column<decimal>(nullable: true),
                    ManualGameCounter = table.Column<int>(nullable: false),
                    DisplayErrors = table.Column<bool>(nullable: false),
                    RegisterMessageTemplate = table.Column<string>(nullable: true),
                    NameFormat = table.Column<string>(nullable: true),
                    UpdateNames = table.Column<bool>(nullable: false),
                    AllowMultiQueueing = table.Column<bool>(nullable: false),
                    AllowNegativeScore = table.Column<bool>(nullable: false),
                    AllowReRegister = table.Column<bool>(nullable: false),
                    AllowSelfRename = table.Column<bool>(nullable: false),
                    AllowVoting = table.Column<bool>(nullable: false),
                    DefaultRegisterScore = table.Column<int>(nullable: false),
                    QueueTimeout = table.Column<TimeSpan>(nullable: true),
                    DefaultWinModifier = table.Column<int>(nullable: false),
                    DefaultLossModifier = table.Column<int>(nullable: false),
                    PremiumRedeemer = table.Column<decimal>(nullable: true),
                    LegacyPremiumExpiry = table.Column<DateTime>(nullable: true),
                    PremiumBuffer = table.Column<DateTime>(nullable: true),
                    BufferedPremiumCount = table.Column<int>(nullable: true),
                    ReactiveMessage = table.Column<decimal>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "LegacyTokens",
                columns: table => new
                {
                    Key = table.Column<string>(nullable: false),
                    Days = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyTokens", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PremiumRoles",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(nullable: false),
                    Limit = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumRoles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Bans",
                columns: table => new
                {
                    BanId = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false),
                    Comment = table.Column<string>(nullable: true),
                    Moderator = table.Column<decimal>(nullable: false),
                    Length = table.Column<TimeSpan>(nullable: false),
                    TimeOfBan = table.Column<DateTime>(nullable: false),
                    ManuallyDisabled = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bans", x => x.BanId);
                    table.ForeignKey(
                        name: "FK_Bans_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lobbies",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    GameReadyAnnouncementChannel = table.Column<decimal>(nullable: true),
                    MentionUsersInReadyAnnouncement = table.Column<bool>(nullable: false),
                    GameResultAnnouncementChannel = table.Column<decimal>(nullable: true),
                    MinimumPoints = table.Column<int>(nullable: true),
                    LobbyMultiplier = table.Column<double>(nullable: false),
                    MultiplyLossValue = table.Column<bool>(nullable: false),
                    HighLimit = table.Column<int>(nullable: true),
                    ReductionPercent = table.Column<double>(nullable: false),
                    DmUsersOnGameReady = table.Column<bool>(nullable: false),
                    HideQueue = table.Column<bool>(nullable: false),
                    PlayersPerTeam = table.Column<int>(nullable: false),
                    TeamPickMode = table.Column<int>(nullable: false),
                    CurrentGameCount = table.Column<int>(nullable: false),
                    CaptainPickOrder = table.Column<int>(nullable: false),
                    HostSelectionMode = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lobbies", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_Lobbies_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualGameResults",
                columns: table => new
                {
                    GameId = table.Column<int>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    Comment = table.Column<string>(nullable: true),
                    Submitter = table.Column<decimal>(nullable: false),
                    GameState = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualGameResults", x => new { x.GuildId, x.GameId });
                    table.ForeignKey(
                        name: "FK_ManualGameResults_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(nullable: false),
                    CommandName = table.Column<string>(nullable: false),
                    Level = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => new { x.GuildId, x.CommandName });
                    table.ForeignKey(
                        name: "FK_Permissions_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    UserId = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    DisplayName = table.Column<string>(nullable: true),
                    Points = table.Column<int>(nullable: false),
                    Wins = table.Column<int>(nullable: false),
                    Losses = table.Column<int>(nullable: false),
                    Draws = table.Column<int>(nullable: false),
                    RegistrationDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Players_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ranks",
                columns: table => new
                {
                    RoleId = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    Points = table.Column<int>(nullable: false),
                    WinModifier = table.Column<int>(nullable: true),
                    LossModifier = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranks", x => x.RoleId);
                    table.ForeignKey(
                        name: "FK_Ranks_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameResults",
                columns: table => new
                {
                    GameId = table.Column<int>(nullable: false),
                    LobbyId = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    CreationTime = table.Column<DateTime>(nullable: false),
                    GameState = table.Column<int>(nullable: false),
                    Comment = table.Column<string>(nullable: true),
                    MapName = table.Column<string>(nullable: true),
                    Submitter = table.Column<decimal>(nullable: false),
                    PickOrder = table.Column<int>(nullable: false),
                    GamePickMode = table.Column<int>(nullable: false),
                    WinningTeam = table.Column<int>(nullable: false),
                    Picks = table.Column<int>(nullable: false),
                    VoteComplete = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameResults", x => new { x.LobbyId, x.GameId });
                    table.ForeignKey(
                        name: "FK_GameResults_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameResults_Lobbies_LobbyId",
                        column: x => x.LobbyId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    MapName = table.Column<string>(nullable: false),
                    ChannelId = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => new { x.ChannelId, x.MapName });
                    table.ForeignKey(
                        name: "FK_Maps_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyMembers",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false),
                    PartyHost = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMembers", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PartyMembers_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueuedPlayers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(nullable: false),
                    ChannelId = table.Column<decimal>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    QueuedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedPlayers", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_QueuedPlayers_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QueuedPlayers_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualGameScoreUpdates",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false),
                    ManualGameId = table.Column<int>(nullable: false),
                    ModifyAmount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualGameScoreUpdates", x => new { x.GuildId, x.ManualGameId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ManualGameScoreUpdates_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManualGameScoreUpdates_ManualGameResults_GuildId_ManualGame~",
                        columns: x => new { x.GuildId, x.ManualGameId },
                        principalTable: "ManualGameResults",
                        principalColumns: new[] { "GuildId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoreUpdates",
                columns: table => new
                {
                    UserId = table.Column<decimal>(nullable: false),
                    ChannelId = table.Column<decimal>(nullable: false),
                    GameNumber = table.Column<int>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    ModifyAmount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreUpdates", x => new { x.ChannelId, x.UserId, x.GameNumber });
                    table.ForeignKey(
                        name: "FK_ScoreUpdates_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreUpdates_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreUpdates_GameResults_ChannelId_GameNumber",
                        columns: x => new { x.ChannelId, x.GameNumber },
                        principalTable: "GameResults",
                        principalColumns: new[] { "LobbyId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamCaptains",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(nullable: false),
                    GameNumber = table.Column<int>(nullable: false),
                    TeamNumber = table.Column<int>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamCaptains", x => new { x.ChannelId, x.GameNumber, x.TeamNumber });
                    table.ForeignKey(
                        name: "FK_TeamCaptains_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamCaptains_GameResults_ChannelId_GameNumber",
                        columns: x => new { x.ChannelId, x.GameNumber },
                        principalTable: "GameResults",
                        principalColumns: new[] { "LobbyId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayers",
                columns: table => new
                {
                    ChannelId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false),
                    GameNumber = table.Column<int>(nullable: false),
                    TeamNumber = table.Column<int>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayers", x => new { x.ChannelId, x.UserId, x.GameNumber, x.TeamNumber });
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_GameResults_ChannelId_GameNumber",
                        columns: x => new { x.ChannelId, x.GameNumber },
                        principalTable: "GameResults",
                        principalColumns: new[] { "LobbyId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    GameId = table.Column<int>(nullable: false),
                    GuildId = table.Column<decimal>(nullable: false),
                    ChannelId = table.Column<decimal>(nullable: false),
                    UserId = table.Column<decimal>(nullable: false),
                    UserVote = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => new { x.GuildId, x.ChannelId, x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_Votes_Lobbies_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Lobbies",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Competitions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Competitions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_GameResults_ChannelId_GameId",
                        columns: x => new { x.ChannelId, x.GameId },
                        principalTable: "GameResults",
                        principalColumns: new[] { "LobbyId", "GameId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bans_GuildId",
                table: "Bans",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GameResults_GuildId",
                table: "GameResults",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Lobbies_GuildId",
                table: "Lobbies",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_GuildId",
                table: "PartyMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedPlayers_GuildId",
                table: "QueuedPlayers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Ranks_GuildId",
                table: "Ranks",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreUpdates_GuildId",
                table: "ScoreUpdates",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreUpdates_ChannelId_GameNumber",
                table: "ScoreUpdates",
                columns: new[] { "ChannelId", "GameNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_GuildId",
                table: "TeamPlayers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_ChannelId_GameNumber",
                table: "TeamPlayers",
                columns: new[] { "ChannelId", "GameNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ChannelId_GameId",
                table: "Votes",
                columns: new[] { "ChannelId", "GameId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /*
            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "LegacyTokens");

            migrationBuilder.DropTable(
                name: "ManualGameScoreUpdates");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "PartyMembers");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "PremiumRoles");

            migrationBuilder.DropTable(
                name: "QueuedPlayers");

            migrationBuilder.DropTable(
                name: "Ranks");

            migrationBuilder.DropTable(
                name: "ScoreUpdates");

            migrationBuilder.DropTable(
                name: "TeamCaptains");

            migrationBuilder.DropTable(
                name: "TeamPlayers");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "ManualGameResults");

            migrationBuilder.DropTable(
                name: "GameResults");

            migrationBuilder.DropTable(
                name: "Lobbies");

            migrationBuilder.DropTable(
                name: "Competitions");*/
        }
    }
}