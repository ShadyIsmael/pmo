using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPhaseGateLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrganizationPhaseGateRequirements_Tenant_Id",
                table: "OrganizationPhaseGateRequirements",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrganizationPhaseGateConfigurationVersions_Tenant_Configuration_Id",
                table: "OrganizationPhaseGateConfigurationVersions",
                columns: new[] { "TenantId", "ConfigurationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Documents_TenantId_Id",
                table: "Documents",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "ProjectPhaseGateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcePhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationVersionNumber = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectPhaseGateSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_ProjectPhaseGateSnapshots_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProjectPhaseGateSnapshots_ConfigurationVersionPositive", "[ConfigurationVersionNumber] >= 1");
                    table.CheckConstraint("CK_ProjectPhaseGateSnapshots_DistinctPhases", "[SourcePhase] <> [TargetPhase]");
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateSnapshots_OrganizationPhaseGateConfigurationVersions_TenantId_ConfigurationId_ConfigurationVersionId",
                        columns: x => new { x.TenantId, x.ConfigurationId, x.ConfigurationVersionId },
                        principalTable: "OrganizationPhaseGateConfigurationVersions",
                        principalColumns: new[] { "TenantId", "ConfigurationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateSnapshots_OrganizationPhaseGateConfigurations_TenantId_ConfigurationId",
                        columns: x => new { x.TenantId, x.ConfigurationId },
                        principalTable: "OrganizationPhaseGateConfigurations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateSnapshots_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateSnapshots_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPhaseGateRequirementSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectPhaseGateRequirementSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_ProjectPhaseGateRequirementSnapshots_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProjectPhaseGateRequirementSnapshots_RequirementType", "[RequirementType] IN (N'Mandatory', N'Optional')");
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateRequirementSnapshots_DocumentVersions_TenantId_TemplateId_TemplateVersionId",
                        columns: x => new { x.TenantId, x.TemplateId, x.TemplateVersionId },
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateRequirementSnapshots_OrganizationPhaseGateRequirements_TenantId_SourceRequirementId",
                        columns: x => new { x.TenantId, x.SourceRequirementId },
                        principalTable: "OrganizationPhaseGateRequirements",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateRequirementSnapshots_ProjectPhaseGateSnapshots_TenantId_SnapshotId",
                        columns: x => new { x.TenantId, x.SnapshotId },
                        principalTable: "ProjectPhaseGateSnapshots",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPhaseGateEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectPhaseGateEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateEvidence_DocumentVersions_TenantId_DocumentId_DocumentVersionId",
                        columns: x => new { x.TenantId, x.DocumentId, x.DocumentVersionId },
                        principalTable: "DocumentVersions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateEvidence_Documents_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "Documents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateEvidence_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateEvidence_ProjectPhaseGateRequirementSnapshots_TenantId_RequirementSnapshotId",
                        columns: x => new { x.TenantId, x.RequirementSnapshotId },
                        principalTable: "ProjectPhaseGateRequirementSnapshots",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseGateEvidence_Projects_TenantId_ProjectId",
                        columns: x => new { x.TenantId, x.ProjectId },
                        principalTable: "Projects",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateEvidence_TenantId_DocumentId_DocumentVersionId",
                table: "ProjectPhaseGateEvidence",
                columns: new[] { "TenantId", "DocumentId", "DocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateEvidence_TenantId_OrganizationId",
                table: "ProjectPhaseGateEvidence",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateEvidence_TenantId_ProjectId",
                table: "ProjectPhaseGateEvidence",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseGateEvidence_ActiveRequirement",
                table: "ProjectPhaseGateEvidence",
                columns: new[] { "TenantId", "RequirementSnapshotId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateRequirementSnapshots_TenantId_SourceRequirementId",
                table: "ProjectPhaseGateRequirementSnapshots",
                columns: new[] { "TenantId", "SourceRequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateRequirementSnapshots_TenantId_TemplateId_TemplateVersionId",
                table: "ProjectPhaseGateRequirementSnapshots",
                columns: new[] { "TenantId", "TemplateId", "TemplateVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseGateRequirementSnapshots_Snapshot_Template",
                table: "ProjectPhaseGateRequirementSnapshots",
                columns: new[] { "TenantId", "SnapshotId", "TemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_ConfigurationId_ConfigurationVersionId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "ConfigurationId", "ConfigurationVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_OrganizationId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseGateSnapshots_Project_Transition",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "ProjectId", "OrganizationId", "SourcePhase", "TargetPhase" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectPhaseGateEvidence");

            migrationBuilder.DropTable(
                name: "ProjectPhaseGateRequirementSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrganizationPhaseGateRequirements_Tenant_Id",
                table: "OrganizationPhaseGateRequirements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrganizationPhaseGateConfigurationVersions_Tenant_Configuration_Id",
                table: "OrganizationPhaseGateConfigurationVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Documents_TenantId_Id",
                table: "Documents");

        }
    }
}
