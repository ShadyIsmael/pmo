using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanTypeToPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plans_Tenant_Project",
                table: "Plans");

            migrationBuilder.AddColumn<int>(
                name: "PlanType",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Tenant_Project_Type",
                table: "Plans",
                columns: new[] { "TenantId", "ProjectId", "PlanType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plans_Tenant_Project_Type",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "Plans");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Tenant_Project",
                table: "Plans",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true);
        }
    }
}
