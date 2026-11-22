using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MemoriasCalendarioObservabilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarFeedToken",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "living_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_living_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_living_profiles_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_living_profiles_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_memories_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memories_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routine_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    Curated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routine_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_CalendarFeedToken",
                table: "users",
                column: "CalendarFeedToken",
                unique: true,
                filter: "\"CalendarFeedToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_living_profiles_CircleId",
                table: "living_profiles",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_living_profiles_OwnerUserId",
                table: "living_profiles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_memories_AuthorUserId",
                table: "memories",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_memories_AuthorUserId_SourceCircleId",
                table: "memories",
                columns: new[] { "AuthorUserId", "SourceCircleId" },
                unique: true,
                filter: "\"SourceCircleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_memories_CircleId",
                table: "memories",
                column: "CircleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "living_profiles");

            migrationBuilder.DropTable(
                name: "memories");

            migrationBuilder.DropTable(
                name: "routine_templates");

            migrationBuilder.DropIndex(
                name: "IX_users_CalendarFeedToken",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CalendarFeedToken",
                table: "users");
        }
    }
}
