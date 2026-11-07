IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [AIPlanRequests] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Context] nvarchar(max) NOT NULL,
        [Provider] nvarchar(max) NOT NULL,
        [Model] nvarchar(max) NULL,
        [Parameters] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [Result] nvarchar(max) NULL,
        [RequestedBy] uniqueidentifier NULL,
        [RequestedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AIPlanRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ApprovalRequests] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [EntityNumber] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [Amount] decimal(19,4) NULL,
        [Currency] nvarchar(max) NULL,
        [RequestedBy] uniqueidentifier NULL,
        [RequestedAt] datetime2 NOT NULL,
        [RequiredBy] datetime2 NULL,
        [CurrentApproverId] uniqueidentifier NULL,
        [ApprovalLevel] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ApprovalRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [EntityType] nvarchar(128) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Action] nvarchar(96) NOT NULL,
        [Changes] nvarchar(max) NULL,
        [OriginalValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [ChangedFields] nvarchar(1000) NULL,
        [Description] nvarchar(500) NULL,
        [Source] nvarchar(32) NULL,
        [CorrelationId] nvarchar(128) NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(256) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [Timestamp] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Budgets] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NULL,
        [PortfolioId] uniqueidentifier NULL,
        [Category] nvarchar(max) NOT NULL,
        [PlannedAmount] decimal(19,4) NOT NULL,
        [ApprovedAmount] decimal(19,4) NOT NULL,
        [ActualAmount] decimal(19,4) NOT NULL,
        [CommittedAmount] decimal(19,4) NOT NULL,
        [ForecastAmount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [FiscalYear] int NOT NULL,
        [FiscalQuarter] int NULL,
        [Status] nvarchar(max) NOT NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Budgets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CostCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        [IsBillable] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CostCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Currencies] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Symbol] nvarchar(max) NOT NULL,
        [ExchangeRate] decimal(18,8) NOT NULL,
        [IsBase] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Currencies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Delegations] (
        [Id] uniqueidentifier NOT NULL,
        [FromUserId] uniqueidentifier NOT NULL,
        [ToUserId] uniqueidentifier NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Reason] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Delegations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryConnections] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [ProviderType] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [DirectoryTenantId] nvarchar(200) NULL,
        [ClientId] nvarchar(100) NULL,
        [CredentialReference] nvarchar(100) NULL,
        [Host] nvarchar(255) NULL,
        [Port] int NULL,
        [UseSsl] bit NOT NULL,
        [BaseDn] nvarchar(1024) NULL,
        [BindUser] nvarchar(256) NULL,
        [BindCredentialReference] nvarchar(100) NULL,
        [Domain] nvarchar(255) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryConnections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryFieldMappings] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [TargetField] nvarchar(50) NOT NULL,
        [SourceAttribute] nvarchar(100) NOT NULL,
        [UpdatePolicy] nvarchar(20) NOT NULL,
        [Transform] nvarchar(30) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryFieldMappings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryGroupMappings] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [ExternalGroupId] nvarchar(256) NOT NULL,
        [ExternalGroupName] nvarchar(256) NOT NULL,
        [RoleId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryGroupMappings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryGroups] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [ExternalDirectoryId] nvarchar(256) NOT NULL,
        [Name] nvarchar(256) NOT NULL,
        [Description] nvarchar(1024) NULL,
        [IsActive] bit NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryOrganizationMappings] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [DirectoryValue] nvarchar(256) NOT NULL,
        [OrganizationalUnitId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryOrganizationMappings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryProvisioningPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [MissingUserAction] nvarchar(20) NOT NULL,
        [MissingGroupAction] nvarchar(20) NOT NULL,
        [DisabledUserAction] nvarchar(20) NOT NULL,
        [OutOfScopeUserAction] nvarchar(20) NOT NULL,
        [ChangedGroupAction] nvarchar(20) NOT NULL,
        [MissingGroupReferenceAction] nvarchar(20) NOT NULL,
        [CreateResource] bit NOT NULL,
        [DefaultOrganizationalUnitId] uniqueidentifier NULL,
        [DefaultBaseCalendarId] uniqueidentifier NULL,
        [DefaultEmploymentType] nvarchar(100) NULL,
        [DefaultSeniority] nvarchar(100) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryProvisioningPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryRoleGrants] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [ExternalGroupId] nvarchar(256) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [UserRoleId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryRoleGrants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectorySyncRuns] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [TriggeredByUserId] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Mode] nvarchar(20) NOT NULL,
        [Scope] nvarchar(256) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [UsersFetched] int NOT NULL,
        [UsersCreated] int NOT NULL,
        [UsersUpdated] int NOT NULL,
        [UsersDeactivated] int NOT NULL,
        [UsersSkipped] int NOT NULL,
        [GroupsCreated] int NOT NULL,
        [GroupsUpdated] int NOT NULL,
        [GroupsDeactivated] int NOT NULL,
        [Conflicts] int NOT NULL,
        [Failures] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectorySyncRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectorySyncScopes] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(32) NOT NULL,
        [SelectedGroupIdsJson] nvarchar(max) NOT NULL,
        [SelectedUserIdsJson] nvarchar(max) NOT NULL,
        [OrganizationalUnitsJson] nvarchar(max) NOT NULL,
        [UserFilter] nvarchar(1024) NULL,
        [GroupFilter] nvarchar(1024) NULL,
        [MaxItems] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectorySyncScopes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectoryUserLinks] (
        [Id] uniqueidentifier NOT NULL,
        [DirectoryConnectionId] uniqueidentifier NOT NULL,
        [ExternalDirectoryId] nvarchar(256) NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserPrincipalName] nvarchar(256) NULL,
        [Username] nvarchar(256) NULL,
        [DisplayName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [Department] nvarchar(256) NULL,
        [JobTitle] nvarchar(200) NULL,
        [EmployeeId] nvarchar(100) NULL,
        [ManagerExternalId] nvarchar(256) NULL,
        [DistinguishedName] nvarchar(1024) NULL,
        [ManagedOrganizationalMembershipId] uniqueidentifier NULL,
        [ManagedReportingLineId] uniqueidentifier NULL,
        [IsDirectoryEnabled] bit NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [LastImportedValuesJson] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectoryUserLinks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DocumentCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [ParentId] uniqueidentifier NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_DocumentCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Documents] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [FileName] nvarchar(max) NULL,
        [FileType] nvarchar(max) NULL,
        [FileSize] bigint NULL,
        [StoragePath] nvarchar(max) NULL,
        [ContentType] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NULL,
        [PortfolioId] uniqueidentifier NULL,
        [FolderId] uniqueidentifier NULL,
        [Tags] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [Version] int NOT NULL,
        [OwnerId] uniqueidentifier NULL,
        [CategoryId] uniqueidentifier NULL,
        [IsTemplate] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [SourceTemplateId] uniqueidentifier NULL,
        [SourceTemplateVersionNumber] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [EntityCodeSequences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [NextValue] int NOT NULL,
        CONSTRAINT [PK_EntityCodeSequences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [IntegrationConnections] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Provider] nvarchar(max) NOT NULL,
        [BaseUrl] nvarchar(max) NULL,
        [ApiKey] nvarchar(max) NULL,
        [ApiSecret] nvarchar(max) NULL,
        [AuthType] nvarchar(max) NULL,
        [Config] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [LastSyncAt] datetime2 NULL,
        [Status] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IntegrationConnections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [IntegrationWebhooks] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Url] nvarchar(max) NULL,
        [Secret] nvarchar(max) NULL,
        [Events] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [LastTriggeredAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IntegrationWebhooks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceNumber] nvarchar(max) NOT NULL,
        [VendorName] nvarchar(max) NULL,
        [PurchaseOrderNumber] nvarchar(max) NULL,
        [Amount] decimal(19,4) NOT NULL,
        [TaxAmount] decimal(19,4) NULL,
        [Currency] nvarchar(max) NOT NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [PaidDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [BudgetId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [IssueCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Color] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IssueCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Locations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [AddressLine1] nvarchar(max) NULL,
        [AddressLine2] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [Postcode] nvarchar(max) NULL,
        [Timezone] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Lookups] (
        [Id] uniqueidentifier NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Color] nvarchar(50) NULL,
        [IsDefault] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Lookups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Meetings] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NULL,
        [MeetingType] nvarchar(max) NULL,
        [Location] nvarchar(max) NULL,
        [MeetingLink] nvarchar(max) NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NOT NULL,
        [ActualStartTime] datetime2 NULL,
        [ActualEndTime] datetime2 NULL,
        [Status] nvarchar(max) NULL,
        [ReminderOffsetMinutes] int NULL,
        [Notes] nvarchar(max) NULL,
        [OrganizerId] uniqueidentifier NULL,
        [Recurrence] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Meetings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [NotificationDigests] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Email] nvarchar(max) NULL,
        [Frequency] nvarchar(max) NOT NULL,
        [LastSentAt] datetime2 NULL,
        [NextScheduledAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_NotificationDigests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [EmailEnabled] bit NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [PushEnabled] bit NOT NULL,
        [DigestEnabled] bit NOT NULL,
        [DigestFrequency] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Email] nvarchar(max) NULL,
        [Title] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        [ReferenceType] nvarchar(max) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [IsInAppVisible] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [SentAt] datetime2 NOT NULL,
        [ScheduledAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [NotificationTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        [Subject] nvarchar(max) NOT NULL,
        [BodyTemplate] nvarchar(max) NULL,
        [Channel] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_NotificationTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [OrganizationalUnits] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TypeCode] nvarchar(100) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ParentOrganizationalUnitId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OrganizationalUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationalUnits_OrganizationalUnits_ParentOrganizationalUnitId] FOREIGN KEY ([ParentOrganizationalUnitId]) REFERENCES [OrganizationalUnits] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [Website] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [AddressLine1] nvarchar(max) NULL,
        [AddressLine2] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [Postcode] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Organizations_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PaymentObligations] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ProjectName] nvarchar(250) NOT NULL,
        [MilestoneId] uniqueidentifier NULL,
        [PaymentReference] nvarchar(100) NOT NULL,
        [Purpose] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [DueDate] datetime2 NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PaymentObligations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PaymentObligations_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Resource] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanImportSessions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [Format] nvarchar(50) NOT NULL,
        [SuggestedPlanName] nvarchar(250) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ParsedDocumentJson] nvarchar(max) NOT NULL,
        [ResolutionJson] nvarchar(max) NOT NULL,
        [CommitResultJson] nvarchar(max) NULL,
        [ParsedAt] datetime2 NULL,
        [CommittedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PlanImportSessions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanningTablePreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [VisibleColumnsJson] nvarchar(max) NOT NULL,
        [ColumnOrderJson] nvarchar(max) NOT NULL,
        [ColumnWidthsJson] nvarchar(max) NOT NULL,
        [ColumnFiltersJson] nvarchar(max) NOT NULL,
        [CollapsedRowIdsJson] nvarchar(max) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanningTablePreferences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Plans] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PlanType] int NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Plugins] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Version] nvarchar(max) NULL,
        [Author] nvarchar(max) NULL,
        [AssemblyName] nvarchar(max) NULL,
        [EntryPoint] nvarchar(max) NULL,
        [Category] nvarchar(max) NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Plugins] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Portfolios] (
        [Id] uniqueidentifier NOT NULL,
        [PublicId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(300) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(50) NULL,
        [OwnerId] uniqueidentifier NULL,
        [OrganizationId] uniqueidentifier NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [TotalBudget] decimal(19,4) NULL,
        [ActualSpend] decimal(19,4) NULL,
        [Health] nvarchar(max) NULL,
        [Priority] nvarchar(max) NULL,
        [Currency] nvarchar(max) NOT NULL,
        [SponsorId] uniqueidentifier NULL,
        [ConfidenceLevel] nvarchar(max) NULL,
        [StrategicObjective] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Portfolios] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectBaselineVersions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] nvarchar(100) NOT NULL,
        [VersionNumber] int NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Status] int NOT NULL,
        [Description] nvarchar(1000) NULL,
        [ApprovedBy] nvarchar(100) NULL,
        [ApprovedAt] datetime2 NULL,
        [SourceChangeRequestId] uniqueidentifier NULL,
        [IsCurrent] bit NOT NULL,
        [ApprovedBaselineMetadataId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectBaselineVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectBaselineVersions_ProjectBaselineVersions_ApprovedBaselineMetadataId] FOREIGN KEY ([ApprovedBaselineMetadataId]) REFERENCES [ProjectBaselineVersions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL,
        [PublicId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(300) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProgramId] uniqueidentifier NULL,
        [Status] nvarchar(50) NOT NULL,
        [Priority] nvarchar(50) NOT NULL,
        [Category] nvarchar(max) NULL,
        [ProjectManagerId] uniqueidentifier NULL,
        [SponsorId] uniqueidentifier NULL,
        [PortfolioId] uniqueidentifier NULL,
        [DepartmentId] uniqueidentifier NULL,
        [WorkingCalendarId] uniqueidentifier NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [ActualEndDate] datetime2 NULL,
        [Budget] decimal(19,4) NULL,
        [Currency] nvarchar(max) NOT NULL,
        [FinanceApprovalMode] nvarchar(24) NULL,
        [ActualCost] decimal(19,4) NULL,
        [PlannedValue] decimal(19,4) NULL,
        [EarnedValue] decimal(19,4) NULL,
        [Progress] decimal(7,4) NOT NULL,
        [Health] nvarchar(50) NOT NULL,
        [RagStatus] nvarchar(max) NOT NULL,
        [ConfidenceLevel] nvarchar(50) NOT NULL,
        [Phase] nvarchar(100) NULL,
        [RiskRating] nvarchar(50) NULL,
        [BusinessCase] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PurchaseCommitments] (
        [Id] uniqueidentifier NOT NULL,
        [PurchaseOrderNumber] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [VendorName] nvarchar(max) NULL,
        [Amount] decimal(19,4) NOT NULL,
        [InvoicedAmount] decimal(19,4) NULL,
        [Currency] nvarchar(max) NOT NULL,
        [CommitmentDate] datetime2 NOT NULL,
        [ExpectedDeliveryDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [BudgetId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PurchaseCommitments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RateCards] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Role] nvarchar(200) NOT NULL,
        [Seniority] nvarchar(50) NULL,
        [HourlyRate] decimal(10,2) NOT NULL,
        [DailyRate] decimal(19,4) NULL,
        [MonthlyRate] decimal(19,4) NULL,
        [Currency] nvarchar(10) NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_RateCards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ReportDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Category] nvarchar(max) NOT NULL,
        [Query] nvarchar(max) NULL,
        [Parameters] nvarchar(max) NULL,
        [OutputFormat] nvarchar(max) NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceReportingLines] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [ManagerResourceId] uniqueidentifier NOT NULL,
        [ReportingType] nvarchar(50) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResourceReportingLines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ProjectName] nvarchar(300) NOT NULL,
        [Role] nvarchar(200) NOT NULL,
        [SkillRequirements] nvarchar(max) NULL,
        [RequiredAllocation] decimal(7,4) NOT NULL,
        [RequiredFrom] datetime2 NOT NULL,
        [RequiredTo] datetime2 NOT NULL,
        [Priority] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Justification] nvarchar(2000) NULL,
        [RequestedBy] uniqueidentifier NULL,
        [RequestedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ResourceRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Color] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_RiskCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Category] nvarchar(100) NULL,
        [IsSystem] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Skills] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(100) NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Skills] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [TimerSessions] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NULL,
        [LastHeartbeatAt] datetimeoffset NULL,
        [LastActivityAt] datetimeoffset NULL,
        [Status] nvarchar(32) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TimerSessions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [UserDeviceTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(4000) NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [Platform] nvarchar(32) NOT NULL,
        [DeviceName] nvarchar(200) NULL,
        [IsActive] bit NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        [DeactivatedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserDeviceTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [AuthenticationSource] nvarchar(32) NOT NULL,
        [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit),
        [TemporaryPasswordExpiresAt] datetime2 NULL,
        [PasswordChangedAt] datetime2 NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [JobTitle] nvarchar(200) NULL,
        [Phone] nvarchar(50) NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Users_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkingCalendars] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [WorkingDaysPerWeek] int NOT NULL,
        [HoursPerDay] int NOT NULL,
        [PaidBreakMinutesPerDay] int NOT NULL,
        [IsDefault] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_WorkingCalendars] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkItems] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Type] nvarchar(50) NOT NULL,
        [Priority] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Source] nvarchar(50) NOT NULL,
        [Severity] nvarchar(50) NULL,
        [IdentifiedDate] datetime2 NULL,
        [TargetResolutionDate] datetime2 NULL,
        [ResolvedDate] datetime2 NULL,
        [Resolution] nvarchar(4000) NULL,
        [ProjectId] uniqueidentifier NULL,
        [AssignedResourceId] uniqueidentifier NULL,
        [AssignedTeamId] uniqueidentifier NULL,
        [ReporterId] uniqueidentifier NULL,
        [ParentId] uniqueidentifier NULL,
        [StoryPoints] int NULL,
        [EstimatedHours] decimal(10,2) NULL,
        [ActualHours] decimal(10,2) NULL,
        [StartDate] datetime2 NULL,
        [DueDate] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Tags] nvarchar(500) NULL,
        [Labels] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WorkItems_ExactlyOneAssignmentTarget] CHECK (([AssignedResourceId] IS NULL OR [AssignedTeamId] IS NULL))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [AIPlanResponses] (
        [Id] uniqueidentifier NOT NULL,
        [AIPlanRequestId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NULL,
        [ConfidenceScore] int NULL,
        [Suggestions] nvarchar(max) NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AIPlanResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AIPlanResponses_AIPlanRequests_AIPlanRequestId] FOREIGN KEY ([AIPlanRequestId]) REFERENCES [AIPlanRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ApprovalActions] (
        [Id] uniqueidentifier NOT NULL,
        [ApprovalRequestId] uniqueidentifier NOT NULL,
        [ApproverId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Comments] nvarchar(max) NULL,
        [ActionedAt] datetime2 NOT NULL,
        [ApprovalLevel] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ApprovalActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApprovalActions_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [ApprovalRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [AuditActivities] (
        [Id] uniqueidentifier NOT NULL,
        [AuditEntryId] uniqueidentifier NOT NULL,
        [Activity] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_AuditActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditActivities_AuditEntries_AuditEntryId] FOREIGN KEY ([AuditEntryId]) REFERENCES [AuditEntries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [BudgetRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [BudgetId] uniqueidentifier NOT NULL,
        [PreviousApprovedAmount] decimal(19,4) NOT NULL,
        [ProposedApprovedAmount] decimal(19,4) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [Justification] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [ReviewedBy] uniqueidentifier NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewComment] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_BudgetRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BudgetRevisions_Budgets_BudgetId] FOREIGN KEY ([BudgetId]) REFERENCES [Budgets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CostApprovals] (
        [Id] uniqueidentifier NOT NULL,
        [BudgetId] uniqueidentifier NULL,
        [Type] nvarchar(max) NOT NULL,
        [RequestedAmount] decimal(19,4) NOT NULL,
        [ApprovedAmount] decimal(19,4) NULL,
        [Justification] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [RequestedBy] uniqueidentifier NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [ApprovedAt] datetime2 NULL,
        [Comments] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CostApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CostApprovals_Budgets_BudgetId] FOREIGN KEY ([BudgetId]) REFERENCES [Budgets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CostEntries] (
        [Id] uniqueidentifier NOT NULL,
        [BudgetId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [ResourceId] uniqueidentifier NULL,
        [Category] nvarchar(max) NOT NULL,
        [SubCategory] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [TaxAmount] decimal(19,4) NULL,
        [IncurredDate] datetime2 NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Reference] nvarchar(max) NULL,
        [VendorName] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [IncurredBy] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CostEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CostEntries_Budgets_BudgetId] FOREIGN KEY ([BudgetId]) REFERENCES [Budgets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DirectorySyncItems] (
        [Id] uniqueidentifier NOT NULL,
        [DirectorySyncRunId] uniqueidentifier NOT NULL,
        [ItemType] nvarchar(16) NOT NULL,
        [ExternalDirectoryId] nvarchar(256) NOT NULL,
        [Classification] nvarchar(32) NOT NULL,
        [ProposedAction] nvarchar(32) NOT NULL,
        [MatchedUserId] uniqueidentifier NULL,
        [DisplayName] nvarchar(256) NULL,
        [DetailsJson] nvarchar(max) NULL,
        [Status] nvarchar(32) NOT NULL,
        [ErrorCode] nvarchar(64) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DirectorySyncItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DirectorySyncItems_DirectorySyncRuns_DirectorySyncRunId] FOREIGN KEY ([DirectorySyncRunId]) REFERENCES [DirectorySyncRuns] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DocumentReviews] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [ReviewerId] uniqueidentifier NOT NULL,
        [Comments] nvarchar(max) NULL,
        [Status] nvarchar(max) NULL,
        [ReviewedAt] datetime2 NULL,
        [DueDate] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_DocumentReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentReviews_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [DocumentVersions] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [FileName] nvarchar(max) NULL,
        [FileSize] bigint NOT NULL,
        [StoragePath] nvarchar(max) NULL,
        [ContentType] nvarchar(max) NULL,
        [ChangeNotes] nvarchar(max) NULL,
        [UploadedBy] uniqueidentifier NULL,
        [UploadedAt] datetime2 NOT NULL,
        [IsCurrent] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_DocumentVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentVersions_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [IntegrationSyncs] (
        [Id] uniqueidentifier NOT NULL,
        [IntegrationConnectionId] uniqueidentifier NOT NULL,
        [Direction] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [RecordsProcessed] int NULL,
        [RecordsFailed] int NULL,
        [ErrorLog] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IntegrationSyncs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IntegrationSyncs_IntegrationConnections_IntegrationConnectionId] FOREIGN KEY ([IntegrationConnectionId]) REFERENCES [IntegrationConnections] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Issues] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NULL,
        [CategoryId] uniqueidentifier NULL,
        [Severity] nvarchar(max) NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [AssigneeId] uniqueidentifier NULL,
        [ReporterId] uniqueidentifier NULL,
        [IdentifiedDate] datetime2 NULL,
        [TargetResolutionDate] datetime2 NULL,
        [ResolvedDate] datetime2 NULL,
        [Resolution] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Issues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Issues_IssueCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [IssueCategories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Vendors] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactEmail] nvarchar(256) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [Category] nvarchar(100) NULL,
        [VendorCategoryId] uniqueidentifier NULL,
        [Status] nvarchar(50) NULL,
        [ContractValue] decimal(19,4) NULL,
        [ContractStart] datetime2 NULL,
        [ContractEnd] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Vendors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vendors_Lookups_VendorCategoryId] FOREIGN KEY ([VendorCategoryId]) REFERENCES [Lookups] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingAgendas] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingId] uniqueidentifier NOT NULL,
        [Topic] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [DurationMinutes] int NULL,
        [OwnerId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingAgendas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingAgendas_Meetings_MeetingId] FOREIGN KEY ([MeetingId]) REFERENCES [Meetings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingMinutes] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NULL,
        [Decisions] nvarchar(max) NULL,
        [DiscussionPoints] nvarchar(max) NULL,
        [Outcomes] nvarchar(max) NULL,
        [State] nvarchar(20) NOT NULL DEFAULT N'Draft',
        [RecordedAt] datetime2 NOT NULL,
        [RecordedBy] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingMinutes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingMinutes_Meetings_MeetingId] FOREIGN KEY ([MeetingId]) REFERENCES [Meetings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingParticipants] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Name] nvarchar(200) NULL,
        [Email] nvarchar(256) NOT NULL,
        [AttendanceStatus] nvarchar(max) NULL,
        [InvitationResponseStatus] nvarchar(20) NULL,
        [Role] nvarchar(max) NULL,
        [RespondedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingParticipants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MeetingParticipants_UserOrEmail] CHECK ([UserId] IS NOT NULL OR [Email] IS NOT NULL),
        CONSTRAINT [FK_MeetingParticipants_Meetings_MeetingId] FOREIGN KEY ([MeetingId]) REFERENCES [Meetings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingReminderDispatches] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingId] uniqueidentifier NOT NULL,
        [ScheduledAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingReminderDispatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingReminderDispatches_Meetings_MeetingId] FOREIGN KEY ([MeetingId]) REFERENCES [Meetings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [NotificationDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationId] uniqueidentifier NOT NULL,
        [Channel] nvarchar(16) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [NextAttemptAt] datetime2 NOT NULL,
        [LastAttemptAt] datetime2 NULL,
        [ProcessingStartedAt] datetime2 NULL,
        [LeaseId] uniqueidentifier NULL,
        [DeliveredAt] datetime2 NULL,
        [FailureCategory] nvarchar(64) NULL,
        [LastError] nvarchar(300) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationDeliveries_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [Notifications] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceOrganizationalUnits] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [OrganizationalUnitId] uniqueidentifier NOT NULL,
        [IsPrimary] bit NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResourceOrganizationalUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceOrganizationalUnits_OrganizationalUnits_OrganizationalUnitId] FOREIGN KEY ([OrganizationalUnitId]) REFERENCES [OrganizationalUnits] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [BusinessUnits] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [HeadOfUnitId] uniqueidentifier NULL,
        [Budget] decimal(19,4) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_BusinessUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessUnits_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ConfigurationSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Scope] nvarchar(16) NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [OwnerId] uniqueidentifier NULL,
        [OrganizationId] uniqueidentifier NULL,
        [SettingKey] nvarchar(160) NOT NULL,
        [ValueType] nvarchar(16) NOT NULL,
        [ValueJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ConfigurationSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ConfigurationSettings_ScopeOwner] CHECK ((([Scope] = N'Platform' AND [TenantId] IS NULL AND [OwnerId] IS NULL AND [OrganizationId] IS NULL) OR ([Scope] = N'Tenant' AND [TenantId] IS NOT NULL AND [OwnerId] = [TenantId] AND [OrganizationId] IS NULL) OR ([Scope] = N'Organization' AND [TenantId] IS NOT NULL AND [OwnerId] = [OrganizationId] AND [OrganizationId] IS NOT NULL))),
        CONSTRAINT [CK_ConfigurationSettings_ValueJson_IsJson] CHECK (ISJSON([ValueJson], VALUE) = 1),
        CONSTRAINT [CK_ConfigurationSettings_ValueType] CHECK ([ValueType] IN (N'String', N'Integer', N'Decimal', N'Boolean')),
        CONSTRAINT [FK_ConfigurationSettings_Organizations_TenantId_OrganizationId] FOREIGN KEY ([TenantId], [OrganizationId]) REFERENCES [Organizations] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PaymentNotificationDispatches] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentObligationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [NotificationType] nvarchar(50) NOT NULL,
        [NotificationDate] datetime2 NOT NULL,
        [NotificationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PaymentNotificationDispatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentNotificationDispatches_PaymentObligations_TenantId_PaymentObligationId] FOREIGN KEY ([TenantId], [PaymentObligationId]) REFERENCES [PaymentObligations] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PaymentSettlements] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentObligationId] uniqueidentifier NOT NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [SettlementDate] datetime2 NOT NULL,
        [PaymentReference] nvarchar(100) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PaymentSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId] FOREIGN KEY ([TenantId], [PaymentObligationId]) REFERENCES [PaymentObligations] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanImportIssues] (
        [Id] uniqueidentifier NOT NULL,
        [PlanImportSessionId] uniqueidentifier NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Category] nvarchar(80) NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [SourceId] nvarchar(200) NULL,
        [Code] nvarchar(120) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Count] int NOT NULL,
        [IsBlocking] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PlanImportIssues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanImportIssues_PlanImportSessions_PlanImportSessionId] FOREIGN KEY ([PlanImportSessionId]) REFERENCES [PlanImportSessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanVersions] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [Name] nvarchar(250) NULL,
        [SourceVersionId] uniqueidentifier NULL,
        [SourceBaselineId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanVersions_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PluginDependencies] (
        [Id] uniqueidentifier NOT NULL,
        [PluginId] uniqueidentifier NOT NULL,
        [DependencyName] nvarchar(max) NOT NULL,
        [MinVersion] nvarchar(max) NULL,
        [MaxVersion] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PluginDependencies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PluginDependencies_Plugins_PluginId] FOREIGN KEY ([PluginId]) REFERENCES [Plugins] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [TenantPlugins] (
        [Id] uniqueidentifier NOT NULL,
        [PluginId] uniqueidentifier NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Config] nvarchar(max) NULL,
        [ActivatedAt] datetime2 NULL,
        [DeactivatedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_TenantPlugins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantPlugins_Plugins_PluginId] FOREIGN KEY ([PluginId]) REFERENCES [Plugins] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioBudgets] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [PlannedAmount] decimal(19,4) NOT NULL,
        [ActualAmount] decimal(19,4) NOT NULL,
        [FiscalYear] int NOT NULL,
        [FiscalQuarter] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PortfolioBudgets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioBudgets_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioHandoverRequests] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [CurrentPortfolioManagerId] uniqueidentifier NULL,
        [NewPortfolioManagerId] uniqueidentifier NOT NULL,
        [InitiationType] nvarchar(40) NOT NULL DEFAULT N'ManagerInitiated',
        [EffectiveHandoverDate] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [RespondedBy] uniqueidentifier NULL,
        [RespondedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [SnapshotId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PortfolioHandoverRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioHandoverRequests_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioHealths] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Comments] nvarchar(max) NULL,
        [RecordedAt] datetime2 NOT NULL,
        [ScheduleVariance] decimal(19,4) NULL,
        [CostVariance] decimal(19,4) NULL,
        [RecordedBy] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PortfolioHealths] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioHealths_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioManagerAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [PortfolioManagerId] uniqueidentifier NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsCurrent] bit NOT NULL,
        [HandoverFromPortfolioManagerId] uniqueidentifier NULL,
        [HandoverDate] datetime2 NULL,
        [HandoverReason] nvarchar(500) NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PortfolioManagerAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioManagerAssignments_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioRisks] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Category] nvarchar(max) NOT NULL,
        [Impact] nvarchar(max) NOT NULL,
        [Likelihood] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [PotentialCost] decimal(19,4) NULL,
        [MitigationPlan] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_PortfolioRisks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioRisks_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PortfolioSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [SnapshotNumber] int NOT NULL,
        [Name] nvarchar(300) NOT NULL,
        [SnapshotDate] datetime2 NOT NULL,
        [EffectiveEndDate] datetime2 NOT NULL,
        [PreviousPortfolioManagerId] uniqueidentifier NULL,
        [NewPortfolioManagerId] uniqueidentifier NOT NULL,
        [HandoverReason] nvarchar(500) NOT NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [Data] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortfolioSnapshots_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Programs] (
        [Id] uniqueidentifier NOT NULL,
        [PublicId] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [Status] nvarchar(max) NULL,
        [ProgramManagerId] uniqueidentifier NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Budget] decimal(19,4) NULL,
        [ActualSpend] decimal(19,4) NULL,
        [Health] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Programs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Programs_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [StrategicObjectives] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Category] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [PortfolioId] uniqueidentifier NOT NULL,
        [Status] nvarchar(max) NULL,
        [Progress] decimal(7,4) NULL,
        [TargetDate] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_StrategicObjectives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StrategicObjectives_Portfolios_PortfolioId] FOREIGN KEY ([PortfolioId]) REFERENCES [Portfolios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Baselines] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [BaselineDate] datetime2 NULL,
        [Data] nvarchar(max) NULL,
        [IsCurrent] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Baselines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Baselines_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Deliverables] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(max) NULL,
        [DueDate] datetime2 NULL,
        [DeliveredDate] datetime2 NULL,
        [OwnerId] uniqueidentifier NULL,
        [QualityCriteria] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Deliverables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Deliverables_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Milestones] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [DueDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [Status] nvarchar(max) NULL,
        [Progress] decimal(7,4) NULL,
        [Category] nvarchar(max) NULL,
        [AssigneeId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Milestones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Milestones_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectManagerAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ProjectManagerId] uniqueidentifier NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsCurrent] bit NOT NULL,
        [HandoverFromProjectManagerId] uniqueidentifier NULL,
        [HandoverDate] datetime2 NULL,
        [HandoverReason] nvarchar(500) NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectManagerAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectManagerAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectMembers] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Role] nvarchar(200) NOT NULL,
        [AllocationPercentage] decimal(7,4) NOT NULL,
        [HoursPerWeek] decimal(10,2) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [IsBillable] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CalendarPolicy] nvarchar(50) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectPhases] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [SortOrder] int NOT NULL,
        [Status] nvarchar(max) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [Progress] decimal(7,4) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ProjectPhases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectPhases_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectReportingConfigurations] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [ReportingFrequency] nvarchar(20) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [CurrencyCode] nvarchar(10) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [IsCurrent] bit NOT NULL,
        [IncludedSectionsJson] nvarchar(max) NOT NULL,
        [ConfiguredBy] nvarchar(300) NOT NULL,
        [ConfiguredAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectReportingConfigurations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectReportingConfigurations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [BaselineStartDate] datetime2 NULL,
        [BaselineEndDate] datetime2 NULL,
        [ForecastStartDate] datetime2 NULL,
        [ForecastEndDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [ActualEndDate] datetime2 NULL,
        [TotalDays] int NULL,
        [RemainingDays] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ProjectSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectSchedules_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SnapshotNumber] int NOT NULL,
        [SnapshotName] nvarchar(200) NOT NULL,
        [SnapshotType] nvarchar(50) NOT NULL,
        [SnapshotDate] datetime2 NOT NULL,
        [EffectiveEndDate] datetime2 NOT NULL,
        [PreviousProjectManagerId] uniqueidentifier NULL,
        [NewProjectManagerId] uniqueidentifier NULL,
        [HandoverReason] nvarchar(500) NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProgramId] uniqueidentifier NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NULL,
        [SponsorId] uniqueidentifier NULL,
        [PortfolioId] uniqueidentifier NULL,
        [DepartmentId] uniqueidentifier NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [ActualEndDate] datetime2 NULL,
        [Budget] decimal(19,4) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ActualCost] decimal(19,4) NULL,
        [PlannedValue] decimal(19,4) NULL,
        [EarnedValue] decimal(19,4) NULL,
        [CostVariance] decimal(19,4) NULL,
        [ScheduleVariance] decimal(19,4) NULL,
        [Progress] decimal(7,4) NOT NULL,
        [Health] nvarchar(max) NOT NULL,
        [RagStatus] nvarchar(max) NOT NULL,
        [ConfidenceLevel] nvarchar(max) NOT NULL,
        [Phase] nvarchar(max) NULL,
        [RiskRating] nvarchar(max) NULL,
        [BusinessCase] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [Data] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectSnapshots_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectStakeholders] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [ResourceId] uniqueidentifier NULL,
        [Name] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [Role] nvarchar(max) NOT NULL,
        [RelationshipType] nvarchar(30) NOT NULL DEFAULT N'Stakeholder',
        [Interest] nvarchar(max) NULL,
        [Influence] nvarchar(max) NULL,
        [Status] nvarchar(20) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ProjectStakeholders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectStakeholders_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectStatusReports] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ReportDate] datetime2 NOT NULL,
        [PeriodFrom] datetime2 NOT NULL,
        [PeriodTo] datetime2 NOT NULL,
        [StatusDate] datetime2 NOT NULL,
        [ReportState] nvarchar(20) NOT NULL,
        [OverallHealth] nvarchar(20) NULL,
        [ProjectLifecycleStatusAtReport] nvarchar(50) NULL,
        [OverallProgressPercent] decimal(7,4) NULL,
        [ScheduleHealth] nvarchar(20) NULL,
        [CostHealth] nvarchar(20) NULL,
        [ScopeHealth] nvarchar(20) NULL,
        [QualityHealth] nvarchar(20) NULL,
        [ResourceHealth] nvarchar(20) NULL,
        [ExecutiveSummary] nvarchar(4000) NULL,
        [KeyAchievements] nvarchar(4000) NULL,
        [ChallengesConcerns] nvarchar(4000) NULL,
        [NextPeriodPriorities] nvarchar(4000) NULL,
        [ActionsNeeded] nvarchar(4000) NULL,
        [SupportNeeded] nvarchar(4000) NULL,
        [ProjectNameSnapshot] nvarchar(300) NULL,
        [ProjectCodeSnapshot] nvarchar(50) NULL,
        [ProjectManagerIdAtReport] uniqueidentifier NULL,
        [ProjectManagerNameSnapshot] nvarchar(300) NULL,
        [PreparedByUserId] uniqueidentifier NULL,
        [PreparedByNameSnapshot] nvarchar(300) NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedByUserId] uniqueidentifier NULL,
        [PublishedByNameSnapshot] nvarchar(300) NULL,
        [SourceDataCapturedAt] datetime2 NULL,
        [PerformanceSnapshotJson] nvarchar(max) NULL,
        [ReportingConfigurationVersionSnapshot] int NULL,
        [CurrencyCodeSnapshot] nvarchar(10) NULL,
        [BudgetAmountSnapshot] decimal(19,4) NULL,
        [ActualCostAmountSnapshot] decimal(19,4) NULL,
        [CommittedCostAmountSnapshot] decimal(19,4) NULL,
        [ForecastCostAmountSnapshot] decimal(19,4) NULL,
        [CostVarianceAmountSnapshot] decimal(19,4) NULL,
        [ActualPaidSnapshot] decimal(19,4) NULL,
        [OutstandingPlannedPaymentsSnapshot] decimal(19,4) NULL,
        [PaymentsDueWithinPeriodSnapshot] decimal(19,4) NULL,
        [RemainingUnpaidAtPeriodEndSnapshot] decimal(19,4) NULL,
        [ScheduleVarianceDaysSnapshot] decimal(19,4) NULL,
        [ScheduleVariancePercentSnapshot] decimal(7,4) NULL,
        [BaselineIdAtReport] uniqueidentifier NULL,
        [BaselineNameSnapshot] nvarchar(300) NULL,
        [BaselineVersionSnapshot] int NULL,
        [Status] nvarchar(50) NULL,
        [ScheduleVariance] decimal(19,4) NULL,
        [CostVariance] decimal(19,4) NULL,
        [ReportedAt] datetime2 NOT NULL,
        [ReportedBy] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectStatusReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectStatusReports_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkBreakdownStructures] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Deliverable] nvarchar(max) NULL,
        [PlannedHours] decimal(10,2) NULL,
        [ActualHours] decimal(10,2) NULL,
        [Progress] decimal(7,4) NULL,
        [SortOrder] int NOT NULL,
        [Status] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_WorkBreakdownStructures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkBreakdownStructures_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WorkBreakdownStructures_WorkBreakdownStructures_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [WorkBreakdownStructures] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ReportExports] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [Format] nvarchar(max) NOT NULL,
        [FilePath] nvarchar(max) NULL,
        [FileSize] bigint NULL,
        [Status] nvarchar(max) NOT NULL,
        [GeneratedAt] datetime2 NULL,
        [GeneratedBy] uniqueidentifier NULL,
        [Parameters] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ReportExports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportExports_ReportDefinitions_ReportDefinitionId] FOREIGN KEY ([ReportDefinitionId]) REFERENCES [ReportDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ReportSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [ReportDefinitionId] uniqueidentifier NOT NULL,
        [Frequency] nvarchar(max) NOT NULL,
        [CronExpression] nvarchar(max) NULL,
        [Recipients] nvarchar(max) NULL,
        [OutputFormat] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [LastRunAt] datetime2 NULL,
        [NextRunAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ReportSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportSchedules_ReportDefinitions_ReportDefinitionId] FOREIGN KEY ([ReportDefinitionId]) REFERENCES [ReportDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Risks] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PortfolioId] uniqueidentifier NULL,
        [CategoryId] uniqueidentifier NULL,
        [Impact] nvarchar(max) NOT NULL,
        [Likelihood] nvarchar(max) NOT NULL,
        [RiskLevel] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [PotentialCost] decimal(19,4) NULL,
        [PotentialDelay] decimal(19,4) NULL,
        [RiskResponse] nvarchar(max) NULL,
        [OwnerId] uniqueidentifier NULL,
        [IdentifiedDate] datetime2 NULL,
        [ClosedDate] datetime2 NULL,
        [OwnerResourceId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Risks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Risks_RiskCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RiskCategories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [PermissionsId] uniqueidentifier NOT NULL,
        [RolesId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([PermissionsId], [RolesId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionsId] FOREIGN KEY ([PermissionsId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RolesId] FOREIGN KEY ([RolesId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [TimeEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PlanActivityId] uniqueidentifier NULL,
        [WorkItemId] uniqueidentifier NULL,
        [ProjectRoleId] uniqueidentifier NULL,
        [TimerSessionId] uniqueidentifier NULL,
        [WorkDate] datetime2 NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [ElapsedSeconds] int NOT NULL,
        [ActualSeconds] int NOT NULL,
        [BreakSeconds] int NOT NULL,
        [PauseSeconds] int NOT NULL,
        [Description] nvarchar(2000) NULL,
        [EntrySource] nvarchar(20) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TimeEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeEntries_TimerSessions_TimerSessionId] FOREIGN KEY ([TimerSessionId]) REFERENCES [TimerSessions] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [TimerSegments] (
        [Id] uniqueidentifier NOT NULL,
        [TimerSessionId] uniqueidentifier NOT NULL,
        [SegmentType] nvarchar(16) NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TimerSegments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimerSegments_TimerSessions_TimerSessionId] FOREIGN KEY ([TimerSessionId]) REFERENCES [TimerSessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [OrganizationSettingsGrants] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CanRead] bit NOT NULL,
        [CanManage] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OrganizationSettingsGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrganizationSettingsGrants_ManageRequiresRead] CHECK ([CanManage] = 0 OR [CanRead] = 1),
        CONSTRAINT [FK_OrganizationSettingsGrants_Organizations_TenantId_OrganizationId] FOREIGN KEY ([TenantId], [OrganizationId]) REFERENCES [Organizations] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrganizationSettingsGrants_Users_TenantId_UserId] FOREIGN KEY ([TenantId], [UserId]) REFERENCES [Users] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [UserPermissionOverrides] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [Effect] nvarchar(16) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserPermissionOverrides] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissionOverrides_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserPermissionOverrides_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [UserSessions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [RefreshToken] nvarchar(max) NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_UserSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Holidays] (
        [Id] uniqueidentifier NOT NULL,
        [WorkingCalendarId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Date] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsRecurring] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Holidays_WorkingCalendars_WorkingCalendarId] FOREIGN KEY ([WorkingCalendarId]) REFERENCES [WorkingCalendars] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkingCalendarDayRules] (
        [Id] uniqueidentifier NOT NULL,
        [WorkingCalendarId] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [IsWorkingDay] bit NOT NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [BreakStartTime] time NULL,
        [BreakEndTime] time NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkingCalendarDayRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkingCalendarDayRules_WorkingCalendars_WorkingCalendarId] FOREIGN KEY ([WorkingCalendarId]) REFERENCES [WorkingCalendars] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkItemAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [WorkItemId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [UnassignedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_WorkItemAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkItemAssignments_WorkItems_WorkItemId] FOREIGN KEY ([WorkItemId]) REFERENCES [WorkItems] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkItemAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [WorkItemId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(255) NOT NULL,
        [FileSize] bigint NOT NULL,
        [StoragePath] nvarchar(2000) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkItemAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkItemAttachments_WorkItems_WorkItemId] FOREIGN KEY ([WorkItemId]) REFERENCES [WorkItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkItemComments] (
        [Id] uniqueidentifier NOT NULL,
        [WorkItemId] uniqueidentifier NOT NULL,
        [ParentCommentId] uniqueidentifier NULL,
        [CommentText] nvarchar(4000) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(200) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkItemComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkItemComments_WorkItemComments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [WorkItemComments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkItemComments_WorkItems_WorkItemId] FOREIGN KEY ([WorkItemId]) REFERENCES [WorkItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [SyncConflicts] (
        [Id] uniqueidentifier NOT NULL,
        [IntegrationSyncId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [LocalValue] nvarchar(max) NOT NULL,
        [RemoteValue] nvarchar(max) NOT NULL,
        [Resolution] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_SyncConflicts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncConflicts_IntegrationSyncs_IntegrationSyncId] FOREIGN KEY ([IntegrationSyncId]) REFERENCES [IntegrationSyncs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [IssueActions] (
        [Id] uniqueidentifier NOT NULL,
        [IssueId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [OwnerId] uniqueidentifier NULL,
        [DueDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_IssueActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IssueActions_Issues_IssueId] FOREIGN KEY ([IssueId]) REFERENCES [Issues] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Resources] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Name] nvarchar(200) NOT NULL,
        [EmployeeId] nvarchar(50) NULL,
        [JobTitle] nvarchar(200) NULL,
        [Email] nvarchar(256) NULL,
        [Phone] nvarchar(50) NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [Type] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [EmploymentType] nvarchar(50) NULL,
        [Seniority] nvarchar(50) NULL,
        [DateOfJoining] datetime2 NULL,
        [BusinessUnitId] uniqueidentifier NULL,
        [DepartmentId] uniqueidentifier NULL,
        [TeamId] uniqueidentifier NULL,
        [LocationId] uniqueidentifier NULL,
        [VendorId] uniqueidentifier NULL,
        [BaseCalendarId] uniqueidentifier NULL,
        [HoursPerDay] decimal(10,2) NULL,
        [HoursPerWeek] decimal(10,2) NULL,
        [Timezone] nvarchar(50) NULL,
        [UtilizationTarget] decimal(7,4) NULL,
        [CurrentUtilization] decimal(7,4) NULL,
        [Notes] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Resources] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Resources_HoursPerDay_Range] CHECK ([HoursPerDay] IS NULL OR ([HoursPerDay] > 0 AND [HoursPerDay] <= 24)),
        CONSTRAINT [CK_Resources_HoursPerWeek_Range] CHECK ([HoursPerWeek] IS NULL OR ([HoursPerWeek] > 0 AND [HoursPerWeek] <= 168)),
        CONSTRAINT [FK_Resources_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Resources_WorkingCalendars_BaseCalendarId] FOREIGN KEY ([BaseCalendarId]) REFERENCES [WorkingCalendars] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingActions] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [AssigneeId] uniqueidentifier NULL,
        [DueDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [WorkItemId] uniqueidentifier NULL,
        [AgendaItemId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingActions_MeetingAgendas_AgendaItemId] FOREIGN KEY ([AgendaItemId]) REFERENCES [MeetingAgendas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MeetingActions_Meetings_MeetingId] FOREIGN KEY ([MeetingId]) REFERENCES [Meetings] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MeetingActions_WorkItems_WorkItemId] FOREIGN KEY ([WorkItemId]) REFERENCES [WorkItems] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingMinutesAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingMinutesId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [FileSize] bigint NOT NULL,
        [StoragePath] nvarchar(1000) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingMinutesAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingMinutesAttachments_MeetingMinutes_MeetingMinutesId] FOREIGN KEY ([MeetingMinutesId]) REFERENCES [MeetingMinutes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingMinutesChecklistItems] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingMinutesId] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [IsCompleted] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingMinutesChecklistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingMinutesChecklistItems_MeetingMinutes_MeetingMinutesId] FOREIGN KEY ([MeetingMinutesId]) REFERENCES [MeetingMinutes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingMinutesComments] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingMinutesId] uniqueidentifier NOT NULL,
        [ParentCommentId] uniqueidentifier NULL,
        [CommentText] nvarchar(4000) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(200) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingMinutesComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingMinutesComments_MeetingMinutesComments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [MeetingMinutesComments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MeetingMinutesComments_MeetingMinutes_MeetingMinutesId] FOREIGN KEY ([MeetingMinutesId]) REFERENCES [MeetingMinutes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [MeetingMinutesWorkItemLinks] (
        [Id] uniqueidentifier NOT NULL,
        [MeetingMinutesId] uniqueidentifier NOT NULL,
        [WorkItemId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MeetingMinutesWorkItemLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingMinutesWorkItemLinks_MeetingMinutes_MeetingMinutesId] FOREIGN KEY ([MeetingMinutesId]) REFERENCES [MeetingMinutes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [NotificationDeliveryAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [NotificationDeliveryId] uniqueidentifier NOT NULL,
        [DeviceTokenId] uniqueidentifier NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptAt] datetime2 NOT NULL,
        [LastAttemptAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        [ProviderMessageId] nvarchar(256) NULL,
        [FailureCategory] nvarchar(64) NULL,
        [LastError] nvarchar(300) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NotificationDeliveryAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationDeliveryAttempts_NotificationDeliveries_NotificationDeliveryId] FOREIGN KEY ([NotificationDeliveryId]) REFERENCES [NotificationDeliveries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NotificationDeliveryAttempts_UserDeviceTokens_DeviceTokenId] FOREIGN KEY ([DeviceTokenId]) REFERENCES [UserDeviceTokens] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CostCentres] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [BusinessUnitId] uniqueidentifier NULL,
        [AnnualBudget] decimal(19,4) NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CostCentres] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CostCentres_BusinessUnits_BusinessUnitId] FOREIGN KEY ([BusinessUnitId]) REFERENCES [BusinessUnits] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] uniqueidentifier NOT NULL,
        [BusinessUnitId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [HeadOfDepartmentId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_BusinessUnits_BusinessUnitId] FOREIGN KEY ([BusinessUnitId]) REFERENCES [BusinessUnits] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [PlanVersionId] uniqueidentifier NOT NULL,
        [ActivityId] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [AllocationPct] decimal(7,4) NULL,
        [HoursPerWeek] decimal(10,2) NULL,
        [PlannedHours] decimal(10,2) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [ProjectRoleId] uniqueidentifier NULL,
        [ResourceAllocationId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanAssignments_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanDeliverables] (
        [Id] uniqueidentifier NOT NULL,
        [PlanVersionId] uniqueidentifier NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [AcceptanceCriteria] nvarchar(4000) NULL,
        [DueDate] datetime2 NULL,
        [WbsId] uniqueidentifier NULL,
        [ActivityId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanDeliverables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanDeliverables_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanDependencies] (
        [Id] uniqueidentifier NOT NULL,
        [PlanVersionId] uniqueidentifier NOT NULL,
        [ActivityId] uniqueidentifier NOT NULL,
        [PredecessorActivityId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Lag] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanDependencies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanDependencies_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanningCalendars] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [CalendarKind] nvarchar(450) NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [PlanVersionId] uniqueidentifier NULL,
        [TemplateId] uniqueidentifier NULL,
        [EffectiveFrom] datetime2 NULL,
        [EffectiveTo] datetime2 NULL,
        [IsDefault] bit NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanningCalendars] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanningCalendars_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlanningCalendars_PlanningCalendars_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [PlanningCalendars] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanWbsNodes] (
        [Id] uniqueidentifier NOT NULL,
        [PlanVersionId] uniqueidentifier NOT NULL,
        [ParentWbsId] uniqueidentifier NULL,
        [SortOrder] int NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [ActivityId] uniqueidentifier NULL,
        [DeliverableId] uniqueidentifier NULL,
        [TextFormatSpecified] bit NULL,
        [TextFormatBold] bit NULL,
        [TextFormatItalic] bit NULL,
        [TextFormatUnderline] bit NULL,
        [TextFormatTextColor] int NULL,
        [TextFormatHighlightColor] int NULL,
        [TextFormatFontSize] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanWbsNodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanWbsNodes_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlanWbsNodes_PlanWbsNodes_ParentWbsId] FOREIGN KEY ([ParentWbsId]) REFERENCES [PlanWbsNodes] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProgramHandoverRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ProgramId] uniqueidentifier NOT NULL,
        [CurrentProgramManagerId] uniqueidentifier NULL,
        [NewProgramManagerId] uniqueidentifier NOT NULL,
        [InitiationType] nvarchar(40) NOT NULL DEFAULT N'ManagerInitiated',
        [EffectiveHandoverDate] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [RespondedBy] uniqueidentifier NULL,
        [RespondedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [SnapshotId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProgramHandoverRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProgramHandoverRequests_Programs_ProgramId] FOREIGN KEY ([ProgramId]) REFERENCES [Programs] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProgramManagerAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [ProgramId] uniqueidentifier NOT NULL,
        [ProgramManagerId] uniqueidentifier NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsCurrent] bit NOT NULL,
        [HandoverFromProgramManagerId] uniqueidentifier NULL,
        [HandoverDate] datetime2 NULL,
        [HandoverReason] nvarchar(500) NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProgramManagerAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProgramManagerAssignments_Programs_ProgramId] FOREIGN KEY ([ProgramId]) REFERENCES [Programs] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProgramSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ProgramId] uniqueidentifier NOT NULL,
        [SnapshotNumber] int NOT NULL,
        [Name] nvarchar(300) NOT NULL,
        [SnapshotDate] datetime2 NOT NULL,
        [EffectiveEndDate] datetime2 NOT NULL,
        [PreviousProgramManagerId] uniqueidentifier NULL,
        [NewProgramManagerId] uniqueidentifier NOT NULL,
        [HandoverReason] nvarchar(500) NOT NULL,
        [HandoverNotes] nvarchar(2000) NULL,
        [Data] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProgramSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProgramSnapshots_Programs_ProgramId] FOREIGN KEY ([ProgramId]) REFERENCES [Programs] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectMemberRoles] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectMemberId] uniqueidentifier NOT NULL,
        [ProjectRoleId] uniqueidentifier NOT NULL,
        [IsPrimary] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectMemberRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectMemberRoles_ProjectMembers_ProjectMemberId] FOREIGN KEY ([ProjectMemberId]) REFERENCES [ProjectMembers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectReportingPlanSelections] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectReportingConfigurationId] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [WorkingVersionId] uniqueidentifier NOT NULL,
        [BaselineVersionId] uniqueidentifier NOT NULL,
        [IncludeInConsolidatedReporting] bit NOT NULL,
        [PlanNameSnapshot] nvarchar(300) NOT NULL,
        [PlanTypeSnapshot] nvarchar(40) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectReportingPlanSelections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectReportingPlanSelections_ProjectReportingConfigurations_ProjectReportingConfigurationId] FOREIGN KEY ([ProjectReportingConfigurationId]) REFERENCES [ProjectReportingConfigurations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectHandoverRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CurrentProjectManagerId] uniqueidentifier NULL,
        [NewProjectManagerId] uniqueidentifier NOT NULL,
        [InitiationType] nvarchar(40) NOT NULL DEFAULT N'ManagerInitiated',
        [EffectiveHandoverDate] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [RespondedBy] uniqueidentifier NULL,
        [RespondedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [SnapshotId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectHandoverRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectHandoverRequests_ProjectSnapshots_SnapshotId] FOREIGN KEY ([SnapshotId]) REFERENCES [ProjectSnapshots] ([Id]),
        CONSTRAINT [FK_ProjectHandoverRequests_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [StakeholderInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ProjectStakeholderId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_StakeholderInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StakeholderInvitations_ProjectStakeholders_ProjectStakeholderId] FOREIGN KEY ([ProjectStakeholderId]) REFERENCES [ProjectStakeholders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StakeholderInvitations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectStatusReportChangeRequestSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectStatusReportId] uniqueidentifier NOT NULL,
        [SourceChangeRequestId] uniqueidentifier NOT NULL,
        [Rank] int NOT NULL,
        [Number] int NOT NULL,
        [ChangeType] nvarchar(30) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Justification] nvarchar(4000) NULL,
        [Impact] nvarchar(4000) NULL,
        [ChangeRequestStatus] nvarchar(30) NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedByNameSnapshot] nvarchar(300) NULL,
        [RequestedAt] datetime2 NOT NULL,
        [ReviewedAt] datetime2 NULL,
        [DecisionNotes] nvarchar(4000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectStatusReportChangeRequestSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectStatusReportChangeRequestSnapshots_ProjectStatusReports_ProjectStatusReportId] FOREIGN KEY ([ProjectStatusReportId]) REFERENCES [ProjectStatusReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectStatusReportIssueSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectStatusReportId] uniqueidentifier NOT NULL,
        [SourceIssueId] uniqueidentifier NOT NULL,
        [Rank] int NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Severity] nvarchar(30) NOT NULL,
        [Priority] nvarchar(30) NOT NULL,
        [IssueStatus] nvarchar(30) NOT NULL,
        [AssigneeId] uniqueidentifier NULL,
        [AssigneeNameSnapshot] nvarchar(300) NULL,
        [IdentifiedDate] datetime2 NULL,
        [TargetResolutionDate] datetime2 NULL,
        [ResolvedDate] datetime2 NULL,
        [Resolution] nvarchar(4000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectStatusReportIssueSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectStatusReportIssueSnapshots_ProjectStatusReports_ProjectStatusReportId] FOREIGN KEY ([ProjectStatusReportId]) REFERENCES [ProjectStatusReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectStatusReportRiskSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectStatusReportId] uniqueidentifier NOT NULL,
        [SourceRiskId] uniqueidentifier NOT NULL,
        [Rank] int NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Impact] nvarchar(max) NULL,
        [Likelihood] nvarchar(max) NULL,
        [RiskLevel] nvarchar(30) NOT NULL,
        [RiskStatus] nvarchar(30) NOT NULL,
        [PotentialCost] decimal(19,4) NULL,
        [PotentialDelay] decimal(19,4) NULL,
        [RiskResponse] nvarchar(4000) NULL,
        [OwnerId] uniqueidentifier NULL,
        [OwnerNameSnapshot] nvarchar(300) NULL,
        [IdentifiedDate] datetime2 NULL,
        [ClosedDate] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectStatusReportRiskSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectStatusReportRiskSnapshots_ProjectStatusReports_ProjectStatusReportId] FOREIGN KEY ([ProjectStatusReportId]) REFERENCES [ProjectStatusReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskActions] (
        [Id] uniqueidentifier NOT NULL,
        [RiskId] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [OwnerResourceId] uniqueidentifier NULL,
        [TargetDate] datetime2 NULL,
        [NextFollowUpDate] datetime2 NULL,
        [Status] nvarchar(30) NOT NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RiskActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskActions_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskContingencies] (
        [Id] uniqueidentifier NOT NULL,
        [RiskId] uniqueidentifier NOT NULL,
        [Plan] nvarchar(max) NOT NULL,
        [Budget] decimal(19,4) NULL,
        [Trigger] nvarchar(max) NULL,
        [OwnerId] uniqueidentifier NULL,
        [Status] nvarchar(max) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_RiskContingencies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskContingencies_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskImpactAssessments] (
        [Id] uniqueidentifier NOT NULL,
        [RiskId] uniqueidentifier NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RiskImpactAssessments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskImpactAssessments_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskMitigations] (
        [Id] uniqueidentifier NOT NULL,
        [RiskId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [OwnerId] uniqueidentifier NULL,
        [DueDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [Cost] decimal(19,4) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_RiskMitigations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskMitigations_Risks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [Risks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Type] nvarchar(100) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [Value] decimal(19,4) NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Terms] nvarchar(4000) NULL,
        [VendorName] nvarchar(200) NULL,
        [Status] nvarchar(50) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Contracts_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ProjectName] nvarchar(300) NULL,
        [WorkItemId] uniqueidentifier NULL,
        [ProjectRoleId] uniqueidentifier NULL,
        [AllocationPercentage] decimal(7,4) NOT NULL,
        [HoursPerWeek] decimal(10,2) NULL,
        [PlannedHours] decimal(10,2) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [IsBillable] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Notes] nvarchar(1000) NULL,
        [AllocationSource] nvarchar(50) NOT NULL DEFAULT N'Manual',
        [SourceReferenceId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResourceAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceAllocations_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceCalendars] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Date] datetime2 NOT NULL,
        [DayType] nvarchar(50) NOT NULL,
        [AvailableHours] decimal(10,2) NULL,
        [Notes] nvarchar(500) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ResourceCalendars] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceCalendars_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceCostProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [CompensationType] nvarchar(50) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [OvertimeEligible] bit NOT NULL,
        [OvertimeRateType] nvarchar(50) NULL,
        [OvertimeRateValue] decimal(19,4) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ResourceCostProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceCostProfiles_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ResourceSkills] (
        [Id] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [SkillId] uniqueidentifier NOT NULL,
        [Proficiency] nvarchar(50) NOT NULL,
        [YearsOfExperience] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_ResourceSkills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResourceSkills_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ResourceSkills_Skills_SkillId] FOREIGN KEY ([SkillId]) REFERENCES [Skills] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NULL,
        [Description] nvarchar(1000) NULL,
        [TeamLeadId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [ExternalDirectoryId] nvarchar(256) NULL,
        [EmailAlias] nvarchar(256) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Teams_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CalendarDayRules] (
        [Id] uniqueidentifier NOT NULL,
        [PlanningCalendarId] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [IsWorkingDay] bit NOT NULL,
        [StartTime] time NULL,
        [EndTime] time NULL,
        [BreakStartTime] time NULL,
        [BreakEndTime] time NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CalendarDayRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CalendarDayRules_PlanningCalendars_PlanningCalendarId] FOREIGN KEY ([PlanningCalendarId]) REFERENCES [PlanningCalendars] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [CalendarHolidays] (
        [Id] uniqueidentifier NOT NULL,
        [PlanningCalendarId] uniqueidentifier NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Date] date NOT NULL,
        [EndDate] date NULL,
        [IsRecurring] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CalendarHolidays] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CalendarHolidays_PlanningCalendars_PlanningCalendarId] FOREIGN KEY ([PlanningCalendarId]) REFERENCES [PlanningCalendars] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [PlanActivities] (
        [Id] uniqueidentifier NOT NULL,
        [PlanVersionId] uniqueidentifier NOT NULL,
        [SourceActivityId] uniqueidentifier NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Type] int NOT NULL,
        [SchedulingMode] int NOT NULL,
        [ConstraintType] int NOT NULL,
        [ConstraintDate] datetime2 NULL,
        [Deadline] datetime2 NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [ActualStart] datetime2 NULL,
        [ActualFinish] datetime2 NULL,
        [CalendarId] uniqueidentifier NULL,
        [Duration] int NOT NULL,
        [SortOrder] int NOT NULL,
        [EstimateHours] decimal(10,2) NULL,
        [Progress] decimal(7,4) NULL,
        [WbsId] uniqueidentifier NULL,
        [TextFormatSpecified] bit NULL,
        [TextFormatBold] bit NULL,
        [TextFormatItalic] bit NULL,
        [TextFormatUnderline] bit NULL,
        [TextFormatTextColor] int NULL,
        [TextFormatHighlightColor] int NULL,
        [TextFormatFontSize] int NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PlanActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanActivities_PlanVersions_PlanVersionId] FOREIGN KEY ([PlanVersionId]) REFERENCES [PlanVersions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlanActivities_PlanningCalendars_CalendarId] FOREIGN KEY ([CalendarId]) REFERENCES [PlanningCalendars] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskActionUpdates] (
        [Id] uniqueidentifier NOT NULL,
        [RiskActionId] uniqueidentifier NOT NULL,
        [UpdateText] nvarchar(max) NOT NULL,
        [EntryType] nvarchar(30) NOT NULL,
        [ChangeDetailsJson] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RiskActionUpdates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskActionUpdates_RiskActions_RiskActionId] FOREIGN KEY ([RiskActionId]) REFERENCES [RiskActions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [RiskImpactAssessmentTypes] (
        [Id] uniqueidentifier NOT NULL,
        [RiskImpactAssessmentId] uniqueidentifier NOT NULL,
        [ImpactType] nvarchar(30) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RiskImpactAssessmentTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskImpactAssessmentTypes_RiskImpactAssessments_RiskImpactAssessmentId] FOREIGN KEY ([RiskImpactAssessmentId]) REFERENCES [RiskImpactAssessments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [TeamMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectChangeRequestApprovalHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectChangeRequestApprovalId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [FromApproverUserId] uniqueidentifier NOT NULL,
        [FromApproverNameSnapshot] nvarchar(200) NOT NULL,
        [ToApproverUserId] uniqueidentifier NOT NULL,
        [ToApproverNameSnapshot] nvarchar(200) NOT NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedByNameSnapshot] nvarchar(200) NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectChangeRequestApprovalHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectChangeRequestApprovals] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectChangeRequestId] uniqueidentifier NOT NULL,
        [WorkflowInstanceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [OriginalApproverUserId] uniqueidentifier NOT NULL,
        [OriginalApproverNameSnapshot] nvarchar(200) NOT NULL,
        [AssignedToUserId] uniqueidentifier NOT NULL,
        [ApproverNameSnapshot] nvarchar(200) NOT NULL,
        [RoleSnapshot] nvarchar(200) NOT NULL,
        [IsSponsor] bit NOT NULL,
        [IsMandatory] bit NOT NULL,
        [Sequence] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DecidedAt] datetime2 NULL,
        [DecisionActorId] uniqueidentifier NULL,
        [DecisionComment] nvarchar(2000) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectChangeRequestApprovals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectChangeRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Justification] nvarchar(2000) NULL,
        [Impact] nvarchar(max) NULL,
        [BudgetBeforeChange] decimal(19,4) NULL,
        [BudgetChangeAmount] decimal(19,4) NULL,
        [Attachments] nvarchar(4000) NULL,
        [ApprovalStrategy] nvarchar(40) NOT NULL,
        [SelectedApproverUserIdsJson] nvarchar(max) NOT NULL,
        [WorkflowInstanceId] uniqueidentifier NULL,
        [SubmittedAt] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [DecisionNotes] nvarchar(max) NULL,
        [ReviewedBy] uniqueidentifier NULL,
        [ReviewedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ProjectChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectChangeRequests_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectPlanDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SourceBaselineVersionId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [SubmittedChangeRequestId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectPlanDrafts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectPlanDrafts_ProjectBaselineVersions_SourceBaselineVersionId] FOREIGN KEY ([SourceBaselineVersionId]) REFERENCES [ProjectBaselineVersions] ([Id]),
        CONSTRAINT [FK_ProjectPlanDrafts_ProjectChangeRequests_SubmittedChangeRequestId] FOREIGN KEY ([SubmittedChangeRequestId]) REFERENCES [ProjectChangeRequests] ([Id]),
        CONSTRAINT [FK_ProjectPlanDrafts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [ProjectWorkflowConfigurations] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Domain] nvarchar(30) NOT NULL,
        [Mode] nvarchar(20) NOT NULL,
        [WorkflowDefinitionId] uniqueidentifier NULL,
        [EffectiveWorkflowVersionId] uniqueidentifier NULL,
        [ConfiguredBy] nvarchar(200) NOT NULL,
        [ConfiguredAt] datetime2 NOT NULL,
        [SponsorApprovalRequired] bit NOT NULL DEFAULT CAST(1 AS bit),
        [AllowAnyOneOptionalApprover] bit NOT NULL DEFAULT CAST(0 AS bit),
        [AllowSequentialApprovers] bit NOT NULL DEFAULT CAST(1 AS bit),
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectWorkflowConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Domain] nvarchar(30) NOT NULL DEFAULT N'Task',
        [EntityType] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [ActivePublishedVersionId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowDefinitionVersions] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowDefinitionId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [LifecycleStatus] nvarchar(20) NOT NULL,
        [ChangeLog] nvarchar(4000) NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedBy] nvarchar(200) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowDefinitionVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowDefinitionVersions_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowStates] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowDefinitionVersionId] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [DisplayOrder] int NOT NULL,
        [IsInitial] bit NOT NULL,
        [IsFinal] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowStates] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WorkflowStates_WorkflowDefinitionVersionId_Id] UNIQUE ([WorkflowDefinitionVersionId], [Id]),
        CONSTRAINT [FK_WorkflowStates_WorkflowDefinitionVersions_WorkflowDefinitionVersionId] FOREIGN KEY ([WorkflowDefinitionVersionId]) REFERENCES [WorkflowDefinitionVersions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowInstances] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowDefinitionId] uniqueidentifier NOT NULL,
        [WorkflowDefinitionVersionId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [CurrentStateId] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [StartedBy] nvarchar(200) NOT NULL,
        [CompletedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowInstances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowInstances_WorkflowDefinitionVersions_WorkflowDefinitionVersionId] FOREIGN KEY ([WorkflowDefinitionVersionId]) REFERENCES [WorkflowDefinitionVersions] ([Id]),
        CONSTRAINT [FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]),
        CONSTRAINT [FK_WorkflowInstances_WorkflowStates_WorkflowDefinitionVersionId_CurrentStateId] FOREIGN KEY ([WorkflowDefinitionVersionId], [CurrentStateId]) REFERENCES [WorkflowStates] ([WorkflowDefinitionVersionId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowTransitions] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowDefinitionVersionId] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [FromStateId] uniqueidentifier NOT NULL,
        [ToStateId] uniqueidentifier NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowTransitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowTransitions_WorkflowDefinitionVersions_WorkflowDefinitionVersionId] FOREIGN KEY ([WorkflowDefinitionVersionId]) REFERENCES [WorkflowDefinitionVersions] ([Id]),
        CONSTRAINT [FK_WorkflowTransitions_WorkflowStates_WorkflowDefinitionVersionId_FromStateId] FOREIGN KEY ([WorkflowDefinitionVersionId], [FromStateId]) REFERENCES [WorkflowStates] ([WorkflowDefinitionVersionId], [Id]),
        CONSTRAINT [FK_WorkflowTransitions_WorkflowStates_WorkflowDefinitionVersionId_ToStateId] FOREIGN KEY ([WorkflowDefinitionVersionId], [ToStateId]) REFERENCES [WorkflowStates] ([WorkflowDefinitionVersionId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowTransitionActions] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowTransitionId] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(100) NOT NULL,
        [ConfigurationJson] nvarchar(max) NOT NULL,
        [ExecutionOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowTransitionActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowTransitionActions_WorkflowTransitions_WorkflowTransitionId] FOREIGN KEY ([WorkflowTransitionId]) REFERENCES [WorkflowTransitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowTransitionGuards] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowTransitionId] uniqueidentifier NOT NULL,
        [GuardType] nvarchar(100) NOT NULL,
        [ConfigurationJson] nvarchar(max) NOT NULL,
        [ExecutionOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_WorkflowTransitionGuards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowTransitionGuards_WorkflowTransitions_WorkflowTransitionId] FOREIGN KEY ([WorkflowTransitionId]) REFERENCES [WorkflowTransitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE TABLE [WorkflowTransitionHistory] (
        [Id] uniqueidentifier NOT NULL,
        [WorkflowInstanceId] uniqueidentifier NOT NULL,
        [WorkflowTransitionId] uniqueidentifier NOT NULL,
        [FromStateId] uniqueidentifier NOT NULL,
        [ToStateId] uniqueidentifier NOT NULL,
        [ExecutedBy] nvarchar(200) NOT NULL,
        [ExecutedByNameSnapshot] nvarchar(200) NULL,
        [ExecutedAt] datetime2 NOT NULL,
        [Comment] nvarchar(4000) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CorrelationId] nvarchar(100) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_WorkflowTransitionHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowTransitionHistory_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]),
        CONSTRAINT [FK_WorkflowTransitionHistory_WorkflowStates_FromStateId] FOREIGN KEY ([FromStateId]) REFERENCES [WorkflowStates] ([Id]),
        CONSTRAINT [FK_WorkflowTransitionHistory_WorkflowStates_ToStateId] FOREIGN KEY ([ToStateId]) REFERENCES [WorkflowStates] ([Id]),
        CONSTRAINT [FK_WorkflowTransitionHistory_WorkflowTransitions_WorkflowTransitionId] FOREIGN KEY ([WorkflowTransitionId]) REFERENCES [WorkflowTransitions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AIPlanResponses_AIPlanRequestId] ON [AIPlanResponses] ([AIPlanRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ApprovalActions_ApprovalRequestId] ON [ApprovalActions] ([ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditActivities_AuditEntryId] ON [AuditActivities] ([AuditEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Action] ON [AuditEntries] ([TenantId], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_EntityType_EntityId] ON [AuditEntries] ([TenantId], [EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_ProjectId_Timestamp] ON [AuditEntries] ([TenantId], [ProjectId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_Timestamp] ON [AuditEntries] ([TenantId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TenantId_UserId] ON [AuditEntries] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Baselines_ProjectId] ON [Baselines] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_BudgetRevisions_BudgetId] ON [BudgetRevisions] ([BudgetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_BusinessUnits_OrganizationId] ON [BusinessUnits] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_CalendarDayRules_PlanningCalendarId] ON [CalendarDayRules] ([PlanningCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CalendarDayRules_Tenant_Calendar_Day] ON [CalendarDayRules] ([TenantId], [PlanningCalendarId], [DayOfWeek]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_CalendarHolidays_PlanningCalendarId] ON [CalendarHolidays] ([PlanningCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CalendarHolidays_Tenant_Calendar_Date] ON [CalendarHolidays] ([TenantId], [PlanningCalendarId], [Date]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ConfigurationSettings_ActiveOwner] ON [ConfigurationSettings] ([Scope], [TenantId], [OwnerId]) WHERE [DeletedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ConfigurationSettings_TenantId_OrganizationId] ON [ConfigurationSettings] ([TenantId], [OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ConfigurationSettings_Organization_Owner_Key] ON [ConfigurationSettings] ([TenantId], [OwnerId], [SettingKey]) WHERE [Scope] = N''Organization'' AND [DeletedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ConfigurationSettings_Platform_Key] ON [ConfigurationSettings] ([SettingKey]) WHERE [Scope] = N''Platform'' AND [DeletedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Contracts_ResourceId] ON [Contracts] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Contracts_TenantId] ON [Contracts] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_CostApprovals_BudgetId] ON [CostApprovals] ([BudgetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_CostCentres_BusinessUnitId] ON [CostCentres] ([BusinessUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_CostEntries_BudgetId] ON [CostEntries] ([BudgetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Deliverables_ProjectId] ON [Deliverables] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Departments_BusinessUnitId] ON [Departments] ([BusinessUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryConnections_TenantId_IsDefault] ON [DirectoryConnections] ([TenantId], [IsDefault]) WHERE [IsDeleted] = 0 AND [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryConnections_TenantId_Name] ON [DirectoryConnections] ([TenantId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryFieldMappings_TenantId_DirectoryConnectionId_TargetField] ON [DirectoryFieldMappings] ([TenantId], [DirectoryConnectionId], [TargetField]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryGroupMappings_TenantId_DirectoryConnectionId_ExternalGroupId] ON [DirectoryGroupMappings] ([TenantId], [DirectoryConnectionId], [ExternalGroupId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DirectoryGroupMappings_TenantId_DirectoryConnectionId_RoleId] ON [DirectoryGroupMappings] ([TenantId], [DirectoryConnectionId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryGroups_TenantId_DirectoryConnectionId_ExternalDirectoryId] ON [DirectoryGroups] ([TenantId], [DirectoryConnectionId], [ExternalDirectoryId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryOrganizationMappings_TenantId_DirectoryConnectionId_DirectoryValue] ON [DirectoryOrganizationMappings] ([TenantId], [DirectoryConnectionId], [DirectoryValue]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryProvisioningPolicies_TenantId_DirectoryConnectionId] ON [DirectoryProvisioningPolicies] ([TenantId], [DirectoryConnectionId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryRoleGrants_TenantId_DirectoryConnectionId_ExternalGroupId_UserId_RoleId] ON [DirectoryRoleGrants] ([TenantId], [DirectoryConnectionId], [ExternalGroupId], [UserId], [RoleId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncItems_DirectorySyncRunId] ON [DirectorySyncItems] ([DirectorySyncRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncItems_TenantId_DirectorySyncRunId] ON [DirectorySyncItems] ([TenantId], [DirectorySyncRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncRuns_TenantId_DirectoryConnectionId_StartedAt] ON [DirectorySyncRuns] ([TenantId], [DirectoryConnectionId], [StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectorySyncScopes_TenantId_DirectoryConnectionId] ON [DirectorySyncScopes] ([TenantId], [DirectoryConnectionId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryUserLinks_TenantId_DirectoryConnectionId_ExternalDirectoryId] ON [DirectoryUserLinks] ([TenantId], [DirectoryConnectionId], [ExternalDirectoryId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DirectoryUserLinks_TenantId_DirectoryConnectionId_UserId] ON [DirectoryUserLinks] ([TenantId], [DirectoryConnectionId], [UserId]) WHERE [IsDeleted] = 0 AND [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DirectoryUserLinks_TenantId_UserPrincipalName] ON [DirectoryUserLinks] ([TenantId], [UserPrincipalName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DocumentReviews_DocumentId] ON [DocumentReviews] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Documents_CategoryId] ON [Documents] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Documents_SourceTemplateId] ON [Documents] ([SourceTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_DocumentVersions_DocumentId] ON [DocumentVersions] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EntityCodeSequences_TenantId_EntityType] ON [EntityCodeSequences] ([TenantId], [EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Holidays_WorkingCalendarId] ON [Holidays] ([WorkingCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_IntegrationSyncs_IntegrationConnectionId] ON [IntegrationSyncs] ([IntegrationConnectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_IssueActions_IssueId] ON [IssueActions] ([IssueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Issues_CategoryId] ON [Issues] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Lookups_TenantId_Category_Code] ON [Lookups] ([TenantId], [Category], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Lookups_TenantId_Category_IsActive_SortOrder] ON [Lookups] ([TenantId], [Category], [IsActive], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingActions_AgendaItemId] ON [MeetingActions] ([AgendaItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingActions_MeetingId] ON [MeetingActions] ([MeetingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingActions_WorkItemId] ON [MeetingActions] ([WorkItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingAgendas_MeetingId] ON [MeetingAgendas] ([MeetingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingMinutes_MeetingId] ON [MeetingMinutes] ([MeetingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingMinutesAttachments_MeetingMinutesId] ON [MeetingMinutesAttachments] ([MeetingMinutesId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingMinutesChecklistItems_MeetingMinutesId_SortOrder] ON [MeetingMinutesChecklistItems] ([MeetingMinutesId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingMinutesComments_MeetingMinutesId_CreatedAt] ON [MeetingMinutesComments] ([MeetingMinutesId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingMinutesComments_ParentCommentId] ON [MeetingMinutesComments] ([ParentCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MeetingMinutesWorkItemLinks_MeetingMinutesId_WorkItemId] ON [MeetingMinutesWorkItemLinks] ([MeetingMinutesId], [WorkItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_MeetingParticipants_MeetingId] ON [MeetingParticipants] ([MeetingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MeetingParticipants_MeetingId_Email] ON [MeetingParticipants] ([MeetingId], [Email]) WHERE [IsDeleted] = 0 AND [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MeetingParticipants_MeetingId_UserId] ON [MeetingParticipants] ([MeetingId], [UserId]) WHERE [IsDeleted] = 0 AND [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MeetingReminderDispatches_MeetingId_ScheduledAtUtc] ON [MeetingReminderDispatches] ([MeetingId], [ScheduledAtUtc]) WHERE [IsDeleted] = 0 AND [Status] = ''Pending''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Meetings_StartTime_ReminderOffsetMinutes] ON [Meetings] ([StartTime], [ReminderOffsetMinutes]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Milestones_ProjectId] ON [Milestones] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_NotificationId] ON [NotificationDeliveries] ([NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_Status_NextAttemptAt] ON [NotificationDeliveries] ([Status], [NextAttemptAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveries_Status_ProcessingStartedAt] ON [NotificationDeliveries] ([Status], [ProcessingStartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationDeliveries_TenantId_NotificationId_Channel] ON [NotificationDeliveries] ([TenantId], [NotificationId], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveryAttempts_DeviceTokenId] ON [NotificationDeliveryAttempts] ([DeviceTokenId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveryAttempts_NotificationDeliveryId] ON [NotificationDeliveryAttempts] ([NotificationDeliveryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationDeliveryAttempts_TenantId_DeviceTokenId_Status] ON [NotificationDeliveryAttempts] ([TenantId], [DeviceTokenId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationDeliveryAttempts_TenantId_NotificationDeliveryId_DeviceTokenId] ON [NotificationDeliveryAttempts] ([TenantId], [NotificationDeliveryId], [DeviceTokenId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_NotificationPreferences_TenantId_UserId_Type] ON [NotificationPreferences] ([TenantId], [UserId], [Type]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_OrganizationalUnits_ParentOrganizationalUnitId] ON [OrganizationalUnits] ([ParentOrganizationalUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrganizationalUnits_TenantId_Code] ON [OrganizationalUnits] ([TenantId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_OrganizationalUnits_TenantId_ParentOrganizationalUnitId_IsActive] ON [OrganizationalUnits] ([TenantId], [ParentOrganizationalUnitId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_OrganizationalUnits_TenantId_TypeCode_IsActive] ON [OrganizationalUnits] ([TenantId], [TypeCode], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OrganizationSettingsGrants_ActiveUserOrg] ON [OrganizationSettingsGrants] ([TenantId], [UserId], [OrganizationId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_OrganizationSettingsGrants_ActiveOrgUser] ON [OrganizationSettingsGrants] ([TenantId], [OrganizationId], [UserId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PaymentNotificationDispatches_TenantId_PaymentObligationId] ON [PaymentNotificationDispatches] ([TenantId], [PaymentObligationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentNotificationDispatches_TenantId_PaymentObligationId_UserId_NotificationType_NotificationDate] ON [PaymentNotificationDispatches] ([TenantId], [PaymentObligationId], [UserId], [NotificationType], [NotificationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PaymentObligations_TenantId_MilestoneId] ON [PaymentObligations] ([TenantId], [MilestoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PaymentObligations_TenantId_ProjectId_DueDate] ON [PaymentObligations] ([TenantId], [ProjectId], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PaymentObligations_TenantId_ProjectId_PaymentReference] ON [PaymentObligations] ([TenantId], [ProjectId], [PaymentReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentSettlements_TenantId_IdempotencyKey] ON [PaymentSettlements] ([TenantId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PaymentSettlements_TenantId_PaymentObligationId_SettlementDate] ON [PaymentSettlements] ([TenantId], [PaymentObligationId], [SettlementDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Resource_Action] ON [Permissions] ([Resource], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanActivities_CalendarId] ON [PlanActivities] ([CalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanActivities_PlanVersionId] ON [PlanActivities] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanActivities_Tenant_Version_Code] ON [PlanActivities] ([TenantId], [PlanVersionId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanActivities_Tenant_Version_SourceActivity] ON [PlanActivities] ([TenantId], [PlanVersionId], [SourceActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanAssignments_PlanVersionId] ON [PlanAssignments] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanAssignments_ResourceAllocationId] ON [PlanAssignments] ([ResourceAllocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanAssignments_Tenant_Version_Activity_Resource] ON [PlanAssignments] ([TenantId], [PlanVersionId], [ActivityId], [ResourceId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanDeliverables_PlanVersionId] ON [PlanDeliverables] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanDeliverables_Tenant_Version_Name] ON [PlanDeliverables] ([TenantId], [PlanVersionId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanDependencies_PlanVersionId] ON [PlanDependencies] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanDependencies_Tenant_Version_Edge] ON [PlanDependencies] ([TenantId], [PlanVersionId], [ActivityId], [PredecessorActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanImportIssues_PlanImportSessionId] ON [PlanImportIssues] ([PlanImportSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanImportIssues_TenantId_PlanImportSessionId_IsBlocking] ON [PlanImportIssues] ([TenantId], [PlanImportSessionId], [IsBlocking]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanImportSessions_TenantId_ProjectId_Status] ON [PlanImportSessions] ([TenantId], [ProjectId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanCalendars_Tenant_Version_Default] ON [PlanningCalendars] ([TenantId], [PlanVersionId], [IsDefault]) WHERE [IsDeleted] = 0 AND [IsDefault] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanCalendars_Tenant_Version_Name] ON [PlanningCalendars] ([TenantId], [PlanVersionId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanningCalendars_PlanVersionId] ON [PlanningCalendars] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanningCalendars_TemplateId] ON [PlanningCalendars] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanningTablePreferences_TenantId_UserId_PlanId] ON [PlanningTablePreferences] ([TenantId], [UserId], [PlanId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Plans_Tenant_Project_Type_Name] ON [Plans] ([TenantId], [ProjectId], [PlanType], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanVersions_PlanId] ON [PlanVersions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanVersions_Tenant_Plan_ActiveWorking] ON [PlanVersions] ([TenantId], [PlanId], [Type]) WHERE "Type" = 0 AND "Status" = 0 AND "IsDeleted" = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PlanVersions_Tenant_Plan_ApprovedBaseline] ON [PlanVersions] ([TenantId], [PlanId], [Status]) WHERE "Type" = 1 AND "Status" = 3 AND "IsDeleted" = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanVersions_Tenant_Plan_Version] ON [PlanVersions] ([TenantId], [PlanId], [VersionNumber], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanWbsNodes_ParentWbsId] ON [PlanWbsNodes] ([ParentWbsId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PlanWbsNodes_PlanVersionId] ON [PlanWbsNodes] ([PlanVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PlanWbsNodes_Tenant_Version_Parent_Code] ON [PlanWbsNodes] ([TenantId], [PlanVersionId], [ParentWbsId], [Code]) WHERE [ParentWbsId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PluginDependencies_PluginId] ON [PluginDependencies] ([PluginId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioBudgets_PortfolioId] ON [PortfolioBudgets] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioHandoverRequests_PortfolioId] ON [PortfolioHandoverRequests] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioHandoverRequests_TenantId] ON [PortfolioHandoverRequests] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PortfolioHandoverRequests_TenantId_PortfolioId_Status] ON [PortfolioHandoverRequests] ([TenantId], [PortfolioId], [Status]) WHERE [Status] = ''Pending'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioHealths_PortfolioId] ON [PortfolioHealths] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PortfolioManagerAssignments_PortfolioId] ON [PortfolioManagerAssignments] ([PortfolioId]) WHERE [IsCurrent] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioManagerAssignments_PortfolioManagerId] ON [PortfolioManagerAssignments] ([PortfolioManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioManagerAssignments_TenantId] ON [PortfolioManagerAssignments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioRisks_PortfolioId] ON [PortfolioRisks] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Portfolios_PublicId] ON [Portfolios] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Portfolios_TenantId_Code] ON [Portfolios] ([TenantId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Portfolios_TenantId_Status] ON [Portfolios] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioSnapshots_PortfolioId] ON [PortfolioSnapshots] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_PortfolioSnapshots_TenantId] ON [PortfolioSnapshots] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PortfolioSnapshots_TenantId_PortfolioId_SnapshotNumber] ON [PortfolioSnapshots] ([TenantId], [PortfolioId], [SnapshotNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramHandoverRequests_ProgramId] ON [ProgramHandoverRequests] ([ProgramId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramHandoverRequests_TenantId] ON [ProgramHandoverRequests] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProgramHandoverRequests_TenantId_ProgramId_Status] ON [ProgramHandoverRequests] ([TenantId], [ProgramId], [Status]) WHERE [Status] = ''Pending'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProgramManagerAssignments_ProgramId] ON [ProgramManagerAssignments] ([ProgramId]) WHERE [IsCurrent] = 1 AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramManagerAssignments_ProgramManagerId] ON [ProgramManagerAssignments] ([ProgramManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramManagerAssignments_TenantId] ON [ProgramManagerAssignments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Programs_PortfolioId] ON [Programs] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Programs_PublicId] ON [Programs] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramSnapshots_ProgramId] ON [ProgramSnapshots] ([ProgramId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProgramSnapshots_TenantId] ON [ProgramSnapshots] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProgramSnapshots_TenantId_ProgramId_SnapshotNumber] ON [ProgramSnapshots] ([TenantId], [ProgramId], [SnapshotNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectBaselineVersions_ApprovedBaselineMetadataId] ON [ProjectBaselineVersions] ([ApprovedBaselineMetadataId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Tenant_Project_CurrentApproved] ON [ProjectBaselineVersions] ([TenantId], [ProjectId], [IsCurrent], [Status]) WHERE "IsCurrent" = 1 AND "Status" = 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenant_Project_Version] ON [ProjectBaselineVersions] ([TenantId], [ProjectId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequestApprovalHistories_ProjectChangeRequestApprovalId] ON [ProjectChangeRequestApprovalHistories] ([ProjectChangeRequestApprovalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequestApprovalHistories_TenantId_ProjectChangeRequestApprovalId_ChangedAt] ON [ProjectChangeRequestApprovalHistories] ([TenantId], [ProjectChangeRequestApprovalId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequestApprovals_ProjectChangeRequestId] ON [ProjectChangeRequestApprovals] ([ProjectChangeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequestApprovals_TenantId_AssignedToUserId_Status] ON [ProjectChangeRequestApprovals] ([TenantId], [AssignedToUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectChangeRequestApprovals_TenantId_ProjectChangeRequestId_Sequence] ON [ProjectChangeRequestApprovals] ([TenantId], [ProjectChangeRequestId], [Sequence]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequests_ProjectId] ON [ProjectChangeRequests] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectChangeRequests_TenantId_WorkflowInstanceId] ON [ProjectChangeRequests] ([TenantId], [WorkflowInstanceId]) WHERE [IsDeleted] = 0 AND [WorkflowInstanceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectChangeRequests_WorkflowInstanceId] ON [ProjectChangeRequests] ([WorkflowInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectHandoverRequests_NewProjectManagerId] ON [ProjectHandoverRequests] ([NewProjectManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectHandoverRequests_ProjectId] ON [ProjectHandoverRequests] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectHandoverRequests_SnapshotId] ON [ProjectHandoverRequests] ([SnapshotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectHandoverRequests_Status] ON [ProjectHandoverRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectHandoverRequests_TenantId] ON [ProjectHandoverRequests] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectHandoverRequests_TenantId_ProjectId_Status] ON [ProjectHandoverRequests] ([TenantId], [ProjectId], [Status]) WHERE [Status] = ''Pending'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectManagerAssignments_ProjectId] ON [ProjectManagerAssignments] ([ProjectId]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectManagerAssignments_ProjectManagerId] ON [ProjectManagerAssignments] ([ProjectManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectManagerAssignments_TenantId] ON [ProjectManagerAssignments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMemberRoles_ProjectMemberId] ON [ProjectMemberRoles] ([ProjectMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMemberRoles_TenantId] ON [ProjectMemberRoles] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectMemberRoles_TenantId_ProjectMemberId_ProjectRoleId] ON [ProjectMemberRoles] ([TenantId], [ProjectMemberId], [ProjectRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_ProjectId] ON [ProjectMembers] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_ProjectId_ResourceId_Status] ON [ProjectMembers] ([ProjectId], [ResourceId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_ResourceId] ON [ProjectMembers] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_TenantId] ON [ProjectMembers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectPhases_ProjectId] ON [ProjectPhases] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectPlanDrafts_ProjectId] ON [ProjectPlanDrafts] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectPlanDrafts_SourceBaselineVersionId] ON [ProjectPlanDrafts] ([SourceBaselineVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectPlanDrafts_SubmittedChangeRequestId] ON [ProjectPlanDrafts] ([SubmittedChangeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectPlanDrafts_Tenant_Project_Active] ON [ProjectPlanDrafts] ([TenantId], [ProjectId]) WHERE "IsActive" = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectReportingConfigurations_ProjectId] ON [ProjectReportingConfigurations] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectReportingConfigurations_Tenant_Project_Current] ON [ProjectReportingConfigurations] ([TenantId], [ProjectId], [IsCurrent]) WHERE [IsCurrent] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectReportingConfigurations_TenantId_ProjectId_Version] ON [ProjectReportingConfigurations] ([TenantId], [ProjectId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectReportingPlanSelections_ProjectReportingConfigurationId] ON [ProjectReportingPlanSelections] ([ProjectReportingConfigurationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectReportingPlanSelections_TenantId_PlanId] ON [ProjectReportingPlanSelections] ([TenantId], [PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectReportingPlanSelections_TenantId_ProjectReportingConfigurationId_PlanId] ON [ProjectReportingPlanSelections] ([TenantId], [ProjectReportingConfigurationId], [PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Projects_ProjectManagerId] ON [Projects] ([ProjectManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_PublicId] ON [Projects] ([PublicId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Projects_Status] ON [Projects] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Projects_TenantId] ON [Projects] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Projects_WorkingCalendarId] ON [Projects] ([WorkingCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectSchedules_ProjectId] ON [ProjectSchedules] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectSnapshots_ProjectId] ON [ProjectSnapshots] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectSnapshots_SnapshotNumber] ON [ProjectSnapshots] ([SnapshotNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectSnapshots_TenantId] ON [ProjectSnapshots] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStakeholders_ProjectId] ON [ProjectStakeholders] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ProjectStakeholders_TenantId_ProjectId_NormalizedEmail] ON [ProjectStakeholders] ([TenantId], [ProjectId], [NormalizedEmail]) WHERE [IsDeleted] = 0 AND [Status] = ''ContactOnly'' AND [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ProjectStakeholders_TenantId_ProjectId_UserId] ON [ProjectStakeholders] ([TenantId], [ProjectId], [UserId]) WHERE [IsDeleted] = 0 AND [Status] = ''Active'' AND [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportChangeRequestSnapshots_ProjectStatusReportId] ON [ProjectStatusReportChangeRequestSnapshots] ([ProjectStatusReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportChangeRequestSnapshots_TenantId_ProjectStatusReportId_Rank] ON [ProjectStatusReportChangeRequestSnapshots] ([TenantId], [ProjectStatusReportId], [Rank]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectStatusReportChangeRequestSnapshots_TenantId_ProjectStatusReportId_SourceChangeRequestId] ON [ProjectStatusReportChangeRequestSnapshots] ([TenantId], [ProjectStatusReportId], [SourceChangeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportIssueSnapshots_ProjectStatusReportId] ON [ProjectStatusReportIssueSnapshots] ([ProjectStatusReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportIssueSnapshots_TenantId_ProjectStatusReportId_Rank] ON [ProjectStatusReportIssueSnapshots] ([TenantId], [ProjectStatusReportId], [Rank]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectStatusReportIssueSnapshots_TenantId_ProjectStatusReportId_SourceIssueId] ON [ProjectStatusReportIssueSnapshots] ([TenantId], [ProjectStatusReportId], [SourceIssueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportRiskSnapshots_ProjectStatusReportId] ON [ProjectStatusReportRiskSnapshots] ([ProjectStatusReportId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReportRiskSnapshots_TenantId_ProjectStatusReportId_Rank] ON [ProjectStatusReportRiskSnapshots] ([TenantId], [ProjectStatusReportId], [Rank]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectStatusReportRiskSnapshots_TenantId_ProjectStatusReportId_SourceRiskId] ON [ProjectStatusReportRiskSnapshots] ([TenantId], [ProjectStatusReportId], [SourceRiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReports_ProjectId] ON [ProjectStatusReports] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReports_TenantId_ProjectId_ReportDate] ON [ProjectStatusReports] ([TenantId], [ProjectId], [ReportDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectStatusReports_TenantId_ProjectId_ReportState] ON [ProjectStatusReports] ([TenantId], [ProjectId], [ReportState]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectWorkflowConfigurations_EffectiveWorkflowVersionId] ON [ProjectWorkflowConfigurations] ([EffectiveWorkflowVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectWorkflowConfigurations_TenantId_ProjectId_Domain] ON [ProjectWorkflowConfigurations] ([TenantId], [ProjectId], [Domain]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectWorkflowConfigurations_TenantId_WorkflowDefinitionId] ON [ProjectWorkflowConfigurations] ([TenantId], [WorkflowDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ProjectWorkflowConfigurations_WorkflowDefinitionId] ON [ProjectWorkflowConfigurations] ([WorkflowDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RateCards_TenantId] ON [RateCards] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ReportExports_ReportDefinitionId] ON [ReportExports] ([ReportDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_ReportDefinitionId] ON [ReportSchedules] ([ReportDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_AllocationSource_SourceReferenceId] ON [ResourceAllocations] ([AllocationSource], [SourceReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_ProjectId] ON [ResourceAllocations] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_ResourceId] ON [ResourceAllocations] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_ResourceId_StartDate_EndDate] ON [ResourceAllocations] ([ResourceId], [StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_Status] ON [ResourceAllocations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_TenantId] ON [ResourceAllocations] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceAllocations_WorkItemId] ON [ResourceAllocations] ([WorkItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ResourceCalendars_ResourceId_Date] ON [ResourceCalendars] ([ResourceId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceCalendars_TenantId] ON [ResourceCalendars] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceCostProfiles_ResourceId] ON [ResourceCostProfiles] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceCostProfiles_ResourceId_EffectiveFrom] ON [ResourceCostProfiles] ([ResourceId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceCostProfiles_TenantId] ON [ResourceCostProfiles] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceOrganizationalUnits_OrganizationalUnitId] ON [ResourceOrganizationalUnits] ([OrganizationalUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceOrganizationalUnits_TenantId_OrganizationalUnitId_IsActive] ON [ResourceOrganizationalUnits] ([TenantId], [OrganizationalUnitId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceOrganizationalUnits_TenantId_ResourceId_EffectiveFrom] ON [ResourceOrganizationalUnits] ([TenantId], [ResourceId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ResourceOrganizationalUnits_TenantId_ResourceId_IsPrimary_IsActive] ON [ResourceOrganizationalUnits] ([TenantId], [ResourceId], [IsPrimary], [IsActive]) WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceReportingLines_TenantId_ManagerResourceId_IsActive] ON [ResourceReportingLines] ([TenantId], [ManagerResourceId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceReportingLines_TenantId_ResourceId_EffectiveFrom] ON [ResourceReportingLines] ([TenantId], [ResourceId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ResourceReportingLines_TenantId_ResourceId_IsPrimary_IsActive] ON [ResourceReportingLines] ([TenantId], [ResourceId], [IsPrimary], [IsActive]) WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceRequests_TenantId] ON [ResourceRequests] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_BaseCalendarId] ON [Resources] ([BaseCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_Email] ON [Resources] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_EmployeeId] ON [Resources] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_Status] ON [Resources] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_TenantId] ON [Resources] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_Type] ON [Resources] ([Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Resources_VendorId] ON [Resources] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ResourceSkills_ResourceId_SkillId] ON [ResourceSkills] ([ResourceId], [SkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceSkills_SkillId] ON [ResourceSkills] ([SkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_ResourceSkills_TenantId] ON [ResourceSkills] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskActions_RiskId] ON [RiskActions] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskActions_TenantId_RiskId_SortOrder] ON [RiskActions] ([TenantId], [RiskId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskActionUpdates_RiskActionId] ON [RiskActionUpdates] ([RiskActionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskActionUpdates_TenantId_RiskActionId_CreatedAt] ON [RiskActionUpdates] ([TenantId], [RiskActionId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskContingencies_RiskId] ON [RiskContingencies] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskImpactAssessments_RiskId] ON [RiskImpactAssessments] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RiskImpactAssessments_TenantId_RiskId] ON [RiskImpactAssessments] ([TenantId], [RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskImpactAssessmentTypes_RiskImpactAssessmentId] ON [RiskImpactAssessmentTypes] ([RiskImpactAssessmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RiskImpactAssessmentTypes_TenantId_RiskImpactAssessmentId_ImpactType] ON [RiskImpactAssessmentTypes] ([TenantId], [RiskImpactAssessmentId], [ImpactType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RiskMitigations_RiskId] ON [RiskMitigations] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Risks_CategoryId] ON [Risks] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Risks_TenantId_OwnerResourceId] ON [Risks] ([TenantId], [OwnerResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Risks_TenantId_ProjectId] ON [Risks] ([TenantId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Risks_TenantId_Status] ON [Risks] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_RolesId] ON [RolePermissions] ([RolesId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Skills_Name] ON [Skills] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Skills_TenantId] ON [Skills] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_StakeholderInvitations_ProjectId] ON [StakeholderInvitations] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_StakeholderInvitations_ProjectStakeholderId] ON [StakeholderInvitations] ([ProjectStakeholderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_StakeholderInvitations_TenantId_ResourceId_ProjectId_Status] ON [StakeholderInvitations] ([TenantId], [ResourceId], [ProjectId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StakeholderInvitations_TokenHash] ON [StakeholderInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_StrategicObjectives_PortfolioId] ON [StrategicObjectives] ([PortfolioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_SyncConflicts_IntegrationSyncId] ON [SyncConflicts] ([IntegrationSyncId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TeamMembers_ResourceId] ON [TeamMembers] ([ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TeamMembers_TeamId] ON [TeamMembers] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TeamMembers_TenantId] ON [TeamMembers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TeamMembers_TenantId_ResourceId_IsActive] ON [TeamMembers] ([TenantId], [ResourceId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TeamMembers_TenantId_TeamId_ResourceId] ON [TeamMembers] ([TenantId], [TeamId], [ResourceId]) WHERE [IsDeleted] = 0 AND [IsActive] = 1 AND [EffectiveTo] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Teams_DepartmentId] ON [Teams] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Teams_IsActive] ON [Teams] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Teams_Name] ON [Teams] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Teams_TenantId] ON [Teams] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Teams_TenantId_Code] ON [Teams] ([TenantId], [Code]) WHERE [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TenantPlugins_PluginId] ON [TenantPlugins] ([PluginId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_Activity_WorkDate] ON [TimeEntries] ([TenantId], [PlanActivityId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_Project_WorkDate] ON [TimeEntries] ([TenantId], [ProjectId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_Resource_WorkDate] ON [TimeEntries] ([TenantId], [ResourceId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_Role_WorkDate] ON [TimeEntries] ([TenantId], [ProjectRoleId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_TimerSession] ON [TimeEntries] ([TenantId], [TimerSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_Tenant_WorkItem_WorkDate] ON [TimeEntries] ([TenantId], [WorkItemId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_TimerSessionId] ON [TimeEntries] ([TimerSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimerSegments_Tenant_Session_StartedAt] ON [TimerSegments] ([TenantId], [TimerSessionId], [StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimerSegments_TimerSessionId] ON [TimerSegments] ([TimerSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_TimerSessions_Tenant_User_Status] ON [TimerSessions] ([TenantId], [UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TimerSessions_Active_Resource] ON [TimerSessions] ([TenantId], [ResourceId]) WHERE [IsDeleted] = 0 AND [Status] IN (''Running'', ''OnBreak'', ''Paused'', ''IdlePaused'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TimerSessions_Active_User] ON [TimerSessions] ([TenantId], [UserId]) WHERE [IsDeleted] = 0 AND [Status] IN (''Running'', ''OnBreak'', ''Paused'', ''IdlePaused'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserDeviceTokens_TenantId_UserId_IsActive] ON [UserDeviceTokens] ([TenantId], [UserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserDeviceTokens_TokenHash] ON [UserDeviceTokens] ([TokenHash]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserPermissionOverrides_PermissionId] ON [UserPermissionOverrides] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissionOverrides_TenantId_UserId_PermissionId] ON [UserPermissionOverrides] ([TenantId], [UserId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserPermissionOverrides_UserId] ON [UserPermissionOverrides] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserRoles_UserId] ON [UserRoles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_UserSessions_UserId] ON [UserSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Vendors_Name] ON [Vendors] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Vendors_TenantId] ON [Vendors] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Vendors_TenantId_Code] ON [Vendors] ([TenantId], [Code]) WHERE [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_Vendors_VendorCategoryId] ON [Vendors] ([VendorCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkBreakdownStructures_ParentId] ON [WorkBreakdownStructures] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkBreakdownStructures_ProjectId] ON [WorkBreakdownStructures] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowDefinitions_ActivePublishedVersionId] ON [WorkflowDefinitions] ([ActivePublishedVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowDefinitions_TenantId_Code] ON [WorkflowDefinitions] ([TenantId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowDefinitions_TenantId_Domain_IsActive] ON [WorkflowDefinitions] ([TenantId], [Domain], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowDefinitionVersions_TenantId_WorkflowDefinitionId_LifecycleStatus] ON [WorkflowDefinitionVersions] ([TenantId], [WorkflowDefinitionId], [LifecycleStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowDefinitionVersions_TenantId_WorkflowDefinitionId_VersionNumber] ON [WorkflowDefinitionVersions] ([TenantId], [WorkflowDefinitionId], [VersionNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowDefinitionVersions_WorkflowDefinitionId] ON [WorkflowDefinitionVersions] ([WorkflowDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowInstances_TenantId_EntityType_EntityId_WorkflowDefinitionId] ON [WorkflowInstances] ([TenantId], [EntityType], [EntityId], [WorkflowDefinitionId]) WHERE [IsDeleted] = 0 AND [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_WorkflowInstances_TenantId_EntityType_EntityId_WorkflowDefinitionId_IsActive] ON [WorkflowInstances] ([TenantId], [EntityType], [EntityId], [WorkflowDefinitionId], [IsActive]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowInstances_WorkflowDefinitionId] ON [WorkflowInstances] ([WorkflowDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowInstances_WorkflowDefinitionVersionId_CurrentStateId] ON [WorkflowInstances] ([WorkflowDefinitionVersionId], [CurrentStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowStates_TenantId_WorkflowDefinitionVersionId_Code] ON [WorkflowStates] ([TenantId], [WorkflowDefinitionVersionId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionActions_TenantId_WorkflowTransitionId_ExecutionOrder] ON [WorkflowTransitionActions] ([TenantId], [WorkflowTransitionId], [ExecutionOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionActions_WorkflowTransitionId] ON [WorkflowTransitionActions] ([WorkflowTransitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionGuards_TenantId_WorkflowTransitionId_ExecutionOrder] ON [WorkflowTransitionGuards] ([TenantId], [WorkflowTransitionId], [ExecutionOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionGuards_WorkflowTransitionId] ON [WorkflowTransitionGuards] ([WorkflowTransitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionHistory_FromStateId] ON [WorkflowTransitionHistory] ([FromStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionHistory_TenantId_WorkflowInstanceId_ExecutedAt] ON [WorkflowTransitionHistory] ([TenantId], [WorkflowInstanceId], [ExecutedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionHistory_ToStateId] ON [WorkflowTransitionHistory] ([ToStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionHistory_WorkflowInstanceId] ON [WorkflowTransitionHistory] ([WorkflowInstanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitionHistory_WorkflowTransitionId] ON [WorkflowTransitionHistory] ([WorkflowTransitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowTransitions_TenantId_WorkflowDefinitionVersionId_Code] ON [WorkflowTransitions] ([TenantId], [WorkflowDefinitionVersionId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkflowTransitions_TenantId_WorkflowDefinitionVersionId_FromStateId_ToStateId] ON [WorkflowTransitions] ([TenantId], [WorkflowDefinitionVersionId], [FromStateId], [ToStateId]) WHERE [IsDeleted] = 0 AND [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitions_WorkflowDefinitionVersionId_FromStateId] ON [WorkflowTransitions] ([WorkflowDefinitionVersionId], [FromStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkflowTransitions_WorkflowDefinitionVersionId_ToStateId] ON [WorkflowTransitions] ([WorkflowDefinitionVersionId], [ToStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkingCalendarDayRules_Tenant_Calendar_Day] ON [WorkingCalendarDayRules] ([TenantId], [WorkingCalendarId], [DayOfWeek]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkingCalendarDayRules_WorkingCalendarId] ON [WorkingCalendarDayRules] ([WorkingCalendarId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItemAssignments_WorkItemId] ON [WorkItemAssignments] ([WorkItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItemAttachments_WorkItemId_CreatedAt] ON [WorkItemAttachments] ([WorkItemId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItemComments_ParentCommentId] ON [WorkItemComments] ([ParentCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItemComments_WorkItemId_CreatedAt] ON [WorkItemComments] ([WorkItemId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItems_AssignedResourceId] ON [WorkItems] ([AssignedResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItems_AssignedTeamId] ON [WorkItems] ([AssignedTeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItems_ProjectId] ON [WorkItems] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    CREATE INDEX [IX_WorkItems_Status_ProjectId] ON [WorkItems] ([Status], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [ProjectChangeRequestApprovalHistories] ADD CONSTRAINT [FK_ProjectChangeRequestApprovalHistories_ProjectChangeRequestApprovals_ProjectChangeRequestApprovalId] FOREIGN KEY ([ProjectChangeRequestApprovalId]) REFERENCES [ProjectChangeRequestApprovals] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [ProjectChangeRequestApprovals] ADD CONSTRAINT [FK_ProjectChangeRequestApprovals_ProjectChangeRequests_ProjectChangeRequestId] FOREIGN KEY ([ProjectChangeRequestId]) REFERENCES [ProjectChangeRequests] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [ProjectChangeRequests] ADD CONSTRAINT [FK_ProjectChangeRequests_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [ProjectWorkflowConfigurations] ADD CONSTRAINT [FK_ProjectWorkflowConfigurations_WorkflowDefinitionVersions_EffectiveWorkflowVersionId] FOREIGN KEY ([EffectiveWorkflowVersionId]) REFERENCES [WorkflowDefinitionVersions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [ProjectWorkflowConfigurations] ADD CONSTRAINT [FK_ProjectWorkflowConfigurations_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    ALTER TABLE [WorkflowDefinitions] ADD CONSTRAINT [FK_WorkflowDefinitions_WorkflowDefinitionVersions_ActivePublishedVersionId] FOREIGN KEY ([ActivePublishedVersionId]) REFERENCES [WorkflowDefinitionVersions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029155617_InitialFopxPmBaseline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029155617_InitialFopxPmBaseline', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    ALTER TABLE [ApprovalActions] ADD [ApproverAssignmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [AK_Projects_TenantId_Id] UNIQUE ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    ALTER TABLE [PlanActivities] ADD CONSTRAINT [AK_PlanActivities_TenantId_Id] UNIQUE ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    ALTER TABLE [ApprovalRequests] ADD CONSTRAINT [AK_ApprovalRequests_TenantId_Id] UNIQUE ([TenantId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE TABLE [PaymentRequestApproverAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [ApprovalRequestId] uniqueidentifier NOT NULL,
        [ApproverUserId] uniqueidentifier NOT NULL,
        [Position] int NULL,
        [Requirement] nvarchar(24) NOT NULL,
        [State] nvarchar(24) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentRequestApproverAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PaymentRequestApproverAssignments_Tenant_Request_Id] UNIQUE ([TenantId], [ApprovalRequestId], [Id]),
        CONSTRAINT [CK_PaymentRequestApproverAssignments_Position] CHECK ([Position] IS NULL OR [Position] > 0),
        CONSTRAINT [CK_PaymentRequestApproverAssignments_Requirement] CHECK ([Requirement] IN (N'Mandatory', N'Optional')),
        CONSTRAINT [CK_PaymentRequestApproverAssignments_State] CHECK ([State] IN (N'Pending', N'Approved', N'Rejected')),
        CONSTRAINT [FK_PaymentRequestApproverAssignments_ApprovalRequests_TenantId_ApprovalRequestId] FOREIGN KEY ([TenantId], [ApprovalRequestId]) REFERENCES [ApprovalRequests] ([TenantId], [Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PaymentRequestApproverAssignments_Users_TenantId_ApproverUserId] FOREIGN KEY ([TenantId], [ApproverUserId]) REFERENCES [Users] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE TABLE [ProjectPaymentRequestDetails] (
        [ApprovalRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PlanActivityId] uniqueidentifier NULL,
        [ApprovalMode] nvarchar(24) NULL,
        [RequestKind] nvarchar(24) NOT NULL,
        [ReversesApprovalRequestId] uniqueidentifier NULL,
        [IdempotencyKey] uniqueidentifier NULL,
        [RequestFingerprint] nvarchar(128) NULL,
        [SubmittedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectPaymentRequestDetails] PRIMARY KEY ([ApprovalRequestId]),
        CONSTRAINT [CK_ProjectPaymentRequestDetails_ApprovalMode] CHECK ([ApprovalMode] IS NULL OR [ApprovalMode] IN (N'Sequential', N'Parallel')),
        CONSTRAINT [CK_ProjectPaymentRequestDetails_IdempotencyFingerprint] CHECK (([IdempotencyKey] IS NULL AND [RequestFingerprint] IS NULL) OR ([IdempotencyKey] IS NOT NULL AND [RequestFingerprint] IS NOT NULL)),
        CONSTRAINT [CK_ProjectPaymentRequestDetails_RequestKind] CHECK ([RequestKind] IN (N'PaymentRequest', N'ReversalRequest')),
        CONSTRAINT [CK_ProjectPaymentRequestDetails_ReversalReference] CHECK (([RequestKind] = N'PaymentRequest' AND [ReversesApprovalRequestId] IS NULL) OR ([RequestKind] = N'ReversalRequest' AND [ReversesApprovalRequestId] IS NOT NULL)),
        CONSTRAINT [FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ApprovalRequestId] FOREIGN KEY ([TenantId], [ApprovalRequestId]) REFERENCES [ApprovalRequests] ([TenantId], [Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ReversesApprovalRequestId] FOREIGN KEY ([TenantId], [ReversesApprovalRequestId]) REFERENCES [ApprovalRequests] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestDetails_PlanActivities_TenantId_PlanActivityId] FOREIGN KEY ([TenantId], [PlanActivityId]) REFERENCES [PlanActivities] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestDetails_Projects_TenantId_ProjectId] FOREIGN KEY ([TenantId], [ProjectId]) REFERENCES [Projects] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE INDEX [IX_ApprovalActions_TenantId_ApprovalRequestId_ApproverAssignmentId] ON [ApprovalActions] ([TenantId], [ApprovalRequestId], [ApproverAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ApprovalActions_ApproverAssignment] ON [ApprovalActions] ([ApproverAssignmentId]) WHERE [ApproverAssignmentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE INDEX [IX_PaymentRequestApproverAssignments_TenantId_ApproverUserId] ON [PaymentRequestApproverAssignments] ([TenantId], [ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentRequestApproverAssignments_Request_Approver] ON [PaymentRequestApproverAssignments] ([TenantId], [ApprovalRequestId], [ApproverUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PaymentRequestApproverAssignments_Request_Position] ON [PaymentRequestApproverAssignments] ([TenantId], [ApprovalRequestId], [Position]) WHERE [Position] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestDetails_ReversesRequest] ON [ProjectPaymentRequestDetails] ([TenantId], [ReversesApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestDetails_Tenant_Project_Kind] ON [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [RequestKind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId] ON [ProjectPaymentRequestDetails] ([TenantId], [ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestDetails_TenantId_PlanActivityId] ON [ProjectPaymentRequestDetails] ([TenantId], [PlanActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProjectPaymentRequestDetails_Project_Idempotency] ON [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    ALTER TABLE [ApprovalActions] ADD CONSTRAINT [FK_ApprovalActions_PaymentRequestApproverAssignments_TenantId_ApprovalRequestId_ApproverAssignmentId] FOREIGN KEY ([TenantId], [ApprovalRequestId], [ApproverAssignmentId]) REFERENCES [PaymentRequestApproverAssignments] ([TenantId], [ApprovalRequestId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029181208_ProjectPaymentRequestPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029181208_ProjectPaymentRequestPersistence', N'10.0.10');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    DROP INDEX [IX_Roles_Name] ON [Roles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    DROP INDEX [IX_Permissions_Resource_Action] ON [Permissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Tenants_Status] CHECK ([Status] IN (N'Provisioning', N'Active', N'Suspended', N'Retired'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Roles_Tenant_Name] ON [Roles] ([TenantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Permissions_Tenant_Resource_Action] ON [Permissions] ([TenantId], [Resource], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    CREATE INDEX [IX_Tenants_Status] ON [Tenants] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Tenants_Code] ON [Tenants] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN

    INSERT INTO [Tenants] ([Id], [Code], [DisplayName], [Status], [CreatedAt], [CreatedBy])
    SELECT legacy.[TenantId],
           N'LEGACY-' + REPLACE(CONVERT(nvarchar(36), legacy.[TenantId]), N'-', N''),
           N'Legacy Tenant ' + CONVERT(nvarchar(36), legacy.[TenantId]),
           N'Active',
           SYSUTCDATETIME(),
           N'Migration'
    FROM (
        SELECT [TenantId] FROM [Users] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
        UNION
        SELECT [TenantId] FROM [Organizations] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
        UNION
        SELECT [TenantId] FROM [Roles] WHERE [TenantId] <> '00000000-0000-0000-0000-000000000000'
        UNION
        SELECT [TenantId] FROM [ConfigurationSettings] WHERE [TenantId] IS NOT NULL AND [TenantId] <> '00000000-0000-0000-0000-000000000000'
    ) AS legacy
    WHERE NOT EXISTS (SELECT 1 FROM [Tenants] AS registered WHERE registered.[Id] = legacy.[TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    ALTER TABLE [ConfigurationSettings] ADD CONSTRAINT [FK_ConfigurationSettings_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    ALTER TABLE [Organizations] ADD CONSTRAINT [FK_Organizations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029183557_AddTenantRegistry'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029183557_AddTenantRegistry', N'10.0.10');
END;

COMMIT;
GO

