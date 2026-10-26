using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NEXUS.Persistence;

#nullable disable

namespace NEXUS.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261021120000_AddProjectChangeRequestApprovals")]
public partial class AddProjectChangeRequestApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ApprovalStrategy", table: "ProjectChangeRequests", type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "AllRequired");
        migrationBuilder.AddColumn<string>(name: "SelectedApproverUserIdsJson", table: "ProjectChangeRequests", type: "nvarchar(max)", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<Guid>(name: "WorkflowInstanceId", table: "ProjectChangeRequests", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "SubmittedAt", table: "ProjectChangeRequests", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "SponsorApprovalRequired", table: "ProjectWorkflowConfigurations", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(name: "AllowAnyOneOptionalApprover", table: "ProjectWorkflowConfigurations", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "AllowSequentialApprovers", table: "ProjectWorkflowConfigurations", type: "bit", nullable: false, defaultValue: true);

        migrationBuilder.CreateIndex(name: "IX_ProjectChangeRequests_TenantId_WorkflowInstanceId", table: "ProjectChangeRequests", columns: new[] { "TenantId", "WorkflowInstanceId" }, unique: true, filter: "[IsDeleted] = 0 AND [WorkflowInstanceId] IS NOT NULL");
        migrationBuilder.AddForeignKey(name: "FK_ProjectChangeRequests_WorkflowInstances_WorkflowInstanceId", table: "ProjectChangeRequests", column: "WorkflowInstanceId", principalTable: "WorkflowInstances", principalColumn: "Id", onDelete: ReferentialAction.NoAction);

        migrationBuilder.CreateTable(
            name: "ProjectChangeRequestApprovals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginalApproverNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApproverNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RoleSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsSponsor = table.Column<bool>(type: "bit", nullable: false),
                IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DecisionActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_ProjectChangeRequestApprovals", item => item.Id);
                table.ForeignKey(name: "FK_ProjectChangeRequestApprovals_ProjectChangeRequests_ProjectChangeRequestId", column: item => item.ProjectChangeRequestId, principalTable: "ProjectChangeRequests", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_ProjectChangeRequestApprovals_TenantId_ProjectChangeRequestId_Sequence", table: "ProjectChangeRequestApprovals", columns: new[] { "TenantId", "ProjectChangeRequestId", "Sequence" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(name: "IX_ProjectChangeRequestApprovals_TenantId_AssignedToUserId_Status", table: "ProjectChangeRequestApprovals", columns: new[] { "TenantId", "AssignedToUserId", "Status" });

        migrationBuilder.CreateTable(
            name: "ProjectChangeRequestApprovalHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectChangeRequestApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromApproverNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ToApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ToApproverNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChangedByNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                table.PrimaryKey("PK_ProjectChangeRequestApprovalHistories", item => item.Id);
                table.ForeignKey(name: "FK_ProjectChangeRequestApprovalHistories_ProjectChangeRequestApprovals_ProjectChangeRequestApprovalId", column: item => item.ProjectChangeRequestApprovalId, principalTable: "ProjectChangeRequestApprovals", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateIndex(name: "IX_ProjectChangeRequestApprovalHistories_TenantId_ProjectChangeRequestApprovalId_ChangedAt", table: "ProjectChangeRequestApprovalHistories", columns: new[] { "TenantId", "ProjectChangeRequestApprovalId", "ChangedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectChangeRequestApprovalHistories");
        migrationBuilder.DropTable(name: "ProjectChangeRequestApprovals");
        migrationBuilder.DropForeignKey(name: "FK_ProjectChangeRequests_WorkflowInstances_WorkflowInstanceId", table: "ProjectChangeRequests");
        migrationBuilder.DropIndex(name: "IX_ProjectChangeRequests_TenantId_WorkflowInstanceId", table: "ProjectChangeRequests");
        migrationBuilder.DropColumn(name: "ApprovalStrategy", table: "ProjectChangeRequests");
        migrationBuilder.DropColumn(name: "SelectedApproverUserIdsJson", table: "ProjectChangeRequests");
        migrationBuilder.DropColumn(name: "WorkflowInstanceId", table: "ProjectChangeRequests");
        migrationBuilder.DropColumn(name: "SubmittedAt", table: "ProjectChangeRequests");
        migrationBuilder.DropColumn(name: "SponsorApprovalRequired", table: "ProjectWorkflowConfigurations");
        migrationBuilder.DropColumn(name: "AllowAnyOneOptionalApprover", table: "ProjectWorkflowConfigurations");
        migrationBuilder.DropColumn(name: "AllowSequentialApprovers", table: "ProjectWorkflowConfigurations");
    }
}
