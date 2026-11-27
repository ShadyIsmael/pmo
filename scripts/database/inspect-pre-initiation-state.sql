/*
   Read-only Development diagnostics for Organization-scoped Pre-Initiation phases.
   Run only through the Product Owner's authorized Development SQL connection.
   This file contains identity guards and SELECT statements only.
*/
SET NOCOUNT ON;

DECLARE @ServerName nvarchar(128) = CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));
DECLARE @DatabaseName sysname = DB_NAME();

IF UPPER(COALESCE(@ServerName, N'')) <> N'SHADY'
   AND UPPER(COALESCE(@ServerName, N'')) NOT LIKE N'SHADY\%'
    THROW 51000, 'Stopped: the connected SQL Server is not the approved Development server Shady.', 1;

IF @DatabaseName <> N'NexusPmo'
    THROW 51001, 'Stopped: the connected database is not the approved Development database NexusPmo.', 1;

SELECT
    @ServerName AS ServerName,
    @DatabaseName AS DatabaseName,
    DB_ID() AS DatabaseId,
    CONVERT(nvarchar(60), DATABASEPROPERTYEX(@DatabaseName, 'Status')) AS DatabaseStatus;

SELECT
    TenantId,
    OrganizationId,
    CASE WHEN OrganizationId IS NULL THEN N'Tenant scope' ELSE N'Organization scope' END AS ScopeType,
    Id AS PhaseId,
    Code,
    NormalizedCode,
    NameEn,
    NameAr,
    DisplayOrder,
    IsActive,
    IsDefault,
    IsDeleted,
    CreatedAt,
    UpdatedAt
FROM dbo.ProjectPhaseCatalogEntries
WHERE NormalizedCode = N'PRE-INITIATION'
   OR NameEn = N'Pre-Initiation'
   OR NameAr = N'مرحلة ما قبل البدء'
ORDER BY TenantId, OrganizationId, IsDeleted, IsActive DESC, CreatedAt;

SELECT
    organization.TenantId,
    organization.Id AS OrganizationId,
    organization.Name AS OrganizationName,
    organization.IsActive AS OrganizationIsActive,
    organization.IsDeleted AS OrganizationIsDeleted,
    COUNT(phase.Id) AS NonDeletedCandidateRows,
    SUM(CASE WHEN phase.NormalizedCode = N'PRE-INITIATION'
                  AND phase.IsActive = 1 AND phase.IsDefault = 1
                  AND phase.NameEn = N'Pre-Initiation'
                  AND phase.NameAr = N'مرحلة ما قبل البدء'
                  AND phase.DisplayOrder = 0
             THEN 1 ELSE 0 END) AS ActiveCanonicalDefaultRows
FROM dbo.Organizations AS organization
LEFT JOIN dbo.ProjectPhaseCatalogEntries AS phase
    ON phase.TenantId = organization.TenantId
   AND phase.OrganizationId = organization.Id
   AND (phase.NormalizedCode = N'PRE-INITIATION'
        OR phase.NameEn = N'Pre-Initiation'
        OR phase.NameAr = N'مرحلة ما قبل البدء')
   AND phase.IsDeleted = 0
GROUP BY organization.TenantId, organization.Id, organization.Name, organization.IsActive, organization.IsDeleted
ORDER BY organization.TenantId, organization.IsDeleted, organization.IsActive DESC, organization.Name;

WITH ProjectOwner AS
(
    SELECT
        project.Id AS ProjectId,
        project.TenantId,
        project.CreatedAt,
        project.Code,
        project.Name,
        project.PublicId,
        project.ProgramId,
        project.PortfolioId,
        project.DepartmentId,
        project.ProjectPhaseId,
        project.Phase AS LegacyPhase,
        CASE WHEN project.ProgramId IS NOT NULL
                  AND (program.Id IS NULL
                       OR (project.PortfolioId IS NOT NULL AND project.PortfolioId <> program.PortfolioId))
             THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS ProgramOwnershipValid,
        owningOrganization.Id AS OwningOrganizationId
    FROM dbo.Projects AS project
    LEFT JOIN dbo.Programs AS program
        ON program.Id = project.ProgramId
       AND program.TenantId = project.TenantId
       AND program.IsDeleted = 0
    LEFT JOIN dbo.Portfolios AS portfolio
        ON portfolio.Id = CASE WHEN project.ProgramId IS NOT NULL THEN program.PortfolioId ELSE project.PortfolioId END
       AND portfolio.TenantId = project.TenantId
       AND portfolio.IsDeleted = 0
    LEFT JOIN dbo.Departments AS department
        ON department.Id = project.DepartmentId
       AND department.TenantId = project.TenantId
       AND department.IsDeleted = 0
       AND department.IsActive = 1
    LEFT JOIN dbo.BusinessUnits AS businessUnit
        ON businessUnit.Id = department.BusinessUnitId
       AND businessUnit.TenantId = project.TenantId
       AND businessUnit.IsDeleted = 0
       AND businessUnit.IsActive = 1
    LEFT JOIN dbo.Organizations AS owningOrganization
        ON owningOrganization.Id = CASE
               WHEN project.ProgramId IS NOT NULL
                    AND (program.Id IS NULL
                         OR (project.PortfolioId IS NOT NULL AND project.PortfolioId <> program.PortfolioId))
               THEN NULL
               ELSE COALESCE(portfolio.OrganizationId, businessUnit.OrganizationId)
           END
       AND owningOrganization.TenantId = project.TenantId
       AND owningOrganization.IsDeleted = 0
       AND owningOrganization.IsActive = 1
    WHERE project.IsDeleted = 0
)
SELECT
    owner.ProjectId,
    owner.TenantId,
    owner.CreatedAt,
    owner.Code,
    owner.Name,
    owner.PublicId,
    owner.ProgramId,
    owner.PortfolioId,
    owner.DepartmentId,
    owner.ProgramOwnershipValid,
    owner.OwningOrganizationId,
    owner.ProjectPhaseId,
    owner.LegacyPhase,
    phase.OrganizationId AS PhaseOrganizationId,
    phase.Code AS PhaseCode,
    phase.NormalizedCode AS PhaseNormalizedCode,
    phase.NameEn AS PhaseNameEn,
    phase.NameAr AS PhaseNameAr,
    phase.IsActive AS PhaseIsActive,
    phase.IsDefault AS PhaseIsDefault,
    phase.IsDeleted AS PhaseIsDeleted,
    CASE WHEN phase.Id IS NOT NULL AND ISNULL(owner.LegacyPhase, N'') <> phase.Code
         THEN CONVERT(bit, 0) ELSE CONVERT(bit, 1) END AS LegacyPhaseProjectionMatches
FROM ProjectOwner AS owner
LEFT JOIN dbo.ProjectPhaseCatalogEntries AS phase
    ON phase.Id = owner.ProjectPhaseId
   AND phase.TenantId = owner.TenantId
WHERE owner.ProjectPhaseId IS NULL
   OR owner.ProgramOwnershipValid = 0
   OR owner.OwningOrganizationId IS NULL
   OR phase.Id IS NULL
   OR phase.OrganizationId IS NULL
   OR phase.OrganizationId <> owner.OwningOrganizationId
   OR phase.IsActive = 0
   OR phase.IsDeleted = 1
   OR (phase.Id IS NOT NULL AND ISNULL(owner.LegacyPhase, N'') <> phase.Code)
ORDER BY owner.CreatedAt DESC;
