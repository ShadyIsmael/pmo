using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectOriginalBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalProjectBudget",
                table: "Projects",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            // Backfill the original lifetime ceiling for projects that already exist so the
            // baseline matches the approved ceiling captured before this column was introduced.
            migrationBuilder.Sql(
                "UPDATE [Projects] SET [OriginalProjectBudget] = [Budget] WHERE [OriginalProjectBudget] IS NULL AND [Budget] IS NOT NULL AND [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalProjectBudget",
                table: "Projects");
        }
    }
}
