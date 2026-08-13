using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModernizeProjectBaselineVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "ProjectBaselineVersions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ProjectBaselineVersions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProjectBaselineVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProjectBaselineVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ProjectBaselineVersions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProjectBaselineVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProjectBaselineVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ProjectBaselineVersions");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "ProjectBaselineVersions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ProjectBaselineVersions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
