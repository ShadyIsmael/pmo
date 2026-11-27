using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTask06SettlementAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_PaymentSettlements_TenantId_ProjectId_Id",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "ProjectId", "Id" });

            migrationBuilder.CreateTable(
                name: "PaymentSettlementAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalSettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectPaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedPaymentInstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjustmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplacementSettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSettlementAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentSettlementAdjustments_PaymentSettlements_ReplacementSettlementId",
                        column: x => x.ReplacementSettlementId,
                        principalTable: "PaymentSettlements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentSettlementAdjustments_PaymentSettlements_TenantId_ProjectId_OriginalSettlementId",
                        columns: x => new { x.TenantId, x.ProjectId, x.OriginalSettlementId },
                        principalTable: "PaymentSettlements",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlementAdjustments_ReplacementSettlementId",
                table: "PaymentSettlementAdjustments",
                column: "ReplacementSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_IdempotencyKey",
                table: "PaymentSettlementAdjustments",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlementAdjustments_TenantId_ProjectId_OriginalSettlementId",
                table: "PaymentSettlementAdjustments",
                columns: new[] { "TenantId", "ProjectId", "OriginalSettlementId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentSettlementAdjustments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PaymentSettlements_TenantId_ProjectId_Id",
                table: "PaymentSettlements");
        }
    }
}
