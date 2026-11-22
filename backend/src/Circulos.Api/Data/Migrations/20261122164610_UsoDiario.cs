using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UsoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReminderId",
                table: "shortcuts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "search_queries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Term = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimesUsed = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_queries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_search_queries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shortcuts_ReminderId",
                table: "shortcuts",
                column: "ReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_search_queries_UserId_Term",
                table: "search_queries",
                columns: new[] { "UserId", "Term" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_shortcuts_reminders_ReminderId",
                table: "shortcuts",
                column: "ReminderId",
                principalTable: "reminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shortcuts_reminders_ReminderId",
                table: "shortcuts");

            migrationBuilder.DropTable(
                name: "search_queries");

            migrationBuilder.DropIndex(
                name: "IX_shortcuts_ReminderId",
                table: "shortcuts");

            migrationBuilder.DropColumn(
                name: "ReminderId",
                table: "shortcuts");
        }
    }
}
