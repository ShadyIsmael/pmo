using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStatusReportingSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttentionLevel",
                table: "ProjectStatusCatalogEntries",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "ReportingCategory",
                table: "ProjectStatusCatalogEntries",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "InProgress");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectStatusCatalogEntries_AttentionLevel",
                table: "ProjectStatusCatalogEntries",
                sql: "[AttentionLevel] IN ('None', 'NeedsAttention', 'Critical')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectStatusCatalogEntries_ReportingCategory",
                table: "ProjectStatusCatalogEntries",
                sql: "[ReportingCategory] IN ('InProgress', 'Completed', 'Excluded')");

            migrationBuilder.Sql(
                """
                UPDATE dbo.ProjectStatusCatalogEntries
                SET ReportingCategory = CASE
                        WHEN UPPER(LTRIM(RTRIM(NormalizedCode))) IN (N'COMPLETED', N'CLOSED', N'DONE')
                          OR UPPER(LTRIM(RTRIM(NameEn))) IN (N'COMPLETED', N'CLOSED', N'DONE') THEN N'Completed'
                        WHEN UPPER(LTRIM(RTRIM(NormalizedCode))) IN (N'CANCELLED', N'CANCELED', N'ARCHIVED')
                          OR UPPER(LTRIM(RTRIM(NameEn))) IN (N'CANCELLED', N'CANCELED', N'ARCHIVED') THEN N'Excluded'
                        ELSE N'InProgress'
                    END,
                    AttentionLevel = CASE
                        WHEN UPPER(LTRIM(RTRIM(NormalizedCode))) = N'CRITICAL'
                          OR UPPER(LTRIM(RTRIM(NameEn))) = N'CRITICAL' THEN N'Critical'
                        WHEN UPPER(LTRIM(RTRIM(NormalizedCode))) IN (N'ON-HOLD', N'ONHOLD', N'RISK', N'DELAY')
                          OR UPPER(LTRIM(RTRIM(NameEn))) IN (N'ON HOLD', N'ON-HOLD')
                          OR UPPER(NormalizedCode) LIKE N'%RISK%'
                          OR UPPER(NormalizedCode) LIKE N'%DELAY%'
                          OR UPPER(NameEn) LIKE N'%RISK%'
                          OR UPPER(NameEn) LIKE N'%DELAY%' THEN N'NeedsAttention'
                        ELSE N'None'
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectStatusCatalogEntries_AttentionLevel",
                table: "ProjectStatusCatalogEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectStatusCatalogEntries_ReportingCategory",
                table: "ProjectStatusCatalogEntries");

            migrationBuilder.DropColumn(
                name: "AttentionLevel",
                table: "ProjectStatusCatalogEntries");

            migrationBuilder.DropColumn(
                name: "ReportingCategory",
                table: "ProjectStatusCatalogEntries");
        }
    }
}
