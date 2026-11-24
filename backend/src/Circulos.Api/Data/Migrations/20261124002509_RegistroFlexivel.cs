using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistroFlexivel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "record_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SchemaJson = table.Column<string>(type: "jsonb", nullable: false),
                    OriginTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginLifeAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_types_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric", nullable: true),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PhotoStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileStorageKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    FileContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReminderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginLifeEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginDiaryPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_records_record_types_TypeId",
                        column: x => x.TypeId,
                        principalTable: "record_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_records_reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_records_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_shares_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_record_shares_records_RecordId",
                        column: x => x.RecordId,
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_record_shares_CircleId",
                table: "record_shares",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_record_shares_RecordId_CircleId",
                table: "record_shares",
                columns: new[] { "RecordId", "CircleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_record_types_OriginLifeAreaId",
                table: "record_types",
                column: "OriginLifeAreaId",
                unique: true,
                filter: "\"OriginLifeAreaId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_record_types_UserId",
                table: "record_types",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_records_FieldsJson",
                table: "records",
                column: "FieldsJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_records_OccurredOn",
                table: "records",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_records_OriginDiaryPageId",
                table: "records",
                column: "OriginDiaryPageId",
                unique: true,
                filter: "\"OriginDiaryPageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_records_OriginDocumentId",
                table: "records",
                column: "OriginDocumentId",
                unique: true,
                filter: "\"OriginDocumentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_records_OriginLifeEntryId",
                table: "records",
                column: "OriginLifeEntryId",
                unique: true,
                filter: "\"OriginLifeEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_records_ReminderId",
                table: "records",
                column: "ReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_records_Title",
                table: "records",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_records_TypeId",
                table: "records",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_records_UserId",
                table: "records",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "record_shares");

            migrationBuilder.DropTable(
                name: "records");

            migrationBuilder.DropTable(
                name: "record_types");
        }
    }
}
