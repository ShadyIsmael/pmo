using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemTeamAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedResourceId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedTeamId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            // The old AssigneeId stored the authenticated User id. Preserve that
            // assignment by resolving it to the user's Resource before removing
            // the legacy column. Team assignments remain null until explicitly set.
            migrationBuilder.Sql("UPDATE wi SET AssignedResourceId = r.Id FROM WorkItems wi INNER JOIN Resources r ON r.UserId = wi.AssigneeId WHERE wi.AssigneeId IS NOT NULL AND wi.AssignedResourceId IS NULL");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssignedResourceId",
                table: "WorkItems",
                column: "AssignedResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssignedTeamId",
                table: "WorkItems",
                column: "AssignedTeamId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkItems_ExactlyOneAssignmentTarget",
                table: "WorkItems",
                sql: "([AssignedResourceId] IS NULL OR [AssignedTeamId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkItems_ExactlyOneAssignmentTarget",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssignedResourceId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssignedTeamId",
                table: "WorkItems");

            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE wi SET AssigneeId = r.UserId FROM WorkItems wi INNER JOIN Resources r ON r.Id = wi.AssignedResourceId WHERE wi.AssignedResourceId IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "AssignedResourceId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignedTeamId",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId");
        }
    }
}
