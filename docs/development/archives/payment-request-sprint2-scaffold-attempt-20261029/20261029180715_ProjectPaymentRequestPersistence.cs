using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectPaymentRequestPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "ApprovalRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ApprovalRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ApprovalRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "ApproverAssignmentId",
                table: "ApprovalActions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Projects_TenantId_Id",
                table: "Projects",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PlanActivities_TenantId_Id",
                table: "PlanActivities",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ApprovalRequests_TenantId_Id",
                table: "ApprovalRequests",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "PaymentRequestApproverAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: true),
                    Requirement = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
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
                    table.PrimaryKey("PK_PaymentRequestApproverAssignments", x => x.Id);
                    table.UniqueConstraint("AK_PaymentRequestApproverAssignments_Tenant_Request_Id", x => new { x.TenantId, x.ApprovalRequestId, x.Id });
                    table.CheckConstraint("CK_PaymentRequestApproverAssignments_Position", "[Position] IS NULL OR [Position] > 0");
                    table.CheckConstraint("CK_PaymentRequestApproverAssignments_Requirement", "[Requirement] IN (N'Mandatory', N'Optional')");
                    table.CheckConstraint("CK_PaymentRequestApproverAssignments_State", "[State] IN (N'Pending', N'Approved', N'Rejected')");
                    table.ForeignKey(
                        name: "FK_PaymentRequestApproverAssignments_ApprovalRequests_TenantId_ApprovalRequestId",
                        columns: x => new { x.TenantId, x.ApprovalRequestId },
                        principalTable: "ApprovalRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRequestApproverAssignments_Users_TenantId_ApproverUserId",
                        columns: x => new { x.TenantId, x.ApproverUserId },
                        principalTable: "Users",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentRequestDetails",
                columns: table => new
                {
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalMode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    RequestKind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReversesApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPaymentRequestDetails", x => x.ApprovalRequestId);
                    table.CheckConstraint("CK_ProjectPaymentRequestDetails_ApprovalMode", "[ApprovalMode] IS NULL OR [ApprovalMode] IN (N'Sequential', N'Parallel')");
                    table.CheckConstraint("CK_ProjectPaymentRequestDetails_IdempotencyFingerprint", "([IdempotencyKey] IS NULL AND [RequestFingerprint] IS NULL) OR ([IdempotencyKey] IS NOT NULL AND [RequestFingerprint] IS NOT NULL)");
                    table.CheckConstraint("CK_ProjectPaymentRequestDetails_RequestKind", "[RequestKind] IN (N'PaymentRequest', N'ReversalRequest')");
                    table.CheckConstraint("CK_ProjectPaymentRequestDetails_ReversalReference", "([RequestKind] = N'PaymentRequest' AND [ReversesApprovalRequestId] IS NULL) OR ([RequestKind] = N'ReversalRequest' AND [ReversesApprovalRequestId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ApprovalRequestId",
                        columns: x => new { x.TenantId, x.ApprovalRequestId },
                        principalTable: "ApprovalRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ReversesApprovalRequestId",
                        columns: x => new { x.TenantId, x.ReversesApprovalRequestId },
                        principalTable: "ApprovalRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestDetails_PlanActivities_TenantId_PlanActivityId",
                        columns: x => new { x.TenantId, x.PlanActivityId },
                        principalTable: "PlanActivities",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRequestDetails_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Tenant_Type_Entity_Status",
                table: "ApprovalRequests",
                columns: new[] { "TenantId", "Type", "EntityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalActions_TenantId_ApprovalRequestId_ApproverAssignmentId",
                table: "ApprovalActions",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApproverAssignmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_ApprovalActions_ApproverAssignment",
                table: "ApprovalActions",
                column: "ApproverAssignmentId",
                unique: true,
                filter: "[ApproverAssignmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestApproverAssignments_TenantId_ApproverUserId",
                table: "PaymentRequestApproverAssignments",
                columns: new[] { "TenantId", "ApproverUserId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_ReversesRequest",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ReversesApprovalRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_Tenant_Project_Kind",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "RequestKind" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ApprovalRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRequestDetails_TenantId_PlanActivityId",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "PlanActivityId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPaymentRequestDetails_Project_Idempotency",
                table: "ProjectPaymentRequestDetails",
                columns: new[] { "TenantId", "ProjectId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalActions_PaymentRequestApproverAssignments_TenantId_ApprovalRequestId_ApproverAssignmentId",
                table: "ApprovalActions",
                columns: new[] { "TenantId", "ApprovalRequestId", "ApproverAssignmentId" },
                principalTable: "PaymentRequestApproverAssignments",
                principalColumns: new[] { "TenantId", "ApprovalRequestId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalActions_PaymentRequestApproverAssignments_TenantId_ApprovalRequestId_ApproverAssignmentId",
                table: "ApprovalActions");

            migrationBuilder.DropTable(
                name: "PaymentRequestApproverAssignments");

            migrationBuilder.DropTable(
                name: "ProjectPaymentRequestDetails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Projects_TenantId_Id",
                table: "Projects");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PlanActivities_TenantId_Id",
                table: "PlanActivities");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ApprovalRequests_TenantId_Id",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_Tenant_Type_Entity_Status",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalActions_TenantId_ApprovalRequestId_ApproverAssignmentId",
                table: "ApprovalActions");

            migrationBuilder.DropIndex(
                name: "UX_ApprovalActions_ApproverAssignment",
                table: "ApprovalActions");

            migrationBuilder.DropColumn(
                name: "ApproverAssignmentId",
                table: "ApprovalActions");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "ApprovalRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ApprovalRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ApprovalRequests",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true);
        }
    }
}
