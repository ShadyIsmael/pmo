using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTenantScopedRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentNotificationDispatches_PaymentObligations_PaymentObligationId",
                table: "PaymentNotificationDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_PaymentObligationId",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSettlements_PaymentObligationId",
                table: "PaymentSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentNotificationDispatches_PaymentObligationId",
                table: "PaymentNotificationDispatches");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentObligations_TenantId_Id",
                table: "PaymentObligations",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentNotificationDispatches_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentNotificationDispatches",
                columns: new[] { "TenantId", "PaymentObligationId" },
                principalTable: "PaymentObligations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "PaymentObligationId" },
                principalTable: "PaymentObligations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentNotificationDispatches_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentNotificationDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId",
                table: "PaymentSettlements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentObligations_TenantId_Id",
                table: "PaymentObligations");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_PaymentObligationId",
                table: "PaymentSettlements",
                column: "PaymentObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotificationDispatches_PaymentObligationId",
                table: "PaymentNotificationDispatches",
                column: "PaymentObligationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentNotificationDispatches_PaymentObligations_PaymentObligationId",
                table: "PaymentNotificationDispatches",
                column: "PaymentObligationId",
                principalTable: "PaymentObligations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSettlements_PaymentObligations_PaymentObligationId",
                table: "PaymentSettlements",
                column: "PaymentObligationId",
                principalTable: "PaymentObligations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
