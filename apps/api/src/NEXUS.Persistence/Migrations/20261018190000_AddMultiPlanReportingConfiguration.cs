using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NEXUS.Persistence;

#nullable disable

namespace NEXUS.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018190000_AddMultiPlanReportingConfiguration")]
public partial class AddMultiPlanReportingConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "StatusDate",
            table: "ProjectStatusReports",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql("UPDATE [ProjectStatusReports] SET [StatusDate] = CONVERT(date, [ReportDate]) WHERE [StatusDate] IS NULL;");

        migrationBuilder.AlterColumn<DateTime>(
            name: "StatusDate",
            table: "ProjectStatusReports",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PerformanceSnapshotJson",
            table: "ProjectStatusReports",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ReportingConfigurationVersionSnapshot",
            table: "ProjectStatusReports",
            type: "int",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "IX_Plans_Tenant_Project_Type",
            table: "Plans");

        migrationBuilder.CreateIndex(
            name: "IX_Plans_Tenant_Project_Type_Name",
            table: "Plans",
            columns: new[] { "TenantId", "ProjectId", "PlanType", "Name" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "ProjectReportingConfigurations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReportingFrequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                IncludedSectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ConfiguredBy = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ConfiguredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectReportingConfigurations", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectReportingConfigurations_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectReportingPlanSelections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectReportingConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkingVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BaselineVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IncludeInConsolidatedReporting = table.Column<bool>(type: "bit", nullable: false),
                PlanNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                PlanTypeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectReportingPlanSelections", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectReportingPlanSelections_ProjectReportingConfigurations_ProjectReportingConfigurationId",
                    column: x => x.ProjectReportingConfigurationId,
                    principalTable: "ProjectReportingConfigurations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingConfigurations_Tenant_Project_Current",
            table: "ProjectReportingConfigurations",
            columns: new[] { "TenantId", "ProjectId", "IsCurrent" },
            unique: true,
            filter: "[IsCurrent] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingConfigurations_TenantId_ProjectId_Version",
            table: "ProjectReportingConfigurations",
            columns: new[] { "TenantId", "ProjectId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingConfigurations_ProjectId",
            table: "ProjectReportingConfigurations",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingPlanSelections_TenantId_ProjectReportingConfigurationId_PlanId",
            table: "ProjectReportingPlanSelections",
            columns: new[] { "TenantId", "ProjectReportingConfigurationId", "PlanId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingPlanSelections_TenantId_PlanId",
            table: "ProjectReportingPlanSelections",
            columns: new[] { "TenantId", "PlanId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReportingPlanSelections_ProjectReportingConfigurationId",
            table: "ProjectReportingPlanSelections",
            column: "ProjectReportingConfigurationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProjectReportingPlanSelections");
        migrationBuilder.DropTable(name: "ProjectReportingConfigurations");

        migrationBuilder.DropIndex(name: "IX_Plans_Tenant_Project_Type_Name", table: "Plans");
        migrationBuilder.CreateIndex(
            name: "IX_Plans_Tenant_Project_Type",
            table: "Plans",
            columns: new[] { "TenantId", "ProjectId", "PlanType" },
            unique: true);

        migrationBuilder.DropColumn(name: "PerformanceSnapshotJson", table: "ProjectStatusReports");
        migrationBuilder.DropColumn(name: "ReportingConfigurationVersionSnapshot", table: "ProjectStatusReports");
        migrationBuilder.DropColumn(name: "StatusDate", table: "ProjectStatusReports");
    }
}
