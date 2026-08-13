using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorProjectChangeRequestToHeaderOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingDataChange_Per_Project_Field",
                table: "ProjectChangeRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectChangeRequest_TypeAndField",
                table: "ProjectChangeRequests");

            migrationBuilder.DropColumn(
                name: "CurrentValue",
                table: "ProjectChangeRequests");

            migrationBuilder.DropColumn(
                name: "Field",
                table: "ProjectChangeRequests");

            migrationBuilder.DropColumn(
                name: "ProposedValue",
                table: "ProjectChangeRequests");

            migrationBuilder.AddColumn<string>(
                name: "Attachments",
                table: "ProjectChangeRequests",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Justification",
                table: "ProjectChangeRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "ProjectChangeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attachments",
                table: "ProjectChangeRequests");

            migrationBuilder.DropColumn(
                name: "Justification",
                table: "ProjectChangeRequests");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "ProjectChangeRequests");

            migrationBuilder.AddColumn<string>(
                name: "CurrentValue",
                table: "ProjectChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Field",
                table: "ProjectChangeRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedValue",
                table: "ProjectChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingDataChange_Per_Project_Field",
                table: "ProjectChangeRequests",
                columns: new[] { "TenantId", "ProjectId", "Field" },
                unique: true,
                filter: "[Status] = 'Pending' AND [Type] = 'DataChange'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectChangeRequest_TypeAndField",
                table: "ProjectChangeRequests",
                sql: "(Type = 'DataChange' AND Field IS NOT NULL) OR (Type = 'BaselineChange' AND Field IS NULL)");
        }
    }
}
