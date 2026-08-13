using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectNameToAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allocation",
                table: "ProjectMembers");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "ProjectMembers",
                newName: "ResourceId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Progress",
                table: "Projects",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RagStatus",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SponsorId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "ProjectMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "ProjectMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocationPercentage",
                table: "ProjectMembers",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursPerWeek",
                table: "ProjectMembers",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ProjectMembers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProjectMembers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RagStatus",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SponsorId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AllocationPercentage",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "HoursPerWeek",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectMembers");

            migrationBuilder.RenameColumn(
                name: "ResourceId",
                table: "ProjectMembers",
                newName: "UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "Progress",
                table: "Projects",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,4)",
                oldPrecision: 7,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "ProjectMembers",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "ProjectMembers",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<decimal>(
                name: "Allocation",
                table: "ProjectMembers",
                type: "decimal(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);
        }
    }
}
