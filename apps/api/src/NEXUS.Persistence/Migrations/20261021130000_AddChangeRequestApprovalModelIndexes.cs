using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRequestApprovalModelIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "SponsorApprovalRequired",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "AllowSequentialApprovers",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "AllowAnyOneOptionalApprover",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChangeRequests_WorkflowInstanceId",
                table: "ProjectChangeRequests",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChangeRequestApprovals_ProjectChangeRequestId",
                table: "ProjectChangeRequestApprovals",
                column: "ProjectChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChangeRequestApprovalHistories_ProjectChangeRequestApprovalId",
                table: "ProjectChangeRequestApprovalHistories",
                column: "ProjectChangeRequestApprovalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectChangeRequests_WorkflowInstanceId",
                table: "ProjectChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_ProjectChangeRequestApprovals_ProjectChangeRequestId",
                table: "ProjectChangeRequestApprovals");

            migrationBuilder.DropIndex(
                name: "IX_ProjectChangeRequestApprovalHistories_ProjectChangeRequestApprovalId",
                table: "ProjectChangeRequestApprovalHistories");

            migrationBuilder.AlterColumn<bool>(
                name: "SponsorApprovalRequired",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "AllowSequentialApprovers",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "AllowAnyOneOptionalApprover",
                table: "ProjectWorkflowConfigurations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
