using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProjectChangeRequestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Field",
                table: "ProjectChangeRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_PendingDataChange_Per_Project_Field",
                table: "ProjectChangeRequests",
                columns: new[] { "TenantId", "ProjectId", "Field" },
                unique: true,
                filter: "[Status] = 'Pending' AND [Type] = 'DataChange'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingDataChange_Per_Project_Field",
                table: "ProjectChangeRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Field",
                table: "ProjectChangeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
