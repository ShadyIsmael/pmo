using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Resource_Action",
                table: "Permissions");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.CheckConstraint("CK_Tenants_Status", "[Status] IN (N'Provisioning', N'Active', N'Suspended', N'Retired')");
                });

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Tenant_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Permissions_Tenant_Resource_Action",
                table: "Permissions",
                columns: new[] { "TenantId", "Resource", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            // Existing installations expressed tenancy through TenantId values but had no registry.
            // Register IDs already referenced by the core identity/organization rows so the new
            // Organization FK can be added without fabricating credentials or changing ownership.
            migrationBuilder.Sql(@"
INSERT INTO [Tenants] ([Id], [Code], [DisplayName], [Status], [CreatedAt], [CreatedBy])
SELECT legacy.[TenantId],
       N'LEGACY-' + REPLACE(CONVERT(nvarchar(36), legacy.[TenantId]), N'-', N''),
       N'Legacy Tenant ' + CONVERT(nvarchar(36), legacy.[TenantId]),
       N'Active',
       SYSUTCDATETIME(),
       N'Migration'
FROM (
    SELECT [TenantId] FROM [Users] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
    UNION
    SELECT [TenantId] FROM [Organizations] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
    UNION
    SELECT [TenantId] FROM [Roles] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
    UNION
    SELECT [TenantId] FROM [ConfigurationSettings] WHERE [TenantId] IS NOT NULL AND [TenantId] <> '00000000-0000-0000-0000-000000000000'
) AS legacy
WHERE NOT EXISTS (SELECT 1 FROM [Tenants] AS registered WHERE registered.[Id] = legacy.[TenantId]);");

            migrationBuilder.AddForeignKey(
                name: "FK_ConfigurationSettings_Tenants_TenantId",
                table: "ConfigurationSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_Tenants_TenantId",
                table: "Organizations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConfigurationSettings_Tenants_TenantId",
                table: "ConfigurationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_Tenants_TenantId",
                table: "Organizations");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "UX_Roles_Tenant_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UX_Permissions_Tenant_Resource_Action",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Resource_Action",
                table: "Permissions",
                columns: new[] { "Resource", "Action" },
                unique: true);
        }
    }
}
