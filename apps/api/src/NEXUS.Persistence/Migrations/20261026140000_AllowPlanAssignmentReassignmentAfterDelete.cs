using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    public partial class AllowPlanAssignmentReassignmentAfterDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "ResourceId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignments_Tenant_Version_Activity_Resource",
                table: "PlanAssignments",
                columns: new[] { "TenantId", "PlanVersionId", "ActivityId", "ResourceId" },
                unique: true);
        }
    }
}
