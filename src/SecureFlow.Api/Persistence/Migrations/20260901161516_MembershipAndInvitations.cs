using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MembershipAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationInvitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_CreatedBy",
                table: "OrganizationInvitations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitations_OrganizationId_CreatedAt",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationInvitations_PendingEmail",
                table: "OrganizationInvitations",
                columns: new[] { "OrganizationId", "Email" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL AND \"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationInvitations_TokenHash",
                table: "OrganizationInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationInvitations");
        }
    }
}
