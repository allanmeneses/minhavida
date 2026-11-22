using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Circulos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rodada8Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingPeriod",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidUntil",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionProviderId",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionStatus",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "shortcuts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "shortcuts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "shared_list_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FindableByLocation",
                table: "profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FindableByLocation",
                table: "circles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "circles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "circles",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "living_profile_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LivingProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_living_profile_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_living_profile_grants_living_profiles_LivingProfileId",
                        column: x => x.LivingProfileId,
                        principalTable: "living_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "succession_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_succession_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_succession_votes_circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "succession_ballots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CastAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_succession_ballots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_succession_ballots_succession_votes_VoteId",
                        column: x => x.VoteId,
                        principalTable: "succession_votes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_events_Provider_EventKey",
                table: "billing_events",
                columns: new[] { "Provider", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_living_profile_grants_LivingProfileId_UserId",
                table: "living_profile_grants",
                columns: new[] { "LivingProfileId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_succession_ballots_VoteId_VoterUserId",
                table: "succession_ballots",
                columns: new[] { "VoteId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_succession_votes_CircleId",
                table: "succession_votes",
                column: "CircleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_events");

            migrationBuilder.DropTable(
                name: "living_profile_grants");

            migrationBuilder.DropTable(
                name: "succession_ballots");

            migrationBuilder.DropTable(
                name: "succession_votes");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PaidUntil",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SubscriptionProviderId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "shortcuts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "shortcuts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "shared_list_items");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "FindableByLocation",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "FindableByLocation",
                table: "circles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "circles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "circles");
        }
    }
}
