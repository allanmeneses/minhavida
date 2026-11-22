using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ListasCompartilhadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_lists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    MemberAccess = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_lists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_lists_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_list_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_list_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_list_grants_shared_lists_ListId",
                        column: x => x.ListId,
                        principalTable: "shared_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_list_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DoneByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_list_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_list_items_shared_lists_ListId",
                        column: x => x.ListId,
                        principalTable: "shared_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_list_grants_ListId_UserId",
                table: "shared_list_grants",
                columns: new[] { "ListId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_list_items_ListId",
                table: "shared_list_items",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_lists_CircleId",
                table: "shared_lists",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_lists_OwnerUserId",
                table: "shared_lists",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_list_grants");

            migrationBuilder.DropTable(
                name: "shared_list_items");

            migrationBuilder.DropTable(
                name: "shared_lists");
        }
    }
}
