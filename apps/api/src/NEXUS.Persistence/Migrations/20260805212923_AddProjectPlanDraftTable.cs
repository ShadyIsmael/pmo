using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPlanDraftTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectPlanDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBaselineVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPlanDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_ProjectBaselineVersions_SourceBaselineVersionId",
                        column: x => x.SourceBaselineVersionId,
                        principalTable: "ProjectBaselineVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_ProjectChangeRequests_SubmittedChangeRequestId",
                        column: x => x.SubmittedChangeRequestId,
                        principalTable: "ProjectChangeRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectPlanDrafts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_ProjectId",
                table: "ProjectPlanDrafts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_SourceBaselineVersionId",
                table: "ProjectPlanDrafts",
                column: "SourceBaselineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_SubmittedChangeRequestId",
                table: "ProjectPlanDrafts",
                column: "SubmittedChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPlanDrafts_Tenant_Project_Active",
                table: "ProjectPlanDrafts",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "\"IsActive\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectPlanDrafts");
        }
    }
}
