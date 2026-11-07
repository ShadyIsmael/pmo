BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    ALTER TABLE [Notifications] ADD [ReferencePublicId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    ALTER TABLE [Meetings] ADD [PublicId] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    ALTER TABLE [MeetingMinutesComments] ADD [PublicId] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Meetings_PublicId] ON [Meetings] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MeetingMinutesComments_PublicId] ON [MeetingMinutesComments] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105214717_AddMeetingNotificationPublicIdentifiers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105214717_AddMeetingNotificationPublicIdentifiers', N'10.0.10');
END;

COMMIT;
GO

