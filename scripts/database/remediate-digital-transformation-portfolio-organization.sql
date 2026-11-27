/*
Approval-gated, targeted repair for the confirmed Digital Transformation Portfolio.
Run locally from the authorized Product Owner SQL session only after explicit approval.
The default is preview only. No IDs are hardcoded; exact names and tenant are resolved at run time.
Before setting @ApplyApproved = 1, verify the active API Development connection resolves to this exact target and enter the approval reference.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ServerName nvarchar(128) = CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));
DECLARE @DatabaseName sysname = DB_NAME();
SELECT @ServerName AS ServerName, @DatabaseName AS DatabaseName, DB_ID() AS DatabaseId;
IF UPPER(@ServerName) <> N'SHADY' OR @DatabaseName <> N'NexusPmo'
    THROW 51001, 'Remediation stopped: expected Development database Shady/NexusPmo.', 1;

DECLARE @ApplyApproved bit = 0;
DECLARE @ProductOwnerApprovalReference nvarchar(200) = NULL;

DECLARE @PortfolioCandidates TABLE (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, OrganizationId uniqueidentifier NULL);
INSERT INTO @PortfolioCandidates (Id, TenantId, OrganizationId)
SELECT Id, TenantId, OrganizationId
FROM dbo.Portfolios
WHERE Name = N'Digital Transformation' AND IsDeleted = 0;

IF (SELECT COUNT(*) FROM @PortfolioCandidates) <> 1
    THROW 51002, 'Remediation stopped: expected exactly one active Digital Transformation Portfolio across the target database.', 1;

DECLARE @PortfolioId uniqueidentifier;
DECLARE @TenantId uniqueidentifier;
DECLARE @CurrentOrganizationId uniqueidentifier;
SELECT @PortfolioId = Id, @TenantId = TenantId, @CurrentOrganizationId = OrganizationId
FROM @PortfolioCandidates;

DECLARE @OrganizationCandidates TABLE (Id uniqueidentifier NOT NULL, Name nvarchar(300) NOT NULL);
INSERT INTO @OrganizationCandidates (Id, Name)
SELECT Id, Name
FROM dbo.Organizations
WHERE TenantId = @TenantId
  AND Name = N'NEXUS Group'
  AND IsActive = 1
  AND IsDeleted = 0;

IF (SELECT COUNT(*) FROM @OrganizationCandidates) <> 1
    THROW 51003, 'Remediation stopped: expected exactly one active NEXUS Group Organization in the Portfolio tenant.', 1;

DECLARE @OrganizationId uniqueidentifier;
SELECT @OrganizationId = Id FROM @OrganizationCandidates;

SELECT p.Id AS PortfolioId, p.Name AS PortfolioName, p.TenantId,
       p.OrganizationId AS CurrentOrganizationId, o.Name AS CurrentOrganizationName,
       @OrganizationId AS ProposedOrganizationId, target.Name AS ProposedOrganizationName,
       @ApplyApproved AS ApplyApproved
FROM dbo.Portfolios AS p
LEFT JOIN dbo.Organizations AS o ON o.Id = p.OrganizationId AND o.TenantId = p.TenantId
JOIN @OrganizationCandidates AS target ON target.Id = @OrganizationId
WHERE p.Id = @PortfolioId AND p.TenantId = @TenantId;

IF @CurrentOrganizationId = @OrganizationId
    RETURN;
IF @CurrentOrganizationId IS NOT NULL
    THROW 51004, 'Remediation stopped: the Portfolio already has a different Organization; review manually.', 1;
IF @ApplyApproved = 0
    RETURN;
IF NULLIF(LTRIM(RTRIM(@ProductOwnerApprovalReference)), N'') IS NULL
    THROW 51005, 'Remediation stopped: provide the explicit Product Owner approval reference.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE dbo.Portfolios
    SET OrganizationId = @OrganizationId,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ProductOwnerApprovalReference
    WHERE Id = @PortfolioId
      AND TenantId = @TenantId
      AND OrganizationId IS NULL
      AND IsDeleted = 0;

    IF @@ROWCOUNT <> 1
        THROW 51006, 'Remediation stopped: the Portfolio changed after preview; no update was applied.', 1;

    COMMIT TRANSACTION;
    SELECT p.Id AS PortfolioId, p.Name AS PortfolioName, p.TenantId,
           p.OrganizationId, o.Name AS OrganizationName
    FROM dbo.Portfolios AS p
    JOIN dbo.Organizations AS o ON o.Id = p.OrganizationId AND o.TenantId = p.TenantId
    WHERE p.Id = @PortfolioId AND p.TenantId = @TenantId;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;