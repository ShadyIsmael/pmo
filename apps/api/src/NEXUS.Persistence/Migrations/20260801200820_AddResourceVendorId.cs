using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceVendorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Portfolios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Portfolios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Portfolios",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Portfolios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Portfolios",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "Portfolios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Portfolios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfidenceLevel",
                table: "Portfolios",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Portfolios",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SponsorId",
                table: "Portfolios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StrategicObjective",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_VendorId",
                table: "Resources",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_TenantId_Code",
                table: "Portfolios",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_TenantId_Status",
                table: "Portfolios",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Vendors_VendorId",
                table: "Resources",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Vendors_VendorId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_VendorId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_TenantId_Code",
                table: "Portfolios");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_TenantId_Status",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "ConfidenceLevel",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "SponsorId",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "StrategicObjective",
                table: "Portfolios");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Portfolios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Portfolios",
                type: "varbinary(max)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Portfolios",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
