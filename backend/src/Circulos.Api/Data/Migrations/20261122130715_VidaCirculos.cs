using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VidaCirculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnedCircleCount",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "StorageUsedBytes",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "VacationUntil",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PhotoSizeBytes",
                table: "profiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_deletion_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuccessorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Discoverable = table.Column<bool>(type: "boolean", nullable: false),
                    IsSurprise = table.Column<bool>(type: "boolean", nullable: false),
                    SurpriseSubjectUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circles_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "life_areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Suggested = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_life_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_life_areas_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_resets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_resets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_resets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextFireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UntilAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    CompletedCount = table.Column<int>(type: "integer", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                    SkipNext = table.Column<bool>(type: "boolean", nullable: false),
                    PersonName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AdvanceDays = table.Column<int>(type: "integer", nullable: false),
                    GiftIdeasJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reminders_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sent_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    To = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_emails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shortcuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActionKind = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shortcuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shortcuts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_invites_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_join_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_join_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_join_requests_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_circle_join_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_logs_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_members_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_circle_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_circle_posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_circle_posts_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_circle_posts_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "life_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LifeAreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_life_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_life_entries_life_areas_LifeAreaId",
                        column: x => x.LifeAreaId,
                        principalTable: "life_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_life_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_TokenHash",
                table: "account_deletion_requests",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_UserId",
                table: "account_deletion_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_invites_CircleId",
                table: "circle_invites",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_invites_TokenHash",
                table: "circle_invites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_circle_join_requests_CircleId_UserId",
                table: "circle_join_requests",
                columns: new[] { "CircleId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_circle_join_requests_UserId",
                table: "circle_join_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_logs_CircleId",
                table: "circle_logs",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_members_CircleId_UserId",
                table: "circle_members",
                columns: new[] { "CircleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_circle_members_UserId",
                table: "circle_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_posts_AuthorUserId",
                table: "circle_posts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_circle_posts_CircleId",
                table: "circle_posts",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_circles_OwnerUserId",
                table: "circles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_life_areas_UserId",
                table: "life_areas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_life_entries_LifeAreaId",
                table: "life_entries",
                column: "LifeAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_life_entries_UserId",
                table: "life_entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_password_resets_TokenHash",
                table: "password_resets",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_resets_UserId",
                table: "password_resets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_reminders_UserId",
                table: "reminders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_shortcuts_UserId",
                table: "shortcuts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_deletion_requests");

            migrationBuilder.DropTable(
                name: "circle_invites");

            migrationBuilder.DropTable(
                name: "circle_join_requests");

            migrationBuilder.DropTable(
                name: "circle_logs");

            migrationBuilder.DropTable(
                name: "circle_members");

            migrationBuilder.DropTable(
                name: "circle_posts");

            migrationBuilder.DropTable(
                name: "life_entries");

            migrationBuilder.DropTable(
                name: "password_resets");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropTable(
                name: "sent_emails");

            migrationBuilder.DropTable(
                name: "shortcuts");

            migrationBuilder.DropTable(
                name: "circles");

            migrationBuilder.DropTable(
                name: "life_areas");

            migrationBuilder.DropColumn(
                name: "OwnedCircleCount",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "users");

            migrationBuilder.DropColumn(
                name: "StorageUsedBytes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VacationUntil",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "PhotoSizeBytes",
                table: "profiles");
        }
    }
}
