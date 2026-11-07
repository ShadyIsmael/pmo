BEGIN TRANSACTION;
ALTER TABLE [Notifications] ADD [OrganizationId] uniqueidentifier NULL;

ALTER TABLE [NotificationDeliveries] ADD [AcceptedAt] datetime2 NULL;

CREATE TABLE [EmailNotificationProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [Scope] nvarchar(16) NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [OwnerId] uniqueidentifier NULL,
    [OrganizationId] uniqueidentifier NULL,
    [Enabled] bit NOT NULL,
    [Host] nvarchar(255) NOT NULL,
    [Port] int NOT NULL,
    [SecurityMode] nvarchar(32) NOT NULL,
    [AuthenticationMode] nvarchar(32) NOT NULL,
    [UserName] nvarchar(512) NULL,
    [ProtectedPassword] nvarchar(max) NULL,
    [FromAddress] nvarchar(320) NOT NULL,
    [FromName] nvarchar(200) NOT NULL,
    [ReplyToAddress] nvarchar(320) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(100) NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmailNotificationProfiles] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_EmailNotificationProfiles_AuthenticationMode] CHECK ([AuthenticationMode] IN (N'UsernamePassword', N'TrustedRelay')),
    CONSTRAINT [CK_EmailNotificationProfiles_EnabledProfile] CHECK ([Enabled] = 0 OR (LEN(LTRIM(RTRIM([Host]))) > 0 AND LEN(LTRIM(RTRIM([FromAddress]))) > 0 AND (([AuthenticationMode] = N'UsernamePassword' AND LEN(LTRIM(RTRIM([UserName]))) > 0 AND [ProtectedPassword] IS NOT NULL) OR ([AuthenticationMode] = N'TrustedRelay' AND [UserName] IS NULL AND [ProtectedPassword] IS NULL)))),
    CONSTRAINT [CK_EmailNotificationProfiles_Port] CHECK ([Port] BETWEEN 1 AND 65535),
    CONSTRAINT [CK_EmailNotificationProfiles_ScopeOwner] CHECK ((([Scope] = N'Platform' AND [TenantId] IS NULL AND [OwnerId] IS NULL AND [OrganizationId] IS NULL) OR ([Scope] = N'Tenant' AND [TenantId] IS NOT NULL AND [OwnerId] = [TenantId] AND [OrganizationId] IS NULL) OR ([Scope] = N'Organization' AND [TenantId] IS NOT NULL AND [OwnerId] = [OrganizationId] AND [OrganizationId] IS NOT NULL))),
    CONSTRAINT [CK_EmailNotificationProfiles_SecurityMode] CHECK ([SecurityMode] IN (N'StartTlsRequired', N'ImplicitTls')),
    CONSTRAINT [FK_EmailNotificationProfiles_Organizations_TenantId_OrganizationId] FOREIGN KEY ([TenantId], [OrganizationId]) REFERENCES [Organizations] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmailNotificationProfiles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Notifications_TenantId_OrganizationId] ON [Notifications] ([TenantId], [OrganizationId]);

CREATE INDEX [IX_EmailNotificationProfiles_ActiveOwner] ON [EmailNotificationProfiles] ([Scope], [TenantId], [OwnerId]) WHERE [DeletedAt] IS NULL;

CREATE INDEX [IX_EmailNotificationProfiles_TenantId_OrganizationId] ON [EmailNotificationProfiles] ([TenantId], [OrganizationId]);

CREATE UNIQUE INDEX [UX_EmailNotificationProfiles_Organization] ON [EmailNotificationProfiles] ([TenantId], [OwnerId]) WHERE [Scope] = N'Organization' AND [DeletedAt] IS NULL;

CREATE UNIQUE INDEX [UX_EmailNotificationProfiles_Platform] ON [EmailNotificationProfiles] ([Scope]) WHERE [Scope] = N'Platform' AND [DeletedAt] IS NULL;

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Organizations_TenantId_OrganizationId] FOREIGN KEY ([TenantId], [OrganizationId]) REFERENCES [Organizations] ([TenantId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107193155_AddUniversalEmailNotificationProfiles', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE UNIQUE INDEX [UX_EmailNotificationProfiles_Tenant] ON [EmailNotificationProfiles] ([TenantId]) WHERE [Scope] = N'Tenant' AND [DeletedAt] IS NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107193416_AddTenantScopedEmailProfileUniqueness', N'10.0.10');

COMMIT;
GO

