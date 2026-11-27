using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTask02PaymentRequestApprovalIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Approver",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Position",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequestApproverAssignments_State",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApprovalCycleNumber",
                table: "ProjectPaymentRequestDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalCycleId",
                table: "PaymentRequestApproverAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PaymentRequestApproverAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectAnnualBudgetLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorizedLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_ProjectAnnualBudgetLines", x => x.Id);
                    table.UniqueConstraint("AK_ProjectAnnualBudgetLines_Tenant_Project_Budget_Id", x => new { x.TenantId, x.ProjectId, x.ProjectAnnualBudgetId, x.Id });
                    table.CheckConstraint("CK_ProjectAnnualBudgetLines_AuthorizedLimit", "[AuthorizedLimit] > 0");
                    table.CheckConstraint("CK_ProjectAnnualBudgetLines_Description", "LEN(LTRIM(RTRIM([Description]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetLines_ProjectAnnualBudgets_TenantId_ProjectId_ProjectAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.ProjectAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetLines_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentRequestApprovalCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    ApprovalMode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PlannedPaymentInstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    AnnualBudgetReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProjectAnnualBudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnualBudgetLineReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnnualBudgetLineDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmitIdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApproversSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectPaymentRequestApprovalCycles", x => x.Id);
                    table.UniqueConstraint("AK_ProjectPaymentRequestApprovalCycles_Tenant_Project_Request_Id", x => new { x.TenantId, x.ProjectId, x.ApprovalRequestId, x.Id });
                    table.CheckConstraint("CK_ProjectPaymentRequestApprovalCycles_Amounts", "[Amount] > 0 AND [FiscalYear] BETWEEN 1900 AND 2200 AND [Currency] <> N''");
                    table.CheckConstraint("CK_ProjectPaymentRequestApprovalCycles_ApprovalMode", "[ApprovalMode] IN (N'Sequential', N'Parallel')");
                    table.CheckConstraint("CK_ProjectPaymentRequestApprovalCycles_CycleNumber", "[CycleNumber] > 0");
                    table.CheckConstraint("CK_ProjectPaymentRequestApprovalCycles_Status", "[Status] IN (N'Pending', N'Approved', N'Rejected', N'ReturnedForRevision', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestApprovalCycles_PlannedPaymentInstallments_TenantId_ProjectId_PlannedPaymentInstallmentId",
                        columns: x => new { x.TenantId, x.ProjectId, x.PlannedPaymentInstallmentId },
                        principalTable: "PlannedPaymentInstallments",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestApprovalCycles_ProjectAnnualBudgetLines_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLin~",
                        columns: x => new { x.TenantId, x.ProjectId, x.ProjectAnnualBudgetId, x.ProjectAnnualBudgetLineId },
                        principalTable: "ProjectAnnualBudgetLines",
                        principalColumns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestApprovalCycles_ProjectAnnualBudgets_TenantId_ProjectId_ProjectAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.ProjectAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestApprovalCycles_ProjectPaymentRequestDetails_TenantId_ProjectId_ApprovalRequestId",
                        columns: x => new { x.TenantId, x.ProjectId, x.ApprovalRequestId },
                        principalTable: "ProjectPaymentRequestDetails",
                        principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ApprovalRequestId_CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "CurrentApprovalCycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "ProjectAnnualBudgetLineId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_AnnualBudgetLineAssociation",
                table: "ProjectPaymentRequestDetails",
                sql: "[ProjectAnnualBudgetLineId] IS NULL OR [ProjectAnnualBudgetId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ApprovalCycleNumber",
                table: "ProjectPaymentRequestDetails",
                sql: "([CurrentApprovalCycleId] IS NULL AND [CurrentApprovalCycleNumber] = 0) OR ([CurrentApprovalCycleId] IS NOT NULL AND [CurrentApprovalCycleNumber] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestApproverAssignments_TenantId_ProjectId_ApprovalRequestId_ApprovalCycleId",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "ApprovalCycleId" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Cycle_Approver",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApprovalCycleId", "ApproverUserId" },
                unique: true,
                filter: "[ApprovalCycleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Cycle_Position",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApprovalCycleId", "Position" },
                unique: true,
                filter: "[Position] IS NOT NULL AND [ApprovalCycleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Approver",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApproverUserId" },
                unique: true,
                filter: "[ApprovalCycleId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Position",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "Position" },
                unique: true,
                filter: "[Position] IS NOT NULL AND [ApprovalCycleId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequestApproverAssignments_State",
                table: "PaymentRequestApproverAssignments",
                sql: "[State] IN (N'Pending', N'Approved', N'Rejected', N'ReturnedForRevision', N'Closed')");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetLines_Tenant_Project_Budget_Active",
                table: "ProjectAnnualBudgetLines",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgetLines_Tenant_Reference",
                table: "ProjectAnnualBudgetLines",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestApprovalCycles_Request_Status_Submitted",
                table: "ProjectPaymentRequestApprovalCycles",
                columns: new[] { "TenantId", "ApprovalRequestId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_PlannedPaymentInstallmentId",
                table: "ProjectPaymentRequestApprovalCycles",
                columns: new[] { "TenantId", "ProjectId", "PlannedPaymentInstallmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestApprovalCycles",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "ProjectAnnualBudgetLineId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestApprovalCycles_Project_Idempotency",
                table: "ProjectPaymentRequestApprovalCycles",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "SubmitIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestApprovalCycles_Request_Cycle",
                table: "ProjectPaymentRequestApprovalCycles",
                columns: new[] { "TenantId", "ApprovalRequestId", "CycleNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequestApproverAssignments_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_ApprovalRequestId_ApprovalCycleId",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "ApprovalCycleId" },
                principalTable: "ProjectPaymentRequestApprovalCycles",
                principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectAnnualBudgetLines_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "ProjectAnnualBudgetLineId" },
                principalTable: "ProjectAnnualBudgetLines",
                principalColumns: new[] { "TenantId", "ProjectId", "ProjectAnnualBudgetId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_ApprovalRequestId_CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "CurrentApprovalCycleId" },
                principalTable: "ProjectPaymentRequestApprovalCycles",
                principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequestApproverAssignments_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_ApprovalRequestId_ApprovalCycleId",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectAnnualBudgetLines_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestApprovalCycles_TenantId_ProjectId_ApprovalRequestId_CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropTable(
                name: "ProjectPaymentRequestApprovalCycles");

            migrationBuilder.DropTable(
                name: "ProjectAnnualBudgetLines");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ApprovalRequestId_CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ProjectAnnualBudgetId_ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_AnnualBudgetLineAssociation",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ApprovalCycleNumber",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequestApproverAssignments_TenantId_ProjectId_ApprovalRequestId_ApprovalCycleId",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Cycle_Approver",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Cycle_Position",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Approver",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Position",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequestApproverAssignments_State",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropColumn(
                name: "CurrentApprovalCycleId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "CurrentApprovalCycleNumber",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ProjectAnnualBudgetLineId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ApprovalCycleId",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PaymentRequestApproverAssignments");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Approver",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApproverUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentRequestApproverAssignments_Request_Position",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApprovalRequestId", "Position" },
                unique: true,
                filter: "[Position] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequestApproverAssignments_State",
                table: "PaymentRequestApproverAssignments",
                sql: "[State] IN (N'Pending', N'Approved', N'Rejected')");
        }
    }
}
