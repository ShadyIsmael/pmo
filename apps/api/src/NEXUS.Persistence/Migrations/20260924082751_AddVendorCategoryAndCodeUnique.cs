using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorCategoryAndCodeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendorCategoryId",
                table: "Vendors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_TenantId_Code",
                table: "Vendors",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_VendorCategoryId",
                table: "Vendors",
                column: "VendorCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vendors_Lookups_VendorCategoryId",
                table: "Vendors",
                column: "VendorCategoryId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vendors_Lookups_VendorCategoryId",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_TenantId_Code",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_VendorCategoryId",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "VendorCategoryId",
                table: "Vendors");
        }
    }
}
