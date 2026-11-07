BEGIN TRANSACTION;
IF EXISTS (
    SELECT 1
    FROM [Resources]
    WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0
    GROUP BY [TenantId], [UserId]
    HAVING COUNT_BIG(*) > 1)
BEGIN
    ;THROW 51000, 'Resource-to-User alignment is blocked by duplicate non-deleted links.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM [Resources] AS r
    WHERE r.[UserId] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM [Users] AS u
          WHERE u.[TenantId] = r.[TenantId] AND u.[Id] = r.[UserId]))
BEGIN
    ;THROW 51000, 'Resource-to-User alignment is blocked by a missing or cross-tenant User link.', 1;
END;

IF EXISTS (
    SELECT 1
    FROM [Resources] AS r
    WHERE r.[TeamId] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM [Teams] AS t
          WHERE t.[TenantId] = r.[TenantId] AND t.[Id] = r.[TeamId]))
BEGIN
    ;THROW 51000, 'Resource-to-Team backfill is blocked by a missing or cross-tenant Team link.', 1;
END;

ALTER TABLE [StakeholderInvitations] ADD [ConsentVersion] nvarchar(64) NULL;

ALTER TABLE [StakeholderInvitations] ADD [ConsentedAt] datetime2 NULL;

ALTER TABLE [StakeholderInvitations] ADD [DeclinedAt] datetime2 NULL;

ALTER TABLE [StakeholderInvitations] ADD [EmailDeliveryStatus] nvarchar(40) NULL;

UPDATE [StakeholderInvitations]
SET [EmailDeliveryStatus] = N'Untracked'
WHERE [EmailDeliveryStatus] IS NULL;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StakeholderInvitations]') AND [c].[name] = N'EmailDeliveryStatus');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [StakeholderInvitations] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [StakeholderInvitations] ALTER COLUMN [EmailDeliveryStatus] nvarchar(40) NOT NULL;

ALTER TABLE [StakeholderInvitations] ADD [EmailSentAt] datetime2 NULL;

ALTER TABLE [StakeholderInvitations] ADD [NotificationsConsent] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [StakeholderInvitations] ADD [ReportsConsent] bit NOT NULL DEFAULT CAST(0 AS bit);

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProjectMembers]') AND [c].[name] = N'ResourceId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ProjectMembers] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [ProjectMembers] ALTER COLUMN [ResourceId] uniqueidentifier NULL;

ALTER TABLE [ProjectMembers] ADD [TeamId] uniqueidentifier NULL;

IF EXISTS (
    SELECT 1
    FROM [ProjectMembers]
    WHERE [ResourceId] IS NULL AND [TeamId] IS NULL)
BEGIN
    ;THROW 51000, 'Project Team alignment is blocked by ProjectMembers with no assignment target.', 1;
END;

DECLARE @TeamMembershipMigrationTime datetime2 = SYSUTCDATETIME();

INSERT INTO [TeamMembers]
    ([Id], [TeamId], [ResourceId], [EffectiveFrom], [EffectiveTo], [IsActive],
     [TenantId], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted], [RowVersion])
SELECT
    NEWID(), r.[TeamId], r.[Id], @TeamMembershipMigrationTime, NULL,
    CASE WHEN r.[IsActive] = 1 AND t.[IsActive] = 1
                   AND r.[IsDeleted] = 0 AND t.[IsDeleted] = 0 THEN 1 ELSE 0 END,
    r.[TenantId], @TeamMembershipMigrationTime, NULL,
    N'Migration:20261031142036_AlignResourceProjectTeamsAndInvitations', NULL,
    CASE WHEN r.[IsDeleted] = 1 OR t.[IsDeleted] = 1 THEN 1 ELSE 0 END,
    CAST(0x AS varbinary(max))
FROM [Resources] AS r
INNER JOIN [Teams] AS t
    ON t.[Id] = r.[TeamId] AND t.[TenantId] = r.[TenantId]
WHERE r.[TeamId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [TeamMembers] AS tm
      WHERE tm.[TenantId] = r.[TenantId]
        AND tm.[TeamId] = r.[TeamId]
        AND tm.[ResourceId] = r.[Id]
        AND tm.[IsDeleted] = 0
        AND tm.[IsActive] = 1
        AND (tm.[EffectiveTo] IS NULL OR tm.[EffectiveTo] > @TeamMembershipMigrationTime));

CREATE UNIQUE INDEX [IX_Resources_TenantId_UserId] ON [Resources] ([TenantId], [UserId]) WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0;

CREATE INDEX [IX_ProjectMembers_TeamId] ON [ProjectMembers] ([TeamId]);

CREATE INDEX [IX_ProjectMembers_TenantId_ProjectId_TeamId_Status] ON [ProjectMembers] ([TenantId], [ProjectId], [TeamId], [Status]);

ALTER TABLE [ProjectMembers] ADD CONSTRAINT [CK_ProjectMembers_ExactlyOneAssignmentTarget] CHECK ((([ResourceId] IS NOT NULL AND [TeamId] IS NULL) OR ([ResourceId] IS NULL AND [TeamId] IS NOT NULL)));

ALTER TABLE [ProjectMembers] ADD CONSTRAINT [FK_ProjectMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Resources] ADD CONSTRAINT [FK_Resources_Users_TenantId_UserId] FOREIGN KEY ([TenantId], [UserId]) REFERENCES [Users] ([TenantId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031142036_AlignResourceProjectTeamsAndInvitations', N'10.0.10');

COMMIT;
GO

