using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeManagerHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentProjectManagerId",
                table: "ProjectHandoverRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "InitiationType",
                table: "ProjectHandoverRequests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ManagerInitiated");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentProgramManagerId",
                table: "ProgramHandoverRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "InitiationType",
                table: "ProgramHandoverRequests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ManagerInitiated");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentPortfolioManagerId",
                table: "PortfolioHandoverRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "InitiationType",
                table: "PortfolioHandoverRequests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ManagerInitiated");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverRequests_TenantId_ProjectId_Status",
                table: "ProjectHandoverRequests",
                columns: new[] { "TenantId", "ProjectId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending' AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectHandoverRequests_TenantId_ProjectId_Status",
                table: "ProjectHandoverRequests");

            migrationBuilder.DropColumn(
                name: "InitiationType",
                table: "ProjectHandoverRequests");

            migrationBuilder.DropColumn(
                name: "InitiationType",
                table: "ProgramHandoverRequests");

            migrationBuilder.DropColumn(
                name: "InitiationType",
                table: "PortfolioHandoverRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentProjectManagerId",
                table: "ProjectHandoverRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentProgramManagerId",
                table: "ProgramHandoverRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentPortfolioManagerId",
                table: "PortfolioHandoverRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
