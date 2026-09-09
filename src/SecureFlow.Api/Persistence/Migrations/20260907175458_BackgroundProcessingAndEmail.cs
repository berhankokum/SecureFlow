using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackgroundProcessingAndEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_OrganizationInvitations_PendingEmail",
                table: "OrganizationInvitations");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiredAt",
                table: "OrganizationInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationEmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEmailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationEmailDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing notifications predate FAZ 16 opt-in delivery. Marking them
            // skipped prevents a deployment-time historical email blast.
            migrationBuilder.Sql("""
                INSERT INTO "NotificationEmailDeliveries"
                    ("Id", "NotificationId", "Status", "AttemptCount", "CreatedAt")
                SELECT "Id", "Id", 'Skipped', 0, "CreatedAt"
                FROM "Notifications";
                """);

            // Preserve invitation history while immediately releasing expired rows
            // from the active pending uniqueness boundary.
            migrationBuilder.Sql("""
                UPDATE "OrganizationInvitations"
                SET "ExpiredAt" = "ExpiresAt"
                WHERE "AcceptedAt" IS NULL
                  AND "RevokedAt" IS NULL
                  AND "ExpiredAt" IS NULL
                  AND "ExpiresAt" <= CURRENT_TIMESTAMP;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationInvitations_PendingEmail",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "Email" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL AND \"ExpiredAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEmailDeliveries_Status_NextAttemptAt",
                table: "NotificationEmailDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationEmailDeliveries_NotificationId",
                table: "NotificationEmailDeliveries",
                column: "NotificationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationEmailDeliveries");

            migrationBuilder.DropIndex(
                name: "UX_OrganizationInvitations_PendingEmail",
                table: "OrganizationInvitations");

            // The old schema represented every unaccepted/unrevoked row as active.
            // Convert soft-expired rows before removing ExpiredAt so rollback cannot
            // violate the old pending-email unique index.
            migrationBuilder.Sql("""
                UPDATE "OrganizationInvitations"
                SET "RevokedAt" = COALESCE("ExpiredAt", "ExpiresAt")
                WHERE "AcceptedAt" IS NULL
                  AND "RevokedAt" IS NULL
                  AND "ExpiredAt" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "OrganizationInvitations");

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationInvitations_PendingEmail",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "Email" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL");
        }
    }
}
