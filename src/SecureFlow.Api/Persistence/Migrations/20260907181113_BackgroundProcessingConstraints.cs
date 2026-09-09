using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackgroundProcessingConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_DueDate_Active",
                table: "WorkItems",
                column: "DueDate",
                filter: "\"DueDate\" IS NOT NULL AND \"ArchivedAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationInvitations_ExpiredAt",
                table: "OrganizationInvitations",
                sql: "\"ExpiredAt\" IS NULL OR \"ExpiredAt\" >= \"ExpiresAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationInvitations_TerminalState",
                table: "OrganizationInvitations",
                sql: "((\"AcceptedAt\" IS NOT NULL)::int + (\"RevokedAt\" IS NOT NULL)::int + (\"ExpiredAt\" IS NOT NULL)::int) <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationEmailDeliveries_AttemptCount",
                table: "NotificationEmailDeliveries",
                sql: "\"AttemptCount\" BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationEmailDeliveries_Identity",
                table: "NotificationEmailDeliveries",
                sql: "\"Id\" = \"NotificationId\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationEmailDeliveries_State",
                table: "NotificationEmailDeliveries",
                sql: "(\"Status\" = 'Pending' AND \"NextAttemptAt\" IS NOT NULL AND \"SentAt\" IS NULL) OR (\"Status\" = 'Sent' AND \"NextAttemptAt\" IS NULL AND \"SentAt\" IS NOT NULL) OR (\"Status\" IN ('Failed', 'Skipped') AND \"NextAttemptAt\" IS NULL AND \"SentAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationEmailDeliveries_Status",
                table: "NotificationEmailDeliveries",
                sql: "\"Status\" IN ('Pending', 'Sent', 'Failed', 'Skipped')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_DueDate_Active",
                table: "WorkItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationInvitations_ExpiredAt",
                table: "OrganizationInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationInvitations_TerminalState",
                table: "OrganizationInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationEmailDeliveries_AttemptCount",
                table: "NotificationEmailDeliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationEmailDeliveries_Identity",
                table: "NotificationEmailDeliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationEmailDeliveries_State",
                table: "NotificationEmailDeliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationEmailDeliveries_Status",
                table: "NotificationEmailDeliveries");
        }
    }
}
