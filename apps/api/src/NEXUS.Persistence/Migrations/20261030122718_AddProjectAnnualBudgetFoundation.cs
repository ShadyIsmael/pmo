using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAnnualBudgetFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAnnualBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    FiscalYearStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FiscalYearEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FinancialReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    NewApprovedFunding = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedCarryForward = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedAdjustments = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectAnnualBudgets", x => x.Id);
                    table.CheckConstraint("CK_ProjectAnnualBudgets_Amounts", "[NewApprovedFunding] >= 0 AND [ApprovedCarryForward] >= 0 AND ([NewApprovedFunding] + [ApprovedCarryForward] + [ApprovedAdjustments]) >= 0");
                    table.CheckConstraint("CK_ProjectAnnualBudgets_FiscalPeriod", "[FiscalYear] BETWEEN 1900 AND 2200 AND [FiscalYearStartDate] <= [FiscalYearEndDate]");
                    table.CheckConstraint("CK_ProjectAnnualBudgets_Status", "[Status] IN (N'Draft', N'Approved')");
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgets_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgets_Tenant_FiscalYear",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "FiscalYear" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgets_Tenant_FinancialReference",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "FinancialReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgets_Tenant_Project_FiscalYear",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "ProjectId", "FiscalYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAnnualBudgets");
        }
    }
}
