using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationManagedDocumentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DocumentCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "DocumentCategories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DocumentCategories",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNameAr",
                table: "DocumentCategories",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNameEn",
                table: "DocumentCategories",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [DocumentCategories] SET [NormalizedNameEn] = LEFT(UPPER(LTRIM(RTRIM([Name]))), 400) WHERE [NormalizedNameEn] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedNameEn",
                table: "DocumentCategories",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentCategories_Organizations_TenantId_OrganizationId",
                table: "DocumentCategories",
                columns: new[] { "TenantId", "OrganizationId" },
                principalTable: "Organizations",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "UX_DocumentCategories_Organization_NameAr",
                table: "DocumentCategories",
                columns: new[] { "TenantId", "OrganizationId", "NormalizedNameAr" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL AND [IsDeleted] = 0 AND [NormalizedNameAr] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentCategories_Organization_NameEn",
                table: "DocumentCategories",
                columns: new[] { "TenantId", "OrganizationId", "NormalizedNameEn" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentCategories_Organizations_TenantId_OrganizationId",
                table: "DocumentCategories");

            migrationBuilder.DropIndex(
                name: "UX_DocumentCategories_Organization_NameAr",
                table: "DocumentCategories");

            migrationBuilder.DropIndex(
                name: "UX_DocumentCategories_Organization_NameEn",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedNameAr",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedNameEn",
                table: "DocumentCategories");
        }
    }
}