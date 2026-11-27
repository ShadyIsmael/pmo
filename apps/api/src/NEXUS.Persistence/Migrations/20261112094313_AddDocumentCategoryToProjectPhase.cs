using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCategoryToProjectPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DocumentCategories_TenantId_Id",
                table: "DocumentCategories",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseCatalogEntries_TenantId_DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries",
                columns: new[] { "TenantId", "DocumentCategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhaseCatalogEntries_DocumentCategories_TenantId_DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries",
                columns: new[] { "TenantId", "DocumentCategoryId" },
                principalTable: "DocumentCategories",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhaseCatalogEntries_DocumentCategories_TenantId_DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPhaseCatalogEntries_TenantId_DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DocumentCategories_TenantId_Id",
                table: "DocumentCategories");

            migrationBuilder.DropColumn(
                name: "DocumentCategoryId",
                table: "ProjectPhaseCatalogEntries");
        }
    }
}
