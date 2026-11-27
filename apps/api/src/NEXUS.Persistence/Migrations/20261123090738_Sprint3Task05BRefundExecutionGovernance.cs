using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3Task05BRefundExecutionGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreatedFromQuickOneOff",
                table: "ProjectPaymentRequestDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalRequestId",
                table: "PaymentSettlementAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "PaymentSettlementAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "PaymentSettlementAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedApproverId",
                table: "PaymentSettlementAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionConfirmedAtUtc",
                table: "PaymentSettlementAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionConfirmedBy",
                table: "PaymentSettlementAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionDate",
                table: "PaymentSettlementAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionIdempotencyKey",
                table: "PaymentSettlementAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PaymentSettlementAdjustments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Executed");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_ApprovalRequestId",
                table: "PaymentSettlementAdjustments",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                unique: true,
                filter: "[ApprovalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_ExecutionIdempotencyKey",
                table: "PaymentSettlementAdjustments",
                columns: new[] { "TenantId", "ExecutionIdempotencyKey" },
                unique: true,
                filter: "[ExecutionIdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentSettlementAdjustments_Status",
                table: "PaymentSettlementAdjustments",
                sql: "[Status] IN (N'PendingApproval', N'Approved', N'Rejected', N'Executed')");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlementAdjustments_ApprovalRequests_TenantId_ApprovalRequestId",
                table: "PaymentSettlementAdjustments",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                principalTable: "ApprovalRequests",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlementAdjustments_ApprovalRequests_TenantId_ApprovalRequestId",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_ApprovalRequestId",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_ExecutionIdempotencyKey",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentSettlementAdjustments_Status",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedFromQuickOneOff",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "AssignedApproverId",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ExecutionConfirmedAtUtc",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ExecutionConfirmedBy",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ExecutionDate",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "ExecutionIdempotencyKey",
                table: "PaymentSettlementAdjustments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PaymentSettlementAdjustments");
        }
    }
}
