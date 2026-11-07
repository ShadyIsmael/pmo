BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanActivities]') AND [c].[name] = N'TextFormatBold');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [PlanActivities] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [PlanActivities] ALTER COLUMN [TextFormatBold] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanActivities]') AND [c].[name] = N'TextFormatItalic');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [PlanActivities] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [PlanActivities] ALTER COLUMN [TextFormatItalic] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanActivities]') AND [c].[name] = N'TextFormatSpecified');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [PlanActivities] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [PlanActivities] ALTER COLUMN [TextFormatSpecified] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanActivities]') AND [c].[name] = N'TextFormatUnderline');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [PlanActivities] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [PlanActivities] ALTER COLUMN [TextFormatUnderline] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DROP INDEX [IX_PlanCalendars_Tenant_Version_Default] ON [PlanningCalendars];
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanningCalendars]') AND [c].[name] = N'IsDefault');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PlanningCalendars] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [PlanningCalendars] ALTER COLUMN [IsDefault] bit NOT NULL;
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanCalendars_Tenant_Version_Default] ON [PlanningCalendars] ([TenantId], [PlanVersionId], [IsDefault]) WHERE [IsDeleted] = 0 AND [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DROP INDEX [IX_PlanCalendars_Tenant_Version_Default] ON [PlanningCalendars];
    DROP INDEX [IX_PlanCalendars_Tenant_Version_Name] ON [PlanningCalendars];
    DROP INDEX [IX_PlanningCalendars_PlanVersionId] ON [PlanningCalendars];
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanningCalendars]') AND [c].[name] = N'PlanVersionId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PlanningCalendars] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [PlanningCalendars] ALTER COLUMN [PlanVersionId] uniqueidentifier NOT NULL;
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanCalendars_Tenant_Version_Default] ON [PlanningCalendars] ([TenantId], [PlanVersionId], [IsDefault]) WHERE [IsDeleted] = 0 AND [IsDefault] = 1');
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanCalendars_Tenant_Version_Name] ON [PlanningCalendars] ([TenantId], [PlanVersionId], [Name]) WHERE [IsDeleted] = 0');
    CREATE INDEX [IX_PlanningCalendars_PlanVersionId] ON [PlanningCalendars] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanningCalendars]') AND [c].[name] = N'ProjectId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [PlanningCalendars] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [PlanningCalendars] ALTER COLUMN [ProjectId] uniqueidentifier NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanWbsNodes]') AND [c].[name] = N'TextFormatBold');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [PlanWbsNodes] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [PlanWbsNodes] ALTER COLUMN [TextFormatBold] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanWbsNodes]') AND [c].[name] = N'TextFormatItalic');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [PlanWbsNodes] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [PlanWbsNodes] ALTER COLUMN [TextFormatItalic] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanWbsNodes]') AND [c].[name] = N'TextFormatSpecified');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [PlanWbsNodes] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [PlanWbsNodes] ALTER COLUMN [TextFormatSpecified] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PlanWbsNodes]') AND [c].[name] = N'TextFormatUnderline');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [PlanWbsNodes] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [PlanWbsNodes] ALTER COLUMN [TextFormatUnderline] bit NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101213237_SynchronizePlanningPhysicalSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101213237_SynchronizePlanningPhysicalSchema', N'10.0.10');
END;

COMMIT;
GO

