BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    CREATE TABLE [ProjectAnnualBudgetApprovalConfigurations] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ApprovalMode] nvarchar(24) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectAnnualBudgetApprovalConfigurations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project_Id] UNIQUE ([TenantId], [ProjectId], [Id]),
        CONSTRAINT [CK_ProjectAnnualBudgetApprovalConfigurations_Mode] CHECK ([ApprovalMode] IN (N'Sequential', N'Parallel')),
        CONSTRAINT [FK_ProjectAnnualBudgetApprovalConfigurations_Projects_TenantId_ProjectId] FOREIGN KEY ([TenantId], [ProjectId]) REFERENCES [Projects] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    CREATE TABLE [ProjectAnnualBudgetApprovalApprovers] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ConfigurationId] uniqueidentifier NOT NULL,
        [ApproverUserId] uniqueidentifier NOT NULL,
        [Position] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectAnnualBudgetApprovalApprovers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProjectAnnualBudgetApprovalApprovers_Position] CHECK ([Position] IS NULL OR [Position] > 0),
        CONSTRAINT [FK_ProjectAnnualBudgetApprovalApprovers_ProjectAnnualBudgetApprovalConfigurations_TenantId_ProjectId_ConfigurationId] FOREIGN KEY ([TenantId], [ProjectId], [ConfigurationId]) REFERENCES [ProjectAnnualBudgetApprovalConfigurations] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectAnnualBudgetApprovalApprovers_Users_TenantId_ApproverUserId] FOREIGN KEY ([TenantId], [ApproverUserId]) REFERENCES [Users] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ProjectAnnualBudgetApprovalApprovers_Configuration_Position] ON [ProjectAnnualBudgetApprovalApprovers] ([TenantId], [ConfigurationId], [Position]) WHERE [IsDeleted] = 0 AND [Position] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetApprovalApprovers_TenantId_ApproverUserId] ON [ProjectAnnualBudgetApprovalApprovers] ([TenantId], [ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetApprovalApprovers_TenantId_ProjectId_ConfigurationId] ON [ProjectAnnualBudgetApprovalApprovers] ([TenantId], [ProjectId], [ConfigurationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProjectAnnualBudgetApprovalApprovers_Configuration_User] ON [ProjectAnnualBudgetApprovalApprovers] ([TenantId], [ConfigurationId], [ApproverUserId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProjectAnnualBudgetApprovalConfigurations_Tenant_Project] ON [ProjectAnnualBudgetApprovalConfigurations] ([TenantId], [ProjectId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101114229_AddProjectAnnualBudgetApprovalConfiguration', N'10.0.10');
END;

COMMIT;
GO

