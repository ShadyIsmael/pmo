/*
  FOPX PM — Multi-Tenant Configuration Settings read-only preflight

  PREPARED ONLY. This file has not been executed.
  Run only after separate owner authorization for Development database reads,
  after the repository EF gate verifies the effective API Development target.

  Replace both placeholders below with the exact metadata reported by the gate.
  This script fails before diagnostics if the current connection does not match.
  It contains no connection string, DDL, DML, migration application, or USE.
  Refresh the source migration list whenever the repository migration chain changes.
*/
SET NOCOUNT ON;

DECLARE @ExpectedServer sysname = N'<replace-with-verified-SERVERPROPERTY-ServerName>';
DECLARE @ExpectedDatabase sysname = N'<replace-with-verified-DB_NAME>';
DECLARE @ActualServer sysname = CONVERT(sysname, SERVERPROPERTY('ServerName'));

IF @ExpectedServer = N'<replace-with-verified-SERVERPROPERTY-ServerName>'
   OR @ExpectedDatabase = N'<replace-with-verified-DB_NAME>'
   OR @ActualServer IS NULL
   OR DB_NAME() IS NULL
   OR @ActualServer <> @ExpectedServer
   OR DB_NAME() <> @ExpectedDatabase
BEGIN
    THROW 51000, 'Preflight stopped: replace the expected target values and verify this Development connection first.', 1;
END;

/* 1. Target identity, SQL Server engine and database compatibility/recovery metadata. */
SELECT
    @ActualServer AS ServerName,
    CONVERT(sysname, SERVERPROPERTY('MachineName')) AS MachineName,
    CONVERT(sysname, SERVERPROPERTY('InstanceName')) AS InstanceName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductLevel')) AS ProductLevel,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition,
    CONVERT(int, SERVERPROPERTY('EngineEdition')) AS EngineEdition,
    DB_NAME() AS DatabaseName,
    DB_ID() AS DatabaseId,
    d.state_desc AS DatabaseState,
    d.compatibility_level AS CompatibilityLevel,
    d.recovery_model_desc AS RecoveryModel
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

/* 2. Applied EF migrations. If the history table is absent, report that explicitly. */
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT
        N'dbo.__EFMigrationsHistory is absent in the verified current database' AS MigrationHistoryStatus,
        CAST(NULL AS nvarchar(150)) AS MigrationId,
        CAST(NULL AS nvarchar(32)) AS ProductVersion;
END
ELSE
BEGIN
    EXEC sys.sp_executesql N'
        SELECT MigrationId, ProductVersion
        FROM dbo.__EFMigrationsHistory
        ORDER BY MigrationId;';
END;

/* 3. Compare current source migration IDs with the target history.
      Refresh this embedded source list immediately before an authorized run. */
DECLARE @MigrationComparisonSql nvarchar(max) = N'
;WITH SourceMigrations AS
(
    SELECT v.MigrationId
    FROM (VALUES
        (N''20261010182322_NexusUpdatedBaseline''),
        (N''20261011205628_AddDirectoryIntegration''),
        (N''20261012103001_AddLocalPasswordLifecycle''),
        (N''20261012110228_AddNotificationDeliveryFoundation''),
        (N''20261015131616_AddAdministrativeManagerHandover''),
        (N''20261015150917_MakeHandoverSnapshotPreviousManagerNullable''),
        (N''20261018084343_AddProjectPublicId''),
        (N''20261018190000_AddMultiPlanReportingConfiguration''),
        (N''20261021120000_AddProjectChangeRequestApprovals''),
        (N''20261021130000_AddChangeRequestApprovalModelIndexes''),
        (N''20261021160000_AddGovernanceRecoveryPublicIds''),
        (N''20261026121145_AddResourceWorkingCapacity''),
        (N''20261026140000_AllowPlanAssignmentReassignmentAfterDelete''),
        (N''20261026150000_AddWorkingCalendarDayRules''),
        (N''20261026160000_AddPlannedPaymentLifecycle''),
        (N''20261026170000_AddPaymentTenantScopedRelationships''),
        (N''20261027174321_AddStatusReportActionsSupport''),
        (N''20261027202734_AddStatusReportPaymentSnapshotFields''),
        (N''20261028084103_AddMeetingActionWorkItemLink''),
        (N''20261028130000_AddMeetingLifecycleTracking''),
        (N''20261028150000_SeparateMeetingInvitationAndAttendanceStatuses''),
        (N''20261028170000_AddProjectAuditTrail''),
        (N''20261029153000_AddProjectFinanceApprovalMode'')
    ) AS v(MigrationId)
)
SELECT CAST(N''SOURCE'' AS nvarchar(20)) AS ComparisonKind,
       s.MigrationId,
       CAST(CASE WHEN h.MigrationId IS NULL THEN N''MISSING_FROM_HISTORY'' ELSE N''APPLIED'' END AS nvarchar(32)) AS Status
FROM SourceMigrations AS s
LEFT JOIN dbo.__EFMigrationsHistory AS h ON h.MigrationId = s.MigrationId
UNION ALL
SELECT CAST(N''HISTORY_ONLY'' AS nvarchar(20)),
       h.MigrationId,
       CAST(N''MISSING_FROM_SOURCE'' AS nvarchar(32))
FROM dbo.__EFMigrationsHistory AS h
LEFT JOIN SourceMigrations AS s ON s.MigrationId = h.MigrationId
WHERE s.MigrationId IS NULL
ORDER BY ComparisonKind, MigrationId;';

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql @MigrationComparisonSql;
END
ELSE
BEGIN
    SELECT
        N'NOT COMPARABLE — migration history table is absent' AS ComparisonKind,
        CAST(NULL AS nvarchar(150)) AS MigrationId,
        N'OWNER REVIEW REQUIRED' AS Status;
END;

/* 4. Physical columns for settings and referenced principal tables. */
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrdinal,
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS SqlType,
    c.max_length AS MaxLengthBytes,
    c.precision AS NumericPrecision,
    c.scale AS NumericScale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    c.is_computed AS IsComputed,
    c.collation_name AS CollationName
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.name IN (N'ConfigurationSettings', N'OrganizationSettingsGrants', N'Organizations', N'Users')
ORDER BY s.name, t.name, c.column_id;

/* 5. Primary/alternate keys and indexes, including active filters and key-column order. */
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    ic.key_ordinal AS KeyOrdinal,
    ic.is_included_column AS IsIncludedColumn,
    c.name AS ColumnName
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.name IN (N'ConfigurationSettings', N'OrganizationSettingsGrants', N'Organizations', N'Users')
  AND i.name IS NOT NULL
  AND i.is_hypothetical = 0
ORDER BY s.name, t.name, i.name, ic.key_ordinal, ic.index_column_id;

/* 6. Check constraints and composite FK column mappings for affected/principal tables. */
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    cc.name AS ConstraintName,
    cc.definition AS Definition,
    cc.is_disabled AS IsDisabled,
    cc.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints AS cc
JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE t.name IN (N'ConfigurationSettings', N'OrganizationSettingsGrants', N'Organizations', N'Users')
ORDER BY s.name, t.name, cc.name;

SELECT
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    fk.name AS ForeignKeyName,
    fkc.constraint_column_id AS ColumnOrdinal,
    pc.name AS ParentColumn,
    rs.name AS ReferencedSchema,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn,
    fk.delete_referential_action_desc AS OnDelete,
    fk.is_disabled AS IsDisabled,
    fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
WHERE pt.name IN (N'ConfigurationSettings', N'OrganizationSettingsGrants', N'Organizations', N'Users')
   OR rt.name IN (N'ConfigurationSettings', N'OrganizationSettingsGrants', N'Organizations', N'Users')
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

/* 7. Current database file paths for comparison with backup media location. */
SELECT name AS LogicalFileName, type_desc AS FileType, physical_name AS PhysicalPath, state_desc AS FileState
FROM sys.database_files
ORDER BY file_id;

/* 8. SQL Server backup metadata. This is not proof that media is independent or restorable. */
SELECT TOP (50)
    bs.database_name AS DatabaseName,
    bs.type AS BackupType,
    bs.backup_start_date AS BackupStartDate,
    bs.backup_finish_date AS BackupFinishDate,
    bs.is_copy_only AS IsCopyOnly,
    bs.has_backup_checksums AS HasBackupChecksums,
    bs.backup_size AS BackupSizeBytes,
    bs.first_lsn AS FirstLsn,
    bs.last_lsn AS LastLsn,
    bs.database_backup_lsn AS DatabaseBackupLsn,
    bmf.family_sequence_number AS MediaFamilySequence,
    bmf.physical_device_name AS BackupMediaPath
FROM msdb.dbo.backupset AS bs
JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id
WHERE bs.database_name = DB_NAME()
  AND bs.type IN (N'D', N'I', N'L')
ORDER BY bs.backup_finish_date DESC, bmf.family_sequence_number;

/*
  Owner follow-up (separate authorization): verify backup media with RESTORE VERIFYONLY,
  then use an isolated restore when recovery evidence is required. Do not run either as
  part of this script or against Production without separate authorization.
*/
