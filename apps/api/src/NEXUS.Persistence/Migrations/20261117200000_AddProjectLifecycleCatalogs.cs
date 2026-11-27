using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations;

// Preserve the concurrently authored operations as an unregistered draft. The
// complete recovered lifecycle migration owns this schema transition.
public partial class PendingAddProjectLifecycleCatalogsDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ProjectPhaseId",
            table: "Projects",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectStatusId",
            table: "Projects",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ProjectPhaseCatalogEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                NormalizedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DescriptionEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DescriptionAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                IsContinuousActivity = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectPhaseCatalogEntries", x => x.Id);
                table.UniqueConstraint("AK_ProjectPhaseCatalogEntries_Tenant_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_ProjectPhaseCatalogEntries_DisplayOrder", "[DisplayOrder] >= 0");
                table.ForeignKey(
                    name: "FK_ProjectPhaseCatalogEntries_Organizations_TenantId_OrganizationId",
                    columns: x => new { x.TenantId, x.OrganizationId },
                    principalTable: "Organizations",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProjectStatusCatalogEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                NormalizedCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DescriptionEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DescriptionAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                ReportingCategory = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "InProgress"),
                AttentionLevel = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "None"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectStatusCatalogEntries", x => x.Id);
                table.UniqueConstraint("AK_ProjectStatusCatalogEntries_Tenant_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_ProjectStatusCatalogEntries_DisplayOrder", "[DisplayOrder] >= 0");
                table.CheckConstraint("CK_ProjectStatusCatalogEntries_ReportingCategory", "[ReportingCategory] IN ('InProgress', 'Completed', 'Excluded')");
                table.CheckConstraint("CK_ProjectStatusCatalogEntries_AttentionLevel", "[AttentionLevel] IN ('None', 'NeedsAttention', 'Critical')");
                table.ForeignKey(
                    name: "FK_ProjectStatusCatalogEntries_Organizations_TenantId_OrganizationId",
                    columns: x => new { x.TenantId, x.OrganizationId },
                    principalTable: "Organizations",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectPhaseCatalogEntries_Scope_Order",
            table: "ProjectPhaseCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId", "IsActive", "DisplayOrder" });

        migrationBuilder.CreateIndex(
            name: "UX_ProjectPhaseCatalogEntries_Scope_Code",
            table: "ProjectPhaseCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId", "NormalizedCode" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "UX_ProjectPhaseCatalogEntries_Scope_Default",
            table: "ProjectPhaseCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [IsActive] = 1 AND [IsDefault] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStatusCatalogEntries_Scope_Order",
            table: "ProjectStatusCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId", "IsActive", "DisplayOrder" });

        migrationBuilder.CreateIndex(
            name: "UX_ProjectStatusCatalogEntries_Scope_Code",
            table: "ProjectStatusCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId", "NormalizedCode" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "UX_ProjectStatusCatalogEntries_Scope_Default",
            table: "ProjectStatusCatalogEntries",
            columns: new[] { "TenantId", "OrganizationId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [IsActive] = 1 AND [IsDefault] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectStatusCatalogEntries");
        migrationBuilder.DropTable(name: "ProjectPhaseCatalogEntries");
        migrationBuilder.DropColumn(name: "ProjectStatusId", table: "Projects");
        migrationBuilder.DropColumn(name: "ProjectPhaseId", table: "Projects");
    }
}
