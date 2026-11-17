using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedPaymentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentObligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PaymentObligations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentNotificationDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NotificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PaymentNotificationDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentNotificationDispatches_PaymentObligations_PaymentObligationId",
                        column: x => x.PaymentObligationId,
                        principalTable: "PaymentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PaymentSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentSettlements_PaymentObligations_PaymentObligationId",
                        column: x => x.PaymentObligationId,
                        principalTable: "PaymentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotificationDispatches_PaymentObligationId",
                table: "PaymentNotificationDispatches",
                column: "PaymentObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotificationDispatches_TenantId_PaymentObligationId",
                table: "PaymentNotificationDispatches",
                columns: new[] { "TenantId", "PaymentObligationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentNotificationDispatches_TenantId_PaymentObligationId_UserId_NotificationType_NotificationDate",
                table: "PaymentNotificationDispatches",
                columns: new[] { "TenantId", "PaymentObligationId", "UserId", "NotificationType", "NotificationDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligations_TenantId_MilestoneId",
                table: "PaymentObligations",
                columns: new[] { "TenantId", "MilestoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligations_TenantId_ProjectId_DueDate",
                table: "PaymentObligations",
                columns: new[] { "TenantId", "ProjectId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligations_TenantId_ProjectId_PaymentReference",
                table: "PaymentObligations",
                columns: new[] { "TenantId", "ProjectId", "PaymentReference" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_PaymentObligationId",
                table: "PaymentSettlements",
                column: "PaymentObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_IdempotencyKey",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSettlements_TenantId_PaymentObligationId_SettlementDate",
                table: "PaymentSettlements",
                columns: new[] { "TenantId", "PaymentObligationId", "SettlementDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentNotificationDispatches");

            migrationBuilder.DropTable(
                name: "PaymentSettlements");

            migrationBuilder.DropTable(
                name: "PaymentObligations");
        }
    }
}
