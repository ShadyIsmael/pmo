using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2AddPlannedPaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlannedPaymentInstallmentId",
                table: "ProjectPaymentRequestDetails",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlannedPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistrationFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdateIdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdateFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RegisteredBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstRequestAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacesPlannedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplacedByPlannedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PlannedPayments", x => x.Id);
                    table.UniqueConstraint("AK_PlannedPayments_Tenant_Project_Plan", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_PlannedPayments_Currency", "LEN([Currency]) = 3");
                    table.CheckConstraint("CK_PlannedPayments_IdempotencyFingerprint", "([IdempotencyKey] IS NULL AND [RegistrationFingerprint] IS NULL) OR ([IdempotencyKey] IS NOT NULL AND [RegistrationFingerprint] IS NOT NULL)");
                    table.CheckConstraint("CK_PlannedPayments_State", "[State] IN (N'Registered', N'Locked', N'Replaced', N'Cancelled')");
                    table.CheckConstraint("CK_PlannedPayments_TotalAmount", "[TotalAmount] > 0");
                    table.CheckConstraint("CK_PlannedPayments_UpdateIdempotencyFingerprint", "([UpdateIdempotencyKey] IS NULL AND [UpdateFingerprint] IS NULL) OR ([UpdateIdempotencyKey] IS NOT NULL AND [UpdateFingerprint] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlannedPayments_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlannedPaymentInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlanActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PlannedPaymentInstallments", x => x.Id);
                    table.UniqueConstraint("AK_PlannedPaymentInstallments_Tenant_Project_Installment", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_PlannedPaymentInstallments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PlannedPaymentInstallments_Sequence", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_PlannedPaymentInstallments_PlanActivities_TenantId_PlanActivityId",
                        columns: x => new { x.TenantId, x.PlanActivityId },
                        principalTable: "PlanActivities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlannedPaymentInstallments_PlannedPayments_TenantId_ProjectId_PlannedPaymentId",
                        columns: x => new { x.TenantId, x.ProjectId, x.PlannedPaymentId },
                        principalTable: "PlannedPayments",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedPaymentLifetimeReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReleasedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_PlannedPaymentLifetimeReservations", x => x.Id);
                    table.UniqueConstraint("AK_PlannedPaymentLifetimeReservations_Tenant_Project_Reservation", x => new { x.TenantId, x.ProjectId, x.Id });
                    table.CheckConstraint("CK_PlannedPaymentLifetimeReservations_ReleasedAmount", "[ReleasedAmount] >= 0 AND [ReleasedAmount] <= [ReservedAmount]");
                    table.CheckConstraint("CK_PlannedPaymentLifetimeReservations_ReservedAmount", "[ReservedAmount] > 0");
                    table.ForeignKey(
                        name: "FK_PlannedPaymentLifetimeReservations_PlannedPayments_TenantId_ProjectId_PlannedPaymentId",
                        columns: x => new { x.TenantId, x.ProjectId, x.PlannedPaymentId },
                        principalTable: "PlannedPayments",
                        principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_PlannedInstallment",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "PlannedPaymentInstallmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPaymentInstallments_Tenant_Project_Milestone",
                table: "PlannedPaymentInstallments",
                columns: new[] { "TenantId", "ProjectId", "PlanActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPaymentInstallments_TenantId_PlanActivityId",
                table: "PlannedPaymentInstallments",
                columns: new[] { "TenantId", "PlanActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPaymentInstallments_TenantId_ProjectId_PlannedPaymentId",
                table: "PlannedPaymentInstallments",
                columns: new[] { "TenantId", "ProjectId", "PlannedPaymentId" });

            migrationBuilder.CreateIndex(
                name: "UX_PlannedPaymentInstallments_Plan_Sequence",
                table: "PlannedPaymentInstallments",
                columns: new[] { "TenantId", "PlannedPaymentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPaymentLifetimeReservations_Tenant_Project",
                table: "PlannedPaymentLifetimeReservations",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPaymentLifetimeReservations_TenantId_ProjectId_PlannedPaymentId",
                table: "PlannedPaymentLifetimeReservations",
                columns: new[] { "TenantId", "ProjectId", "PlannedPaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlannedPaymentLifetimeReservations_Tenant_Plan",
                table: "PlannedPaymentLifetimeReservations",
                columns: new[] { "TenantId", "PlannedPaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedPayments_Tenant_Project_State",
                table: "PlannedPayments",
                columns: new[] { "TenantId", "ProjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "UX_PlannedPayments_Tenant_Project_Idempotency",
                table: "PlannedPayments",
                columns: new[] { "TenantId", "ProjectId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PlannedPayments_Tenant_Project_Reference",
                table: "PlannedPayments",
                columns: new[] { "TenantId", "ProjectId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlannedPayments_Tenant_Project_UpdateIdempotency",
                table: "PlannedPayments",
                columns: new[] { "TenantId", "ProjectId", "UpdateIdempotencyKey" },
                unique: true,
                filter: "[UpdateIdempotencyKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPaymentRequestDetails_PlannedPaymentInstallments_TenantId_ProjectId_PlannedPaymentInstallmentId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "PlannedPaymentInstallmentId" },
                principalTable: "PlannedPaymentInstallments",
                principalColumns: new[] { "TenantId", "ProjectId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPaymentRequestDetails_PlannedPaymentInstallments_TenantId_ProjectId_PlannedPaymentInstallmentId",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropTable(
                name: "PlannedPaymentInstallments");

            migrationBuilder.DropTable(
                name: "PlannedPaymentLifetimeReservations");

            migrationBuilder.DropTable(
                name: "PlannedPayments");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPaymentRequestDetails_PlannedInstallment",
                table: "ProjectPaymentRequestDetails");

            migrationBuilder.DropColumn(
                name: "PlannedPaymentInstallmentId",
                table: "ProjectPaymentRequestDetails");
        }
    }
}
