using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations;

public partial class AddDraftAnnualBudgetApprovalConfigurationJson : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DraftApprovalConfigurationJson",
            table: "ProjectAnnualBudgets",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DraftApprovalConfigurationJson",
            table: "ProjectAnnualBudgets");
    }
}
