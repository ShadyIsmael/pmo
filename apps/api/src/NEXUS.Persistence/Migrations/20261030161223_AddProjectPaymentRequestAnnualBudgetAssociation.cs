using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPaymentRequestAnnualBudgetAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnualBudgetFiscalYear",
                table: "ProjectPaymentRequestDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnualBudgetReference",
                table: "ProjectPaymentRequestDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectAnnualBudgetId",
                table: "ProjectPaymentRequestDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProjectAnnualBudgets_Tenant_Project_Id",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "ProjectId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_AnnualBudget",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_AnnualBudgetAssociation",
                table: "ProjectPaymentRequestDetails",
                sql: "([ProjectAnnualBudgetId] IS NULL AND [AnnualBudgetFiscalYear] IS NULL AND [AnnualBudgetReference] IS NULL) OR ([ProjectAnnualBudgetId] IS NOT NULL AND [AnnualBudgetFiscalYear] BETWEEN 1900 AND 2200 AND [AnnualBudgetReference] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectAnnualBudgets_TenantId_ProjectId_ProjectAnnualBudgetId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId" },
                principalTable: "ProjectAnnualBudgets",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectAnnualBudgets_TenantId_ProjectId_ProjectAnnualBudgetId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_AnnualBudget",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_AnnualBudgetAssociation",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProjectAnnualBudgets_Tenant_Project_Id",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropColumn(
                name: "AnnualBudgetFiscalYear",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "AnnualBudgetReference",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ProjectAnnualBudgetId",
                table: "ProjectPaymentRequestDetails");
        }
    }
}
