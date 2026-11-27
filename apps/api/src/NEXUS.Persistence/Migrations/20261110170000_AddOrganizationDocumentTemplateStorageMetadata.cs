using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations;

public partial class AddOrganizationDocumentTemplateStorageMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "Documents",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceTemplateVersionId",
            table: "Documents",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StorageProviderId",
            table: "DocumentVersions",
            type: "nvarchar(64)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StorageKey",
            table: "DocumentVersions",
            type: "nvarchar(512)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Sha256",
            table: "DocumentVersions",
            type: "nvarchar(64)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Documents_OrganizationId",
            table: "Documents",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_SourceTemplateVersionId",
            table: "Documents",
            column: "SourceTemplateVersionId");

        migrationBuilder.AddForeignKey(
            name: "FK_Documents_Organizations_OrganizationId",
            table: "Documents",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Documents_DocumentVersions_SourceTemplateVersionId",
            table: "Documents",
            column: "SourceTemplateVersionId",
            principalTable: "DocumentVersions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Documents_Organizations_OrganizationId",
            table: "Documents");

        migrationBuilder.DropForeignKey(
            name: "FK_Documents_DocumentVersions_SourceTemplateVersionId",
            table: "Documents");

        migrationBuilder.DropIndex(
            name: "IX_Documents_OrganizationId",
            table: "Documents");

        migrationBuilder.DropIndex(
            name: "IX_Documents_SourceTemplateVersionId",
            table: "Documents");

        migrationBuilder.DropColumn(name: "OrganizationId", table: "Documents");
        migrationBuilder.DropColumn(name: "SourceTemplateVersionId", table: "Documents");
        migrationBuilder.DropColumn(name: "StorageProviderId", table: "DocumentVersions");
        migrationBuilder.DropColumn(name: "StorageKey", table: "DocumentVersions");
        migrationBuilder.DropColumn(name: "Sha256", table: "DocumentVersions");
    }
}
