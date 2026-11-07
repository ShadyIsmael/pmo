using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialPersistenceSprints3to6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ReversalReference",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectAnnualBudgets_Amounts",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_TenantId_PaymentObligationId_SettlementDate",
                table: "PaymentSettlements");

            migrationBuilder.AddColumn<Guid>(
                name: "FundingTransferId",
                table: "ProjectPaymentRequestDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedReservedCarryForward",
                table: "ProjectAnnualBudgets",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "ProjectAnnualBudgets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "ProjectAnnualBudgets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByClosingId",
                table: "ProjectAnnualBudgets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentObligationId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PaymentSettlements",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PaymentSettlements",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingTransferId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectPaymentRequestId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProjectPaymentRequestDetails_Tenant_Project_Request",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ApprovalRequestId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ApprovalRequestId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentObligations_Tenant_Project_Id",
                table: "PaymentObligations",
                columns: new[] { "TenantId", "ProjectId", "Id" });

            migrationBuilder.CreateTable(
                name: "ProjectAnnualBudgetClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    FiscalYearStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FiscalYearEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DestinationAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationFiscalYear = table.Column<int>(type: "int", nullable: true),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ApprovalMode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedAnnualBudget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    NewFunding = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IncomingCarryForward = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedAdjustments = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedPaymentRequestAllocations = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    ApprovedReversalReleases = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    EffectiveActiveAllocations = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    ActualSettlementsInFiscalYear = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OutstandingCommitments = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    FreeAvailableBudget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalUnspentBudget = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ProposedFreeCarryForward = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CarryOutstandingCommitments = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedFreeCarryForward = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ApprovedReservedCarryForward = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    NonCarriedFreeBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    NextFiscalYearNewFunding = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    NextFiscalYearAdjustments = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PendingPaymentRequestCount = table.Column<int>(type: "int", nullable: false),
                    PendingReversalRequestCount = table.Column<int>(type: "int", nullable: false),
                    UnresolvedLegacyRequestCount = table.Column<int>(type: "int", nullable: false),
                    IntegrityIssueCount = table.Column<int>(type: "int", nullable: false),
                    IntegrityIssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectAnnualBudgetClosings", x => x.Id);
                    table.UniqueConstraint("AK_ProjectAnnualBudgetClosings_Tenant_Project_Id", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_ProjectAnnualBudgetClosings_CarryForward", "[ProposedFreeCarryForward] >= 0 AND [ApprovedFreeCarryForward] >= 0 AND [ApprovedReservedCarryForward] >= 0 AND [NonCarriedFreeBalance] >= 0");
                    table.CheckConstraint("CK_ProjectAnnualBudgetClosings_FiscalPeriod", "[FiscalYear] BETWEEN 1900 AND 2200 AND [FiscalYearStartDate] <= [FiscalYearEndDate]");
                    table.CheckConstraint("CK_ProjectAnnualBudgetClosings_Status", "[Status] IN (N'Review', N'PendingApproval', N'Rejected', N'Closed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetClosings_ApprovalRequests_TenantId_ApprovalRequestId",
                        columns: x => new { x.TenantId, x.ApprovalRequestId },
                        principalTable: "ApprovalRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetClosings_ProjectAnnualBudgets_TenantId_ProjectId_DestinationAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.DestinationAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetClosings_ProjectAnnualBudgets_TenantId_ProjectId_SourceAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.SourceAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectAnnualBudgetClosings_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentRequestFundingTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFiscalYear = table.Column<int>(type: "int", nullable: false),
                    DestinationAnnualBudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationFiscalYear = table.Column<int>(type: "int", nullable: false),
                    ClosingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesFundingTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProjectPaymentRequestFundingTransfers", x => x.Id);
                    table.UniqueConstraint("AK_ProjectPaymentRequestFundingTransfers_Tenant_Project_Id", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_ProjectPaymentRequestFundingTransfers_Amount", "[Amount] > 0 AND [SourceFiscalYear] BETWEEN 1900 AND 2199 AND [DestinationFiscalYear] = [SourceFiscalYear] + 1 AND [Currency] <> N''");
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgetClosings_TenantId_ProjectId_ClosingId",
                        columns: x => new { x.TenantId, x.ProjectId, x.ClosingId },
                        principalTable: "ProjectAnnualBudgetClosings",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgets_TenantId_ProjectId_DestinationAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.DestinationAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgets_TenantId_ProjectId_SourceAnnualBudgetId",
                        columns: x => new { x.TenantId, x.ProjectId, x.SourceAnnualBudgetId },
                        principalTable: "ProjectAnnualBudgets",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_ProjectPaymentRequestDetails_TenantId_ProjectId_PaymentRequestId",
                        columns: x => new { x.TenantId, x.ProjectId, x.PaymentRequestId },
                        principalTable: "ProjectPaymentRequestDetails",
                        principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_SupersedesFundingTransferId",
                        columns: x => new { x.TenantId, x.ProjectId, x.SupersedesFundingTransferId },
                        principalTable: "ProjectPaymentRequestFundingTransfers",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestFundingTransfers_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_FundingTransferId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "FundingTransferId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ReversesApprovalRequestId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ReversalReference",
                table: "ProjectPaymentRequestDetails",
                sql: "([RequestKind] = N'PaymentRequest' AND [ReversesApprovalRequestId] IS NULL) OR ([RequestKind] = N'ReversalRequest' AND [ReversesApprovalRequestId] IS NOT NULL AND [ReversesApprovalRequestId] <> [ApprovalRequestId])");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgets_TenantId_ProjectId_ClosedByClosingId",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "ProjectId", "ClosedByClosingId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectAnnualBudgets_Amounts",
                table: "ProjectAnnualBudgets",
                sql: "[NewApprovedFunding] >= 0 AND [ApprovedCarryForward] >= 0 AND [ApprovedReservedCarryForward] >= 0 AND [ApprovedReservedCarryForward] <= [ApprovedCarryForward] AND ([NewApprovedFunding] + [ApprovedCarryForward] + [ApprovedAdjustments]) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectAnnualBudgets_ClosingMetadata",
                table: "ProjectAnnualBudgets",
                sql: "([ClosedAtUtc] IS NULL AND [ClosedByClosingId] IS NULL AND [ClosedBy] IS NULL) OR ([ClosedAtUtc] IS NOT NULL AND [ClosedByClosingId] IS NOT NULL AND [ClosedBy] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_FundingTransferId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "FundingTransferId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_PaymentObligationId_SettlementDate",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "PaymentObligationId", "SettlementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_ProjectPaymentRequestId_SettlementDate",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "ProjectPaymentRequestId", "SettlementDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentSettlements_PaymentRequest_Reference",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "ProjectPaymentRequestId", "PaymentReference" },
                unique: true,
                filter: "[ProjectPaymentRequestId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentSettlements_ExactlyOneOwner",
                table: "PaymentSettlements",
                sql: "([PaymentObligationId] IS NOT NULL AND [ProjectPaymentRequestId] IS NULL) OR ([PaymentObligationId] IS NULL AND [ProjectPaymentRequestId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentSettlements_FundingTransferOwner",
                table: "PaymentSettlements",
                sql: "[FundingTransferId] IS NULL OR ([ProjectPaymentRequestId] IS NOT NULL AND [PaymentObligationId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentSettlements_RequestCurrencyDescription",
                table: "PaymentSettlements",
                sql: "([ProjectPaymentRequestId] IS NULL) OR ([Currency] IS NOT NULL AND LEN([Currency]) = 3 AND [Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetClosings_Project_FiscalYear_Status",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ProjectId", "FiscalYear", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetClosings_TenantId_ApprovalRequestId",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ApprovalRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetClosings_TenantId_ProjectId_DestinationAnnualBudgetId",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ProjectId", "DestinationAnnualBudgetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnnualBudgetClosings_TenantId_ProjectId_SourceAnnualBudgetId",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ProjectId", "SourceAnnualBudgetId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgetClosings_Idempotency",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ProjectId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectAnnualBudgetClosings_Pending_Project_FiscalYear",
                table: "ProjectAnnualBudgetClosings",
                columns: new[] { "TenantId", "ProjectId", "FiscalYear" },
                unique: true,
                filter: "[Status] = N'PendingApproval'");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestFundingTransfers_DestinationBudget_Request",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "ProjectId", "DestinationAnnualBudgetId", "PaymentRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestFundingTransfers_SourceBudget_Request",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "ProjectId", "SourceAnnualBudgetId", "PaymentRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_ClosingId",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "ProjectId", "ClosingId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestFundingTransfers_ActiveChainSuccessor",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "ProjectId", "SupersedesFundingTransferId" },
                unique: true,
                filter: "[SupersedesFundingTransferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestFundingTransfers_Idempotency",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestFundingTransfers_Request_DestinationFiscalYear",
                table: "ProjectPaymentRequestFundingTransfers",
                columns: new[] { "TenantId", "ProjectId", "PaymentRequestId", "DestinationFiscalYear" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_ProjectId_PaymentObligationId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "PaymentObligationId" },
                principalTable: "PaymentObligations",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_ProjectPaymentRequestDetails_TenantId_ProjectId_ProjectPaymentRequestId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "ProjectPaymentRequestId" },
                principalTable: "ProjectPaymentRequestDetails",
                principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "FundingTransferId" },
                principalTable: "ProjectPaymentRequestFundingTransfers",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectAnnualBudgets_ProjectAnnualBudgetClosings_TenantId_ProjectId_ClosedByClosingId",
                table: "ProjectAnnualBudgets",
                columns: new[] { "TenantId", "ProjectId", "ClosedByClosingId" },
                principalTable: "ProjectAnnualBudgetClosings",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "ReversesApprovalRequestId" },
                principalTable: "ProjectPaymentRequestDetails",
                principalColumns: new[] { "TenantId", "ProjectId", "ApprovalRequestId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "FundingTransferId" },
                principalTable: "ProjectPaymentRequestFundingTransfers",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_ProjectId_PaymentObligationId",
                table: "PaymentSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_ProjectPaymentRequestDetails_TenantId_ProjectId_ProjectPaymentRequestId",
                table: "PaymentSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId",
                table: "PaymentSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectAnnualBudgets_ProjectAnnualBudgetClosings_TenantId_ProjectId_ClosedByClosingId",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropTable(
                name: "ProjectPaymentRequestFundingTransfers");

            migrationBuilder.DropTable(
                name: "ProjectAnnualBudgetClosings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProjectPaymentRequestDetails_Tenant_Project_Request",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_FundingTransferId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ReversalReference",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProjectAnnualBudgets_TenantId_ProjectId_ClosedByClosingId",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectAnnualBudgets_Amounts",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectAnnualBudgets_ClosingMetadata",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_FundingTransferId",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_PaymentObligationId_SettlementDate",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_TenantId_ProjectId_ProjectPaymentRequestId_SettlementDate",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "UX_PaymentSettlements_PaymentRequest_Reference",
                table: "PaymentSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentSettlements_ExactlyOneOwner",
                table: "PaymentSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentSettlements_FundingTransferOwner",
                table: "PaymentSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentSettlements_RequestCurrencyDescription",
                table: "PaymentSettlements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentObligations_Tenant_Project_Id",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "FundingTransferId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ApprovedReservedCarryForward",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropColumn(
                name: "ClosedByClosingId",
                table: "ProjectAnnualBudgets");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PaymentSettlements");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PaymentSettlements");

            migrationBuilder.DropColumn(
                name: "FundingTransferId",
                table: "PaymentSettlements");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PaymentSettlements");

            migrationBuilder.DropColumn(
                name: "ProjectPaymentRequestId",
                table: "PaymentSettlements");

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentObligationId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPaymentRequestDetails_ReversalReference",
                table: "ProjectPaymentRequestDetails",
                sql: "([RequestKind] = N'PaymentRequest' AND [ReversesApprovalRequestId] IS NULL) OR ([RequestKind] = N'ReversalRequest' AND [ReversesApprovalRequestId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectAnnualBudgets_Amounts",
                table: "ProjectAnnualBudgets",
                sql: "[NewApprovedFunding] >= 0 AND [ApprovedCarryForward] >= 0 AND ([NewApprovedFunding] + [ApprovedCarryForward] + [ApprovedAdjustments]) >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_PaymentObligationId_SettlementDate",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "PaymentObligationId", "SettlementDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "PaymentObligationId" },
                principalTable: "PaymentObligations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ReversesApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ReversesApprovalRequestId" },
                principalTable: "ApprovalRequests",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
