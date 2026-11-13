using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationPhaseGateConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_DocumentVersions_TenantId_DocumentId_Id",
                table: "DocumentVersions",
                columns: new[] { "TenantId", "DocumentId", "Id" });

            migrationBuilder.CreateTable(
                name: "OrganizationPhaseGateConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OrganizationPhaseGateConfigurations", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationPhaseGateConfigurations_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_OrganizationPhaseGateConfigurations_CurrentVersionPositive", "[CurrentVersionNumber] >= 1");
                    table.CheckConstraint("CK_OrganizationPhaseGateConfigurations_DistinctPhases", "[SourcePhase] <> [TargetPhase]");
                    table.ForeignKey(
                        name: "FK_OrganizationPhaseGateConfigurations_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationPhaseGateConfigurationVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OrganizationPhaseGateConfigurationVersions", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationPhaseGateConfigurationVersions_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_OrganizationPhaseGateConfigurationVersions_VersionPositive", "[VersionNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_OrganizationPhaseGateConfigurationVersions_OrganizationPhaseGateConfigurations_TenantId_ConfigurationId",
                        columns: x => new { x.TenantId, x.ConfigurationId },
                        principalTable: "OrganizationPhaseGateConfigurations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationPhaseGateRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TemplateVersionLabel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_OrganizationPhaseGateRequirements", x => x.Id);
                    table.CheckConstraint("CK_OrganizationPhaseGateRequirements_RequirementType", "[RequirementType] IN (N'Mandatory', N'Optional')");
                    table.ForeignKey(
                        name: "FK_OrganizationPhaseGateRequirements_DocumentVersions_TenantId_TemplateId_TemplateVersionId",
                        columns: x => new { x.TenantId, x.TemplateId, x.TemplateVersionId },
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationPhaseGateRequirements_OrganizationPhaseGateConfigurationVersions_TenantId_ConfigurationVersionId",
                        columns: x => new { x.TenantId, x.ConfigurationVersionId },
                        principalTable: "OrganizationPhaseGateConfigurationVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPhaseGateConfigurations_ActiveTransition",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "OrganizationId", "SourcePhase", "TargetPhase" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPhaseGateConfigurationVersions_Configuration_Version",
                table: "OrganizationPhaseGateConfigurationVersions",
                columns: new[] { "TenantId", "ConfigurationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPhaseGateRequirements_TenantId_TemplateId_TemplateVersionId",
                table: "OrganizationPhaseGateRequirements",
                columns: new[] { "TenantId", "TemplateId", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPhaseGateRequirements_Version_Template",
                table: "OrganizationPhaseGateRequirements",
                columns: new[] { "TenantId", "ConfigurationVersionId", "TemplateId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationPhaseGateRequirements");

            migrationBuilder.DropTable(
                name: "OrganizationPhaseGateConfigurationVersions");

            migrationBuilder.DropTable(
                name: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DocumentVersions_TenantId_DocumentId_Id",
                table: "DocumentVersions");
        }
    }
}
