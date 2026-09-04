using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowAndKanban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStateId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workflows_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStates", x => x.Id);
                    table.UniqueConstraint("AK_WorkflowStates_WorkflowId_Id", x => new { x.WorkflowId, x.Id });
                    table.CheckConstraint("CK_WorkflowStates_Category", "\"Category\" IN ('TODO', 'IN_PROGRESS', 'DONE')");
                    table.CheckConstraint("CK_WorkflowStates_Position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_WorkflowStates_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredPermission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.CheckConstraint("CK_WorkflowTransitions_DifferentStates", "\"FromStateId\" <> \"ToStateId\"");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStates_WorkflowId_FromStateId",
                        columns: x => new { x.WorkflowId, x.FromStateId },
                        principalTable: "WorkflowStates",
                        principalColumns: new[] { "WorkflowId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStates_WorkflowId_ToStateId",
                        columns: x => new { x.WorkflowId, x.ToStateId },
                        principalTable: "WorkflowStates",
                        principalColumns: new[] { "WorkflowId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_WorkflowStateId",
                table: "WorkItems",
                columns: new[] { "ProjectId", "WorkflowStateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_WorkflowStateId",
                table: "WorkItems",
                column: "WorkflowStateId");

            migrationBuilder.CreateIndex(
                name: "UX_Workflows_OrganizationId",
                table: "Workflows",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowStates_WorkflowId_Position",
                table: "WorkflowStates",
                columns: new[] { "WorkflowId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowId_ToStateId",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowId", "ToStateId" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowTransitions_WorkflowId_FromStateId_ToStateId",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowId", "FromStateId", "ToStateId" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "Workflows" ("Id", "OrganizationId", "Name", "CreatedAt")
                SELECT gen_random_uuid(), organization."Id", 'DEFAULT', organization."CreatedAt"
                FROM "Organizations" AS organization
                ON CONFLICT ("OrganizationId") DO NOTHING;

                INSERT INTO "WorkflowStates" ("Id", "WorkflowId", "Name", "Category", "Position")
                SELECT gen_random_uuid(), workflow."Id", definition."Name", definition."Category", definition."Position"
                FROM "Workflows" AS workflow
                CROSS JOIN (VALUES
                    ('BACKLOG', 'TODO', 0),
                    ('TODO', 'TODO', 1),
                    ('IN_PROGRESS', 'IN_PROGRESS', 2),
                    ('IN_REVIEW', 'IN_PROGRESS', 3),
                    ('DONE', 'DONE', 4)
                ) AS definition("Name", "Category", "Position")
                ON CONFLICT ("WorkflowId", "Position") DO NOTHING;

                INSERT INTO "WorkflowTransitions"
                    ("Id", "WorkflowId", "FromStateId", "ToStateId", "RequiredPermission")
                SELECT gen_random_uuid(), workflow."Id", source."Id", target."Id", definition."RequiredPermission"
                FROM "Workflows" AS workflow
                CROSS JOIN (VALUES
                    ('BACKLOG', 'TODO', NULL::character varying),
                    ('TODO', 'IN_PROGRESS', NULL::character varying),
                    ('IN_PROGRESS', 'IN_REVIEW', NULL::character varying),
                    ('IN_REVIEW', 'DONE', 'workitem.approve'::character varying)
                ) AS definition("FromName", "ToName", "RequiredPermission")
                JOIN "WorkflowStates" AS source
                    ON source."WorkflowId" = workflow."Id" AND source."Name" = definition."FromName"
                JOIN "WorkflowStates" AS target
                    ON target."WorkflowId" = workflow."Id" AND target."Name" = definition."ToName"
                ON CONFLICT ("WorkflowId", "FromStateId", "ToStateId") DO NOTHING;

                UPDATE "WorkItems" AS work_item
                SET "WorkflowStateId" = initial_state."Id"
                FROM "Projects" AS project
                JOIN "Workflows" AS workflow
                    ON workflow."OrganizationId" = project."OrganizationId"
                JOIN "WorkflowStates" AS initial_state
                    ON initial_state."WorkflowId" = workflow."Id" AND initial_state."Position" = 0
                WHERE work_item."ProjectId" = project."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowStateId",
                table: "WorkItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkflowStates_WorkflowStateId",
                table: "WorkItems",
                column: "WorkflowStateId",
                principalTable: "WorkflowStates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkflowStates_WorkflowStateId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "WorkflowStates");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ProjectId_WorkflowStateId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_WorkflowStateId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "WorkflowStateId",
                table: "WorkItems");
        }
    }
}
