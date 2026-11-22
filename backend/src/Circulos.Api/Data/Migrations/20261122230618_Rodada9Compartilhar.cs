using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rodada9Compartilhar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailRemindersEnabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEmailedFireAt",
                table: "reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "life_entries",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "life_entry_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LifeEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_life_entry_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_life_entry_shares_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_life_entry_shares_life_entries_LifeEntryId",
                        column: x => x.LifeEntryId,
                        principalTable: "life_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shortcut_change_notices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortcutId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shortcut_change_notices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shortcut_change_notices_shortcuts_ShortcutId",
                        column: x => x.ShortcutId,
                        principalTable: "shortcuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shortcut_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortcutId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shortcut_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shortcut_shares_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shortcut_shares_shortcuts_ShortcutId",
                        column: x => x.ShortcutId,
                        principalTable: "shortcuts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_life_entry_shares_CircleId",
                table: "life_entry_shares",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_life_entry_shares_LifeEntryId_CircleId",
                table: "life_entry_shares",
                columns: new[] { "LifeEntryId", "CircleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shortcut_change_notices_RecipientUserId",
                table: "shortcut_change_notices",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shortcut_change_notices_ShortcutId",
                table: "shortcut_change_notices",
                column: "ShortcutId");

            migrationBuilder.CreateIndex(
                name: "IX_shortcut_shares_CircleId",
                table: "shortcut_shares",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_shortcut_shares_ShortcutId_CircleId",
                table: "shortcut_shares",
                columns: new[] { "ShortcutId", "CircleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "life_entry_shares");

            migrationBuilder.DropTable(
                name: "shortcut_change_notices");

            migrationBuilder.DropTable(
                name: "shortcut_shares");

            migrationBuilder.DropColumn(
                name: "EmailRemindersEnabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastEmailedFireAt",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "life_entries");
        }
    }
}
