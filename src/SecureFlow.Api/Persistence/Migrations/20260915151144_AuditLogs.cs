using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_Metadata", "jsonb_typeof(\"Metadata\") = 'object'");
                    table.CheckConstraint("CK_AuditLogs_Scope", "(\"OrganizationId\" IS NULL) = (\"Action\" IN ('LOGIN_FAILURE', 'PASSWORD_CHANGED'))");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_Timestamp_Id",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "Timestamp", "Id" });
            migrationBuilder.Sql("""
                CREATE FUNCTION secureflow_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLogs are append-only' USING ERRCODE = '42501';
                END;
                $$;
                CREATE TRIGGER audit_logs_immutable BEFORE UPDATE OR DELETE ON "AuditLogs"
                FOR EACH ROW EXECUTE FUNCTION secureflow_audit_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
            migrationBuilder.Sql("DROP FUNCTION secureflow_audit_immutable();");
        }
    }
}
