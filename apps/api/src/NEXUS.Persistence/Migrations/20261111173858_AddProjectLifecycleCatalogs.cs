using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXUS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLifecycleCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProjectPhaseGateSnapshots_Project_Transition",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhaseGateSnapshots_DistinctPhases",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropIndex(
                name: "UX_OrganizationPhaseGateConfigurations_ActiveTransition",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

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

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePhaseId",
                table: "ProjectPhaseGateSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetPhaseId",
                table: "ProjectPhaseGateSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations",
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
                    table.ForeignKey(
                        name: "FK_ProjectStatusCatalogEntries_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPhaseTransitionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourcePhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPhaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_ProjectPhaseTransitionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTransitionRules_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTransitionRules_ProjectPhaseCatalogEntries_TenantId_SourcePhaseId",
                        columns: x => new { x.TenantId, x.SourcePhaseId },
                        principalTable: "ProjectPhaseCatalogEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseTransitionRules_ProjectPhaseCatalogEntries_TenantId_TargetPhaseId",
                        columns: x => new { x.TenantId, x.TargetPhaseId },
                        principalTable: "ProjectPhaseCatalogEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatusTransitionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_ProjectStatusTransitionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStatusTransitionRules_Organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "Organizations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStatusTransitionRules_ProjectStatusCatalogEntries_TenantId_SourceStatusId",
                        columns: x => new { x.TenantId, x.SourceStatusId },
                        principalTable: "ProjectStatusCatalogEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStatusTransitionRules_ProjectStatusCatalogEntries_TenantId_TargetStatusId",
                        columns: x => new { x.TenantId, x.TargetStatusId },
                        principalTable: "ProjectStatusCatalogEntries",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                DECLARE @SeededAt datetime2(7) = SYSUTCDATETIME();
                DECLARE @PhaseSeeds TABLE
                (
                    Code nvarchar(64) NOT NULL,
                    NormalizedCode nvarchar(64) NOT NULL,
                    NameEn nvarchar(200) NOT NULL,
                    NameAr nvarchar(200) NOT NULL,
                    DisplayOrder int NOT NULL,
                    IsDefault bit NOT NULL,
                    IsContinuousActivity bit NOT NULL
                );
                INSERT INTO @PhaseSeeds (Code, NormalizedCode, NameEn, NameAr, DisplayOrder, IsDefault, IsContinuousActivity)
                VALUES
                    (N'pre-initiation', N'PRE-INITIATION', N'Pre-Initiation', N'ما قبل البدء', 10, 1, 0),
                    (N'initiation', N'INITIATION', N'Initiation', N'البدء', 20, 0, 0),
                    (N'planning', N'PLANNING', N'Planning', N'التخطيط', 30, 0, 0),
                    (N'execution', N'EXECUTION', N'Execution', N'التنفيذ', 40, 0, 0),
                    (N'monitoring-controlling', N'MONITORING-CONTROLLING', N'Monitoring & Controlling', N'المراقبة والتحكم', 50, 0, 1),
                    (N'closure', N'CLOSURE', N'Closure', N'الإغلاق', 60, 0, 0);

                INSERT INTO ProjectPhaseCatalogEntries
                    (Id, TenantId, OrganizationId, Code, NormalizedCode, NameEn, NameAr, DescriptionEn, DescriptionAr,
                     DisplayOrder, IsDefault, IsActive, IsContinuousActivity, CreatedAt, CreatedBy, IsDeleted)
                SELECT NEWID(), tenant.Id, NULL, seed.Code, seed.NormalizedCode, seed.NameEn, seed.NameAr, NULL, NULL,
                       seed.DisplayOrder, seed.IsDefault, 1, seed.IsContinuousActivity, @SeededAt, N'System', 0
                FROM Tenants AS tenant
                CROSS JOIN @PhaseSeeds AS seed
                WHERE NOT EXISTS
                  (
                      SELECT 1 FROM ProjectPhaseCatalogEntries AS existing
                      WHERE existing.TenantId = tenant.Id AND existing.OrganizationId IS NULL
                        AND existing.NormalizedCode = seed.NormalizedCode AND existing.IsDeleted = 0
                  );

                DECLARE @StatusSeeds TABLE
                (
                    Code nvarchar(64) NOT NULL,
                    NormalizedCode nvarchar(64) NOT NULL,
                    NameEn nvarchar(200) NOT NULL,
                    NameAr nvarchar(200) NOT NULL,
                    DisplayOrder int NOT NULL,
                    IsDefault bit NOT NULL
                );
                INSERT INTO @StatusSeeds (Code, NormalizedCode, NameEn, NameAr, DisplayOrder, IsDefault)
                VALUES
                    (N'not-started', N'NOT-STARTED', N'Not Started', N'لم يبدأ', 10, 1),
                    (N'active', N'ACTIVE', N'Active', N'نشط', 20, 0),
                    (N'on-hold', N'ON-HOLD', N'On Hold', N'معلق', 30, 0),
                    (N'completed', N'COMPLETED', N'Completed', N'مكتمل', 40, 0),
                    (N'cancelled', N'CANCELLED', N'Cancelled', N'ملغى', 50, 0);

                INSERT INTO ProjectStatusCatalogEntries
                    (Id, TenantId, OrganizationId, Code, NormalizedCode, NameEn, NameAr, DescriptionEn, DescriptionAr,
                     DisplayOrder, IsDefault, IsActive, CreatedAt, CreatedBy, IsDeleted)
                SELECT NEWID(), tenant.Id, NULL, seed.Code, seed.NormalizedCode, seed.NameEn, seed.NameAr, NULL, NULL,
                       seed.DisplayOrder, seed.IsDefault, 1, @SeededAt, N'System', 0
                FROM Tenants AS tenant
                CROSS JOIN @StatusSeeds AS seed
                WHERE NOT EXISTS
                  (
                      SELECT 1 FROM ProjectStatusCatalogEntries AS existing
                      WHERE existing.TenantId = tenant.Id AND existing.OrganizationId IS NULL
                        AND existing.NormalizedCode = seed.NormalizedCode AND existing.IsDeleted = 0
                  );

                ;WITH LegacyPhaseValues AS
                (
                    SELECT TenantId, Phase AS LegacyValue FROM Projects WHERE Phase IS NOT NULL
                    UNION ALL
                    SELECT TenantId, SourcePhase FROM OrganizationPhaseGateConfigurations
                    UNION ALL
                    SELECT TenantId, TargetPhase FROM OrganizationPhaseGateConfigurations
                    UNION ALL
                    SELECT TenantId, SourcePhase FROM ProjectPhaseGateSnapshots
                    UNION ALL
                    SELECT TenantId, TargetPhase FROM ProjectPhaseGateSnapshots
                ), NormalizedLegacyPhases AS
                (
                    SELECT TenantId, LOWER(LTRIM(RTRIM(LegacyValue))) AS NormalizedValue,
                           MAX(LTRIM(RTRIM(LegacyValue))) AS DisplayValue
                    FROM LegacyPhaseValues
                    GROUP BY TenantId, LOWER(LTRIM(RTRIM(LegacyValue)))
                ), UnmappedLegacyPhases AS
                (
                    SELECT TenantId, NormalizedValue, DisplayValue,
                           CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), NormalizedValue)), 2), 57)) AS LegacyCode
                    FROM NormalizedLegacyPhases
                    WHERE UPPER(NormalizedValue) NOT IN
                        (N'PRE-INITIATION', N'PRE INITIATION', N'INITIATION', N'PLANNING', N'EXECUTION',
                         N'MONITORING & CONTROLLING', N'MONITORING AND CONTROLLING', N'MONITORING-CONTROLLING', N'CLOSURE')
                )
                INSERT INTO ProjectPhaseCatalogEntries
                    (Id, TenantId, OrganizationId, Code, NormalizedCode, NameEn, NameAr, DescriptionEn, DescriptionAr,
                     DisplayOrder, IsDefault, IsActive, IsContinuousActivity, CreatedAt, CreatedBy, IsDeleted)
                SELECT NEWID(), legacy.TenantId, NULL, legacy.LegacyCode, UPPER(legacy.LegacyCode),
                       CASE WHEN legacy.DisplayValue = N'' THEN N'Unspecified legacy phase' ELSE LEFT(legacy.DisplayValue, 200) END,
                       CASE WHEN legacy.DisplayValue = N'' THEN N'مرحلة تاريخية غير محددة' ELSE LEFT(CONCAT(N'قيمة مرحلة سابقة: ', legacy.DisplayValue), 200) END,
                       N'Legacy phase retained during lifecycle migration.', N'تم الاحتفاظ بقيمة المرحلة السابقة أثناء نقل دورة الحياة.',
                       9000, 0, 1, 0, @SeededAt, N'System', 0
                FROM UnmappedLegacyPhases AS legacy
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM ProjectPhaseCatalogEntries AS existing
                    WHERE existing.TenantId = legacy.TenantId AND existing.OrganizationId IS NULL
                      AND existing.NormalizedCode = UPPER(legacy.LegacyCode) AND existing.IsDeleted = 0
                );

                ;WITH LegacyStatusValues AS
                (
                    SELECT TenantId, Status AS LegacyValue FROM Projects WHERE Status IS NOT NULL
                ), NormalizedLegacyStatuses AS
                (
                    SELECT TenantId, LOWER(LTRIM(RTRIM(LegacyValue))) AS NormalizedValue,
                           MAX(LTRIM(RTRIM(LegacyValue))) AS DisplayValue
                    FROM LegacyStatusValues
                    GROUP BY TenantId, LOWER(LTRIM(RTRIM(LegacyValue)))
                ), UnmappedLegacyStatuses AS
                (
                    SELECT TenantId, NormalizedValue, DisplayValue,
                           CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), NormalizedValue)), 2), 57)) AS LegacyCode
                    FROM NormalizedLegacyStatuses
                    WHERE UPPER(NormalizedValue) NOT IN (N'NOT-STARTED', N'NOT STARTED', N'ACTIVE', N'ON-HOLD', N'ON HOLD', N'COMPLETED', N'CANCELLED')
                )
                INSERT INTO ProjectStatusCatalogEntries
                    (Id, TenantId, OrganizationId, Code, NormalizedCode, NameEn, NameAr, DescriptionEn, DescriptionAr,
                     DisplayOrder, IsDefault, IsActive, CreatedAt, CreatedBy, IsDeleted)
                SELECT NEWID(), legacy.TenantId, NULL, legacy.LegacyCode, UPPER(legacy.LegacyCode),
                       CASE WHEN legacy.DisplayValue = N'' THEN N'Unspecified legacy status' ELSE LEFT(legacy.DisplayValue, 200) END,
                       CASE WHEN legacy.DisplayValue = N'' THEN N'حالة تاريخية غير محددة' ELSE LEFT(CONCAT(N'قيمة حالة سابقة: ', legacy.DisplayValue), 200) END,
                       N'Legacy status retained during lifecycle migration.', N'تم الاحتفاظ بقيمة الحالة السابقة أثناء نقل دورة الحياة.',
                       9000, 0, 1, @SeededAt, N'System', 0
                FROM UnmappedLegacyStatuses AS legacy
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM ProjectStatusCatalogEntries AS existing
                    WHERE existing.TenantId = legacy.TenantId AND existing.OrganizationId IS NULL
                      AND existing.NormalizedCode = UPPER(legacy.LegacyCode) AND existing.IsDeleted = 0
                );

                UPDATE project
                SET ProjectPhaseId = phase.Id
                FROM Projects AS project
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(project.Phase)))
                        WHEN N'PRE-INITIATION' THEN N'PRE-INITIATION' WHEN N'PRE INITIATION' THEN N'PRE-INITIATION'
                        WHEN N'INITIATION' THEN N'INITIATION' WHEN N'PLANNING' THEN N'PLANNING' WHEN N'EXECUTION' THEN N'EXECUTION'
                        WHEN N'MONITORING & CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING AND CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING-CONTROLLING' THEN N'MONITORING-CONTROLLING' WHEN N'CLOSURE' THEN N'CLOSURE'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(project.Phase))))), 2), 57))
                    END AS NormalizedCode
                ) AS phaseMap
                INNER JOIN ProjectPhaseCatalogEntries AS phase
                    ON phase.TenantId = project.TenantId AND phase.OrganizationId IS NULL
                   AND phase.NormalizedCode = UPPER(phaseMap.NormalizedCode) AND phase.IsDeleted = 0
                WHERE project.Phase IS NOT NULL AND project.ProjectPhaseId IS NULL;

                UPDATE project
                SET ProjectStatusId = status.Id
                FROM Projects AS project
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(project.Status)))
                        WHEN N'NOT-STARTED' THEN N'NOT-STARTED' WHEN N'NOT STARTED' THEN N'NOT-STARTED'
                        WHEN N'ACTIVE' THEN N'ACTIVE' WHEN N'ON-HOLD' THEN N'ON-HOLD' WHEN N'ON HOLD' THEN N'ON-HOLD'
                        WHEN N'COMPLETED' THEN N'COMPLETED' WHEN N'CANCELLED' THEN N'CANCELLED'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(project.Status))))), 2), 57))
                    END AS NormalizedCode
                ) AS statusMap
                INNER JOIN ProjectStatusCatalogEntries AS status
                    ON status.TenantId = project.TenantId AND status.OrganizationId IS NULL
                   AND status.NormalizedCode = UPPER(statusMap.NormalizedCode) AND status.IsDeleted = 0
                WHERE project.ProjectStatusId IS NULL;

                UPDATE configuration
                SET SourcePhaseId = sourcePhase.Id, TargetPhaseId = targetPhase.Id
                FROM OrganizationPhaseGateConfigurations AS configuration
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(configuration.SourcePhase)))
                        WHEN N'PRE-INITIATION' THEN N'PRE-INITIATION' WHEN N'PRE INITIATION' THEN N'PRE-INITIATION'
                        WHEN N'INITIATION' THEN N'INITIATION' WHEN N'PLANNING' THEN N'PLANNING' WHEN N'EXECUTION' THEN N'EXECUTION'
                        WHEN N'MONITORING & CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING AND CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING-CONTROLLING' THEN N'MONITORING-CONTROLLING' WHEN N'CLOSURE' THEN N'CLOSURE'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(configuration.SourcePhase))))), 2), 57))
                    END AS NormalizedCode
                ) AS sourceMap
                INNER JOIN ProjectPhaseCatalogEntries AS sourcePhase
                    ON sourcePhase.TenantId = configuration.TenantId AND sourcePhase.OrganizationId IS NULL
                   AND sourcePhase.NormalizedCode = UPPER(sourceMap.NormalizedCode) AND sourcePhase.IsDeleted = 0
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(configuration.TargetPhase)))
                        WHEN N'PRE-INITIATION' THEN N'PRE-INITIATION' WHEN N'PRE INITIATION' THEN N'PRE-INITIATION'
                        WHEN N'INITIATION' THEN N'INITIATION' WHEN N'PLANNING' THEN N'PLANNING' WHEN N'EXECUTION' THEN N'EXECUTION'
                        WHEN N'MONITORING & CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING AND CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING-CONTROLLING' THEN N'MONITORING-CONTROLLING' WHEN N'CLOSURE' THEN N'CLOSURE'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(configuration.TargetPhase))))), 2), 57))
                    END AS NormalizedCode
                ) AS targetMap
                INNER JOIN ProjectPhaseCatalogEntries AS targetPhase
                    ON targetPhase.TenantId = configuration.TenantId AND targetPhase.OrganizationId IS NULL
                   AND targetPhase.NormalizedCode = UPPER(targetMap.NormalizedCode) AND targetPhase.IsDeleted = 0;

                UPDATE snapshot
                SET SourcePhaseId = sourcePhase.Id, TargetPhaseId = targetPhase.Id
                FROM ProjectPhaseGateSnapshots AS snapshot
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(snapshot.SourcePhase)))
                        WHEN N'PRE-INITIATION' THEN N'PRE-INITIATION' WHEN N'PRE INITIATION' THEN N'PRE-INITIATION'
                        WHEN N'INITIATION' THEN N'INITIATION' WHEN N'PLANNING' THEN N'PLANNING' WHEN N'EXECUTION' THEN N'EXECUTION'
                        WHEN N'MONITORING & CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING AND CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING-CONTROLLING' THEN N'MONITORING-CONTROLLING' WHEN N'CLOSURE' THEN N'CLOSURE'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(snapshot.SourcePhase))))), 2), 57))
                    END AS NormalizedCode
                ) AS sourceMap
                INNER JOIN ProjectPhaseCatalogEntries AS sourcePhase
                    ON sourcePhase.TenantId = snapshot.TenantId AND sourcePhase.OrganizationId IS NULL
                   AND sourcePhase.NormalizedCode = UPPER(sourceMap.NormalizedCode) AND sourcePhase.IsDeleted = 0
                CROSS APPLY
                (
                    SELECT CASE UPPER(LTRIM(RTRIM(snapshot.TargetPhase)))
                        WHEN N'PRE-INITIATION' THEN N'PRE-INITIATION' WHEN N'PRE INITIATION' THEN N'PRE-INITIATION'
                        WHEN N'INITIATION' THEN N'INITIATION' WHEN N'PLANNING' THEN N'PLANNING' WHEN N'EXECUTION' THEN N'EXECUTION'
                        WHEN N'MONITORING & CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING AND CONTROLLING' THEN N'MONITORING-CONTROLLING'
                        WHEN N'MONITORING-CONTROLLING' THEN N'MONITORING-CONTROLLING' WHEN N'CLOSURE' THEN N'CLOSURE'
                        ELSE CONCAT(N'LEGACY-', LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), LOWER(LTRIM(RTRIM(snapshot.TargetPhase))))), 2), 57))
                    END AS NormalizedCode
                ) AS targetMap
                INNER JOIN ProjectPhaseCatalogEntries AS targetPhase
                    ON targetPhase.TenantId = snapshot.TenantId AND targetPhase.OrganizationId IS NULL
                   AND targetPhase.NormalizedCode = UPPER(targetMap.NormalizedCode) AND targetPhase.IsDeleted = 0;

                IF EXISTS
                (
                    SELECT TenantId, OrganizationId, SourcePhaseId, TargetPhaseId
                    FROM OrganizationPhaseGateConfigurations
                    WHERE IsDeleted = 0
                    GROUP BY TenantId, OrganizationId, SourcePhaseId, TargetPhaseId
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    ;THROW 51000, 'Project lifecycle migration found duplicate active Phase Gate configurations after phase ID mapping. No configuration history was changed; resolve the duplicate mappings and retry.', 1;
                END;

                IF EXISTS
                (
                    SELECT TenantId, ProjectId, OrganizationId, SourcePhaseId, TargetPhaseId
                    FROM ProjectPhaseGateSnapshots
                    GROUP BY TenantId, ProjectId, OrganizationId, SourcePhaseId, TargetPhaseId
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    ;THROW 51001, 'Project lifecycle migration found duplicate Phase Gate snapshots after phase ID mapping. No snapshot history was changed; resolve the duplicate mappings and retry.', 1;
                END;

                INSERT INTO ProjectPhaseTransitionRules
                    (Id, TenantId, OrganizationId, SourcePhaseId, TargetPhaseId, IsActive, CreatedAt, CreatedBy, IsDeleted)
                SELECT NEWID(), configuration.TenantId, configuration.OrganizationId,
                       configuration.SourcePhaseId, configuration.TargetPhaseId, 1, @SeededAt, configuration.CreatedBy, 0
                FROM OrganizationPhaseGateConfigurations AS configuration
                WHERE configuration.IsDeleted = 0 AND configuration.SourcePhaseId <> configuration.TargetPhaseId
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM ProjectPhaseTransitionRules AS existing
                      WHERE existing.TenantId = configuration.TenantId
                        AND (existing.OrganizationId = configuration.OrganizationId OR (existing.OrganizationId IS NULL AND configuration.OrganizationId IS NULL))
                        AND existing.SourcePhaseId = configuration.SourcePhaseId
                        AND existing.TargetPhaseId = configuration.TargetPhaseId AND existing.IsDeleted = 0
                  );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourcePhaseId",
                table: "ProjectPhaseGateSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetPhaseId",
                table: "ProjectPhaseGateSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_ProjectPhaseId",
                table: "Projects",
                columns: new[] { "TenantId", "ProjectPhaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_ProjectStatusId",
                table: "Projects",
                columns: new[] { "TenantId", "ProjectStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_SourcePhaseId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "SourcePhaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_TargetPhaseId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "TargetPhaseId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseGateSnapshots_Project_Transition",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "ProjectId", "OrganizationId", "SourcePhaseId", "TargetPhaseId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPhaseGateSnapshots_DistinctPhases",
                table: "ProjectPhaseGateSnapshots",
                sql: "[SourcePhaseId] <> [TargetPhaseId]");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPhaseGateConfigurations_TenantId_SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "SourcePhaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPhaseGateConfigurations_TenantId_TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "TargetPhaseId" });

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPhaseGateConfigurations_ActiveTransition",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "OrganizationId", "SourcePhaseId", "TargetPhaseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationPhaseGateConfigurations_DistinctPhaseIds",
                table: "OrganizationPhaseGateConfigurations",
                sql: "[SourcePhaseId] <> [TargetPhaseId]");

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
                name: "IX_ProjectPhaseTransitionRules_TenantId_SourcePhaseId",
                table: "ProjectPhaseTransitionRules",
                columns: new[] { "TenantId", "SourcePhaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseTransitionRules_TenantId_TargetPhaseId",
                table: "ProjectPhaseTransitionRules",
                columns: new[] { "TenantId", "TargetPhaseId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseTransitionRules_Scope_Transition",
                table: "ProjectPhaseTransitionRules",
                columns: new[] { "TenantId", "OrganizationId", "SourcePhaseId", "TargetPhaseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

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

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusTransitionRules_TenantId_SourceStatusId",
                table: "ProjectStatusTransitionRules",
                columns: new[] { "TenantId", "SourceStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusTransitionRules_TenantId_TargetStatusId",
                table: "ProjectStatusTransitionRules",
                columns: new[] { "TenantId", "TargetStatusId" });

            migrationBuilder.CreateIndex(
                name: "UX_ProjectStatusTransitionRules_Scope_Transition",
                table: "ProjectStatusTransitionRules",
                columns: new[] { "TenantId", "OrganizationId", "SourceStatusId", "TargetStatusId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationPhaseGateConfigurations_ProjectPhaseCatalogEntries_TenantId_SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "SourcePhaseId" },
                principalTable: "ProjectPhaseCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationPhaseGateConfigurations_ProjectPhaseCatalogEntries_TenantId_TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "TargetPhaseId" },
                principalTable: "ProjectPhaseCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhaseGateSnapshots_ProjectPhaseCatalogEntries_TenantId_SourcePhaseId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "SourcePhaseId" },
                principalTable: "ProjectPhaseCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhaseGateSnapshots_ProjectPhaseCatalogEntries_TenantId_TargetPhaseId",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "TargetPhaseId" },
                principalTable: "ProjectPhaseCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectPhaseCatalogEntries_TenantId_ProjectPhaseId",
                table: "Projects",
                columns: new[] { "TenantId", "ProjectPhaseId" },
                principalTable: "ProjectPhaseCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectStatusCatalogEntries_TenantId_ProjectStatusId",
                table: "Projects",
                columns: new[] { "TenantId", "ProjectStatusId" },
                principalTable: "ProjectStatusCatalogEntries",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationPhaseGateConfigurations_ProjectPhaseCatalogEntries_TenantId_SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationPhaseGateConfigurations_ProjectPhaseCatalogEntries_TenantId_TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhaseGateSnapshots_ProjectPhaseCatalogEntries_TenantId_SourcePhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhaseGateSnapshots_ProjectPhaseCatalogEntries_TenantId_TargetPhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectPhaseCatalogEntries_TenantId_ProjectPhaseId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatusCatalogEntries_TenantId_ProjectStatusId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectPhaseTransitionRules");

            migrationBuilder.DropTable(
                name: "ProjectStatusTransitionRules");

            migrationBuilder.DropTable(
                name: "ProjectPhaseCatalogEntries");

            migrationBuilder.DropTable(
                name: "ProjectStatusCatalogEntries");

            migrationBuilder.DropIndex(
                name: "IX_Projects_TenantId_ProjectPhaseId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_TenantId_ProjectStatusId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_SourcePhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPhaseGateSnapshots_TenantId_TargetPhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropIndex(
                name: "UX_ProjectPhaseGateSnapshots_Project_Transition",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhaseGateSnapshots_DistinctPhases",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationPhaseGateConfigurations_TenantId_SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationPhaseGateConfigurations_TenantId_TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropIndex(
                name: "UX_OrganizationPhaseGateConfigurations_ActiveTransition",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationPhaseGateConfigurations_DistinctPhaseIds",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropColumn(
                name: "ProjectPhaseId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectStatusId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SourcePhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropColumn(
                name: "TargetPhaseId",
                table: "ProjectPhaseGateSnapshots");

            migrationBuilder.DropColumn(
                name: "SourcePhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.DropColumn(
                name: "TargetPhaseId",
                table: "OrganizationPhaseGateConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "UX_ProjectPhaseGateSnapshots_Project_Transition",
                table: "ProjectPhaseGateSnapshots",
                columns: new[] { "TenantId", "ProjectId", "OrganizationId", "SourcePhase", "TargetPhase" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPhaseGateSnapshots_DistinctPhases",
                table: "ProjectPhaseGateSnapshots",
                sql: "[SourcePhase] <> [TargetPhase]");

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPhaseGateConfigurations_ActiveTransition",
                table: "OrganizationPhaseGateConfigurations",
                columns: new[] { "TenantId", "OrganizationId", "SourcePhase", "TargetPhase" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
