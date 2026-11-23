using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rodada14Posse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerLostAt",
                table: "circles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipNextCheckAt",
                table: "circles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ownership_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ownership_offers_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ownership_offers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ownership_offers_CircleId_UserId_Status",
                table: "ownership_offers",
                columns: new[] { "CircleId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ownership_offers_RoundId",
                table: "ownership_offers",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_offers_UserId",
                table: "ownership_offers",
                column: "UserId");

            migrationBuilder.Sql("""
                UPDATE circles
                SET "Status" = 0,
                    "OwnerLostAt" = COALESCE("OwnerLostAt", "CreatedAt"),
                    "OwnershipNextCheckAt" = NOW() AT TIME ZONE 'utc'
                WHERE "OwnerUserId" IS NULL AND "Status" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ownership_offers");

            migrationBuilder.DropColumn(
                name: "OwnerLostAt",
                table: "circles");

            migrationBuilder.DropColumn(
                name: "OwnershipNextCheckAt",
                table: "circles");
        }
    }
}
