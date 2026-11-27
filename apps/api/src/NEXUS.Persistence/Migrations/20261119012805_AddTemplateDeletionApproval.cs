using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateDeletionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeletionApproved",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TemplateDeletionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OwnerOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerScope = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApproverName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeletionType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    TemplateMutationSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageCleanupKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageCleanupStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StorageCleanupLastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StorageCleanupError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_TemplateDeletionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateDeletionRequests_TenantId_RequesterId_RequestedAt",
                table: "TemplateDeletionRequests",
                columns: new[] { "TenantId", "RequesterId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateDeletionRequests_TenantId_Status_RequestedAt",
                table: "TemplateDeletionRequests",
                columns: new[] { "TenantId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_TemplateDeletionRequests_Tenant_Template_Pending",
                table: "TemplateDeletionRequests",
                columns: new[] { "TenantId", "TemplateId" },
                unique: true,
                filter: "[Status] = N'PendingApproval'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateDeletionRequests");

            migrationBuilder.DropColumn(
                name: "IsDeletionApproved",
                table: "Documents");
        }
    }
}
