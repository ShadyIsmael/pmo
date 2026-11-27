using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTask03PaymentSettlementInstallmentLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualPaid",
                table: "PlannedPaymentInstallments",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PlannedPaymentInstallmentId",
                table: "PaymentSettlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_PlannedPaymentInstallmentId",
                table: "PaymentSettlements",
                column: "PlannedPaymentInstallmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_PlannedPaymentInstallments_PlannedPaymentInstallmentId",
                table: "PaymentSettlements",
                column: "PlannedPaymentInstallmentId",
                principalTable: "PlannedPaymentInstallments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_PlannedPaymentInstallments_PlannedPaymentInstallmentId",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_PlannedPaymentInstallmentId",
                table: "PaymentSettlements");

            migrationBuilder.DropColumn(
                name: "ActualPaid",
                table: "PlannedPaymentInstallments");

            migrationBuilder.DropColumn(
                name: "PlannedPaymentInstallmentId",
                table: "PaymentSettlements");
        }
    }
}
