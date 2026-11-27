/*
Read-only ownership audit for the Development PMO database.
Run locally from the authorized Product Owner SQL session. No data is changed.
The result is a set of rows requiring review; an empty result set means no matching ownership anomalies were found in that category.
*/
SET NOCOUNT ON;

DECLARE @ServerName nvarchar(128) = CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));
DECLARE @DatabaseName sysname = DB_NAME();
SELECT @ServerName AS ServerName, @DatabaseName AS DatabaseName, DB_ID() AS DatabaseId;
IF UPPER(@ServerName) <> N'SHADY' OR @DatabaseName <> N'NexusPmo'
    THROW 51000, 'Ownership audit stopped: expected Development database Shady/NexusPmo.', 1;

-- Portfolios: null owner, unresolved reference, tenant mismatch, or inactive/deleted Organization.
SELECT
    p.Id AS PortfolioId,
    p.TenantId,
    p.Name AS PortfolioName,
    p.IsDeleted AS PortfolioIsDeleted,
    p.OrganizationId,
    o.Name AS OrganizationName,
    o.IsActive AS OrganizationIsActive,
    o.IsDeleted AS OrganizationIsDeleted,
    CASE
        WHEN p.OrganizationId IS NULL THEN N'Missing OrganizationId'
        WHEN o.Id IS NULL THEN N'Organization missing or tenant mismatch'
        WHEN o.IsDeleted = 1 THEN N'Organization deleted'
        WHEN o.IsActive = 0 THEN N'Organization inactive'
    END AS OwnershipIssue
FROM dbo.Portfolios AS p
LEFT JOIN dbo.Organizations AS o
    ON o.Id = p.OrganizationId AND o.TenantId = p.TenantId
WHERE p.OrganizationId IS NULL
   OR o.Id IS NULL
   OR o.IsDeleted = 1
   OR o.IsActive = 0
ORDER BY p.TenantId, p.Name, p.Id;

-- Programs inherit their Organization from the owning Portfolio.
SELECT
    g.Id AS ProgramId,
    g.TenantId,
    g.Name AS ProgramName,
    g.IsDeleted AS ProgramIsDeleted,
    g.PortfolioId,
    p.Name AS PortfolioName,
    p.OrganizationId,
    o.Name AS OrganizationName,
    CASE
        WHEN p.Id IS NULL THEN N'Portfolio missing or tenant mismatch'
        WHEN p.IsDeleted = 1 THEN N'Portfolio deleted'
        WHEN p.OrganizationId IS NULL THEN N'Portfolio has no Organization'
        WHEN o.Id IS NULL THEN N'Organization missing or tenant mismatch'
        WHEN o.IsDeleted = 1 THEN N'Organization deleted'
        WHEN o.IsActive = 0 THEN N'Organization inactive'
    END AS OwnershipIssue
FROM dbo.Programs AS g
LEFT JOIN dbo.Portfolios AS p
    ON p.Id = g.PortfolioId AND p.TenantId = g.TenantId
LEFT JOIN dbo.Organizations AS o
    ON o.Id = p.OrganizationId AND o.TenantId = p.TenantId
WHERE p.Id IS NULL
   OR p.IsDeleted = 1
   OR p.OrganizationId IS NULL
   OR o.Id IS NULL
   OR o.IsDeleted = 1
   OR o.IsActive = 0
ORDER BY g.TenantId, g.Name, g.Id;

-- Projects resolve ownership from Program -> Portfolio or from a direct Portfolio.
SELECT
    pr.Id AS ProjectId,
    pr.TenantId,
    pr.Name AS ProjectName,
    pr.IsDeleted AS ProjectIsDeleted,
    pr.ProgramId,
    g.Name AS ProgramName,
    g.PortfolioId AS ProgramPortfolioId,
    pr.PortfolioId AS ProjectPortfolioId,
    ownerPortfolio.Id AS ResolvedPortfolioId,
    ownerPortfolio.Name AS ResolvedPortfolioName,
    ownerPortfolio.OrganizationId,
    o.Name AS OrganizationName,
    CASE
        WHEN pr.ProgramId IS NOT NULL AND g.Id IS NULL THEN N'Program missing or tenant mismatch'
        WHEN pr.ProgramId IS NOT NULL AND g.IsDeleted = 1 THEN N'Program deleted'
        WHEN pr.ProgramId IS NOT NULL AND pr.PortfolioId IS NOT NULL AND pr.PortfolioId <> g.PortfolioId THEN N'Project and Program Portfolios disagree'
        WHEN pr.ProgramId IS NULL AND pr.PortfolioId IS NULL THEN N'No Portfolio or Program parent'
        WHEN ownerPortfolio.Id IS NULL THEN N'Portfolio missing or tenant mismatch'
        WHEN ownerPortfolio.IsDeleted = 1 THEN N'Portfolio deleted'
        WHEN ownerPortfolio.OrganizationId IS NULL THEN N'Portfolio has no Organization'
        WHEN o.Id IS NULL THEN N'Organization missing or tenant mismatch'
        WHEN o.IsDeleted = 1 THEN N'Organization deleted'
        WHEN o.IsActive = 0 THEN N'Organization inactive'
    END AS OwnershipIssue
FROM dbo.Projects AS pr
LEFT JOIN dbo.Programs AS g
    ON g.Id = pr.ProgramId AND g.TenantId = pr.TenantId
LEFT JOIN dbo.Portfolios AS directPortfolio
    ON directPortfolio.Id = pr.PortfolioId AND directPortfolio.TenantId = pr.TenantId
LEFT JOIN dbo.Portfolios AS programPortfolio
    ON programPortfolio.Id = g.PortfolioId AND programPortfolio.TenantId = g.TenantId
OUTER APPLY (SELECT CASE WHEN pr.ProgramId IS NOT NULL THEN programPortfolio.Id ELSE directPortfolio.Id END AS Id,
                    CASE WHEN pr.ProgramId IS NOT NULL THEN programPortfolio.Name ELSE directPortfolio.Name END AS Name,
                    CASE WHEN pr.ProgramId IS NOT NULL THEN programPortfolio.IsDeleted ELSE directPortfolio.IsDeleted END AS IsDeleted,
                    CASE WHEN pr.ProgramId IS NOT NULL THEN programPortfolio.OrganizationId ELSE directPortfolio.OrganizationId END AS OrganizationId) AS ownerPortfolio
LEFT JOIN dbo.Organizations AS o
    ON o.Id = ownerPortfolio.OrganizationId AND o.TenantId = pr.TenantId
WHERE (pr.ProgramId IS NOT NULL AND (g.Id IS NULL OR g.IsDeleted = 1))
   OR (pr.ProgramId IS NOT NULL AND pr.PortfolioId IS NOT NULL AND pr.PortfolioId <> g.PortfolioId)
   OR (pr.ProgramId IS NULL AND pr.PortfolioId IS NULL)
   OR ownerPortfolio.Id IS NULL
   OR ownerPortfolio.IsDeleted = 1
   OR ownerPortfolio.OrganizationId IS NULL
   OR o.Id IS NULL
   OR o.IsDeleted = 1
   OR o.IsActive = 0
ORDER BY pr.TenantId, pr.Name, pr.Id;