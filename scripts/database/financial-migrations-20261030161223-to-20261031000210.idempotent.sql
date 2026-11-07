BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] DROP CONSTRAINT [FK_PaymentSettlements_PaymentObligations_TenantId_PaymentObligationId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] DROP CONSTRAINT [FK_ProjectPaymentRequestDetails_ApprovalRequests_TenantId_ReversesApprovalRequestId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    DROP INDEX [IX_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId] ON [ProjectPaymentRequestDetails];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] DROP CONSTRAINT [CK_ProjectPaymentRequestDetails_ReversalReference];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] DROP CONSTRAINT [CK_ProjectAnnualBudgets_Amounts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    DROP INDEX [IX_PaymentSettlements_TenantId_PaymentObligationId_SettlementDate] ON [PaymentSettlements];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] ADD [FundingTransferId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] ADD [ApprovedReservedCarryForward] decimal(19,4) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] ADD [ClosedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] ADD [ClosedBy] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] ADD [ClosedByClosingId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentSettlements]') AND [c].[name] = N'PaymentObligationId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [PaymentSettlements] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [PaymentSettlements] ALTER COLUMN [PaymentObligationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD [Currency] nvarchar(3) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD [Description] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD [FundingTransferId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD [ProjectId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD [ProjectPaymentRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [AK_ProjectPaymentRequestDetails_Tenant_Project_Request] UNIQUE ([TenantId], [ProjectId], [ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [AK_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId] UNIQUE ([TenantId], [ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentObligations] ADD CONSTRAINT [AK_PaymentObligations_Tenant_Project_Id] UNIQUE ([TenantId], [ProjectId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE TABLE [ProjectAnnualBudgetClosings] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SourceAnnualBudgetId] uniqueidentifier NOT NULL,
        [FiscalYear] int NOT NULL,
        [FiscalYearStartDate] date NOT NULL,
        [FiscalYearEndDate] date NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [DestinationAnnualBudgetId] uniqueidentifier NULL,
        [DestinationFiscalYear] int NULL,
        [ApprovalRequestId] uniqueidentifier NULL,
        [Status] nvarchar(24) NOT NULL,
        [ApprovalMode] nvarchar(24) NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [SnapshotFingerprint] nvarchar(128) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [ApprovedAnnualBudget] decimal(19,4) NOT NULL,
        [NewFunding] decimal(19,4) NOT NULL,
        [IncomingCarryForward] decimal(19,4) NOT NULL,
        [ApprovedAdjustments] decimal(19,4) NOT NULL,
        [ApprovedPaymentRequestAllocations] decimal(7,4) NOT NULL,
        [ApprovedReversalReleases] decimal(19,4) NOT NULL,
        [EffectiveActiveAllocations] decimal(7,4) NOT NULL,
        [ActualSettlementsInFiscalYear] decimal(19,4) NOT NULL,
        [OutstandingCommitments] decimal(19,4) NOT NULL,
        [FreeAvailableBudget] decimal(19,4) NOT NULL,
        [TotalUnspentBudget] decimal(19,4) NOT NULL,
        [ProposedFreeCarryForward] decimal(19,4) NOT NULL,
        [CarryOutstandingCommitments] bit NOT NULL,
        [ApprovedFreeCarryForward] decimal(19,4) NOT NULL,
        [ApprovedReservedCarryForward] decimal(19,4) NOT NULL,
        [NonCarriedFreeBalance] decimal(19,4) NOT NULL,
        [NextFiscalYearNewFunding] decimal(19,4) NOT NULL,
        [NextFiscalYearAdjustments] decimal(19,4) NOT NULL,
        [PendingPaymentRequestCount] int NOT NULL,
        [PendingReversalRequestCount] int NOT NULL,
        [UnresolvedLegacyRequestCount] int NOT NULL,
        [IntegrityIssueCount] int NOT NULL,
        [IntegrityIssuesJson] nvarchar(max) NULL,
        [PreparedBy] uniqueidentifier NULL,
        [PreparedAtUtc] datetime2 NULL,
        [SubmittedBy] uniqueidentifier NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [CompletedBy] uniqueidentifier NULL,
        [CompletedAtUtc] datetime2 NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectAnnualBudgetClosings] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ProjectAnnualBudgetClosings_Tenant_Project_Id] UNIQUE ([TenantId], [ProjectId], [Id]),
        CONSTRAINT [CK_ProjectAnnualBudgetClosings_CarryForward] CHECK ([ProposedFreeCarryForward] >= 0 AND [ApprovedFreeCarryForward] >= 0 AND [ApprovedReservedCarryForward] >= 0 AND [NonCarriedFreeBalance] >= 0),
        CONSTRAINT [CK_ProjectAnnualBudgetClosings_FiscalPeriod] CHECK ([FiscalYear] BETWEEN 1900 AND 2200 AND [FiscalYearStartDate] <= [FiscalYearEndDate]),
        CONSTRAINT [CK_ProjectAnnualBudgetClosings_Status] CHECK ([Status] IN (N'Review', N'PendingApproval', N'Rejected', N'Closed', N'Cancelled')),
        CONSTRAINT [FK_ProjectAnnualBudgetClosings_ApprovalRequests_TenantId_ApprovalRequestId] FOREIGN KEY ([TenantId], [ApprovalRequestId]) REFERENCES [ApprovalRequests] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectAnnualBudgetClosings_ProjectAnnualBudgets_TenantId_ProjectId_DestinationAnnualBudgetId] FOREIGN KEY ([TenantId], [ProjectId], [DestinationAnnualBudgetId]) REFERENCES [ProjectAnnualBudgets] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectAnnualBudgetClosings_ProjectAnnualBudgets_TenantId_ProjectId_SourceAnnualBudgetId] FOREIGN KEY ([TenantId], [ProjectId], [SourceAnnualBudgetId]) REFERENCES [ProjectAnnualBudgets] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectAnnualBudgetClosings_Projects_TenantId_ProjectId] FOREIGN KEY ([TenantId], [ProjectId]) REFERENCES [Projects] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE TABLE [ProjectPaymentRequestFundingTransfers] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PaymentRequestId] uniqueidentifier NOT NULL,
        [SourceAnnualBudgetId] uniqueidentifier NOT NULL,
        [SourceFiscalYear] int NOT NULL,
        [DestinationAnnualBudgetId] uniqueidentifier NOT NULL,
        [DestinationFiscalYear] int NOT NULL,
        [ClosingId] uniqueidentifier NOT NULL,
        [SupersedesFundingTransferId] uniqueidentifier NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [IdempotencyKey] uniqueidentifier NOT NULL,
        [ApprovedBy] uniqueidentifier NOT NULL,
        [ApprovedAtUtc] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ProjectPaymentRequestFundingTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ProjectPaymentRequestFundingTransfers_Tenant_Project_Id] UNIQUE ([TenantId], [ProjectId], [Id]),
        CONSTRAINT [CK_ProjectPaymentRequestFundingTransfers_Amount] CHECK ([Amount] > 0 AND [SourceFiscalYear] BETWEEN 1900 AND 2199 AND [DestinationFiscalYear] = [SourceFiscalYear] + 1 AND [Currency] <> N''),
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgetClosings_TenantId_ProjectId_ClosingId] FOREIGN KEY ([TenantId], [ProjectId], [ClosingId]) REFERENCES [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgets_TenantId_ProjectId_DestinationAnnualBudgetId] FOREIGN KEY ([TenantId], [ProjectId], [DestinationAnnualBudgetId]) REFERENCES [ProjectAnnualBudgets] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_ProjectAnnualBudgets_TenantId_ProjectId_SourceAnnualBudgetId] FOREIGN KEY ([TenantId], [ProjectId], [SourceAnnualBudgetId]) REFERENCES [ProjectAnnualBudgets] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_ProjectPaymentRequestDetails_TenantId_ProjectId_PaymentRequestId] FOREIGN KEY ([TenantId], [ProjectId], [PaymentRequestId]) REFERENCES [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [ApprovalRequestId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_SupersedesFundingTransferId] FOREIGN KEY ([TenantId], [ProjectId], [SupersedesFundingTransferId]) REFERENCES [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPaymentRequestFundingTransfers_Projects_TenantId_ProjectId] FOREIGN KEY ([TenantId], [ProjectId]) REFERENCES [Projects] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestDetails_TenantId_ProjectId_FundingTransferId] ON [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [FundingTransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId] ON [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [ReversesApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [CK_ProjectPaymentRequestDetails_ReversalReference] CHECK (([RequestKind] = N''PaymentRequest'' AND [ReversesApprovalRequestId] IS NULL) OR ([RequestKind] = N''ReversalRequest'' AND [ReversesApprovalRequestId] IS NOT NULL AND [ReversesApprovalRequestId] <> [ApprovalRequestId]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgets_TenantId_ProjectId_ClosedByClosingId] ON [ProjectAnnualBudgets] ([TenantId], [ProjectId], [ClosedByClosingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [ProjectAnnualBudgets] ADD CONSTRAINT [CK_ProjectAnnualBudgets_Amounts] CHECK ([NewApprovedFunding] >= 0 AND [ApprovedCarryForward] >= 0 AND [ApprovedReservedCarryForward] >= 0 AND [ApprovedReservedCarryForward] <= [ApprovedCarryForward] AND ([NewApprovedFunding] + [ApprovedCarryForward] + [ApprovedAdjustments]) >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [ProjectAnnualBudgets] ADD CONSTRAINT [CK_ProjectAnnualBudgets_ClosingMetadata] CHECK (([ClosedAtUtc] IS NULL AND [ClosedByClosingId] IS NULL AND [ClosedBy] IS NULL) OR ([ClosedAtUtc] IS NOT NULL AND [ClosedByClosingId] IS NOT NULL AND [ClosedBy] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_PaymentSettlements_TenantId_ProjectId_FundingTransferId] ON [PaymentSettlements] ([TenantId], [ProjectId], [FundingTransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_PaymentSettlements_TenantId_ProjectId_PaymentObligationId_SettlementDate] ON [PaymentSettlements] ([TenantId], [ProjectId], [PaymentObligationId], [SettlementDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_PaymentSettlements_TenantId_ProjectId_ProjectPaymentRequestId_SettlementDate] ON [PaymentSettlements] ([TenantId], [ProjectId], [ProjectPaymentRequestId], [SettlementDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PaymentSettlements_PaymentRequest_Reference] ON [PaymentSettlements] ([TenantId], [ProjectId], [ProjectPaymentRequestId], [PaymentReference]) WHERE [ProjectPaymentRequestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [CK_PaymentSettlements_ExactlyOneOwner] CHECK (([PaymentObligationId] IS NOT NULL AND [ProjectPaymentRequestId] IS NULL) OR ([PaymentObligationId] IS NULL AND [ProjectPaymentRequestId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [CK_PaymentSettlements_FundingTransferOwner] CHECK ([FundingTransferId] IS NULL OR ([ProjectPaymentRequestId] IS NOT NULL AND [PaymentObligationId] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [CK_PaymentSettlements_RequestCurrencyDescription] CHECK (([ProjectPaymentRequestId] IS NULL) OR ([Currency] IS NOT NULL AND LEN([Currency]) = 3 AND [Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetClosings_Project_FiscalYear_Status] ON [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [FiscalYear], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetClosings_TenantId_ApprovalRequestId] ON [ProjectAnnualBudgetClosings] ([TenantId], [ApprovalRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetClosings_TenantId_ProjectId_DestinationAnnualBudgetId] ON [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [DestinationAnnualBudgetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectAnnualBudgetClosings_TenantId_ProjectId_SourceAnnualBudgetId] ON [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [SourceAnnualBudgetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ProjectAnnualBudgetClosings_Idempotency] ON [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProjectAnnualBudgetClosings_Pending_Project_FiscalYear] ON [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [FiscalYear]) WHERE [Status] = N''PendingApproval''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestFundingTransfers_DestinationBudget_Request] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [DestinationAnnualBudgetId], [PaymentRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestFundingTransfers_SourceBudget_Request] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [SourceAnnualBudgetId], [PaymentRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE INDEX [IX_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_ClosingId] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [ClosingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProjectPaymentRequestFundingTransfers_ActiveChainSuccessor] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [SupersedesFundingTransferId]) WHERE [SupersedesFundingTransferId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ProjectPaymentRequestFundingTransfers_Idempotency] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ProjectPaymentRequestFundingTransfers_Request_DestinationFiscalYear] ON [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [PaymentRequestId], [DestinationFiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [FK_PaymentSettlements_PaymentObligations_TenantId_ProjectId_PaymentObligationId] FOREIGN KEY ([TenantId], [ProjectId], [PaymentObligationId]) REFERENCES [PaymentObligations] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [FK_PaymentSettlements_ProjectPaymentRequestDetails_TenantId_ProjectId_ProjectPaymentRequestId] FOREIGN KEY ([TenantId], [ProjectId], [ProjectPaymentRequestId]) REFERENCES [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [ApprovalRequestId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [PaymentSettlements] ADD CONSTRAINT [FK_PaymentSettlements_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId] FOREIGN KEY ([TenantId], [ProjectId], [FundingTransferId]) REFERENCES [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectAnnualBudgets] ADD CONSTRAINT [FK_ProjectAnnualBudgets_ProjectAnnualBudgetClosings_TenantId_ProjectId_ClosedByClosingId] FOREIGN KEY ([TenantId], [ProjectId], [ClosedByClosingId]) REFERENCES [ProjectAnnualBudgetClosings] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [FK_ProjectPaymentRequestDetails_ProjectPaymentRequestDetails_TenantId_ProjectId_ReversesApprovalRequestId] FOREIGN KEY ([TenantId], [ProjectId], [ReversesApprovalRequestId]) REFERENCES [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [ApprovalRequestId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [FK_ProjectPaymentRequestDetails_ProjectPaymentRequestFundingTransfers_TenantId_ProjectId_FundingTransferId] FOREIGN KEY ([TenantId], [ProjectId], [FundingTransferId]) REFERENCES [ProjectPaymentRequestFundingTransfers] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031000210_AddFinancialPersistenceSprints3to6'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031000210_AddFinancialPersistenceSprints3to6', N'10.0.10');
END;

COMMIT;
GO

