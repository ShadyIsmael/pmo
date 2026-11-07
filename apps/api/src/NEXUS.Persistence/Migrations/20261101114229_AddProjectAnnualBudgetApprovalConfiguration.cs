using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAnnualBudgetApprovalConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAnnualBudgetApprovalConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalMode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
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
                    table.PrimaryKey("PK_ProjectAnnualBudgetApprovalConfigurations", x => x.Id);
                    table.UniqueConstraint("AK_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project_Id", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_ProjectAnnualBudgetApprovalConfigurations_Mode", "[ApprovalMode] IN (N'Sequential', N'Parallel')");
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetApprovalConfigurations_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAnnualBudgetApprovalApprovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ProjectAnnualBudgetApprovalApprovers", x => x.Id);
                    table.CheckConstraint("CK_ProjectAnnualBudgetApprovalApprovers_Position", "[Position] IS NULL OR [Position] > 0");
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetApprovalApprovers_ProjectAnnualBudgetApprovalConfigurations_TenantId_ProjectId_ConfigurationId",
                        columns: x => new { x.TenantId, x.ProjectId, x.ConfigurationId },
                        principalTable: "ProjectAnnualBudgetApprovalConfigurations",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetApprovalApprovers_Users_TenantId_ApproverUserId",
                        columns: x => new { x.TenantId, x.ApproverUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetApprovalApprovers_Configuration_Position",
                table: "ProjectAnnualBudgetApprovalApprovers",
                columns: new[] { "TenantId", "ConfigurationId", "Position" },
                filter: "[IsDeleted] = 0 AND [Position] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetApprovalApprovers_TenantId_ApproverUserId",
                table: "ProjectAnnualBudgetApprovalApprovers",
                columns: new[] { "TenantId", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetApprovalApprovers_TenantId_ProjectId_ConfigurationId",
                table: "ProjectAnnualBudgetApprovalApprovers",
                columns: new[] { "TenantId", "ProjectId", "ConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgetApprovalApprovers_Configuration_User",
                table: "ProjectAnnualBudgetApprovalApprovers",
                columns: new[] { "TenantId", "ConfigurationId", "ApproverUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project",
                table: "ProjectAnnualBudgetApprovalConfigurations",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAnnualBudgetApprovalApprovers");

            migrationBuilder.DropTable(
                name: "ProjectAnnualBudgetApprovalConfigurations");
        }
    }
}
