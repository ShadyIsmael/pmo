BEGIN TRANSACTION;
ALTER TABLE [ProjectPaymentRequestDetails] ADD [AnnualBudgetFiscalYear] int NULL;

ALTER TABLE [ProjectPaymentRequestDetails] ADD [AnnualBudgetReference] nvarchar(100) NULL;

ALTER TABLE [ProjectPaymentRequestDetails] ADD [ProjectAnnualBudgetId] uniqueidentifier NULL;

ALTER TABLE [ProjectAnnualBudgets] ADD CONSTRAINT [AK_ProjectAnnualBudgets_Tenant_Project_Id] UNIQUE ([TenantId], [ProjectId], [Id]);

CREATE INDEX [IX_ProjectPaymentRequestDetails_AnnualBudget] ON [ProjectPaymentRequestDetails] ([TenantId], [ProjectId], [ProjectAnnualBudgetId]);

ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [CK_ProjectPaymentRequestDetails_AnnualBudgetAssociation] CHECK (([ProjectAnnualBudgetId] IS NULL AND [AnnualBudgetFiscalYear] IS NULL AND [AnnualBudgetReference] IS NULL) OR ([ProjectAnnualBudgetId] IS NOT NULL AND [AnnualBudgetFiscalYear] BETWEEN 1900 AND 2200 AND [AnnualBudgetReference] IS NOT NULL));

ALTER TABLE [ProjectPaymentRequestDetails] ADD CONSTRAINT [FK_ProjectPaymentRequestDetails_ProjectAnnualBudgets_TenantId_ProjectId_ProjectAnnualBudgetId] FOREIGN KEY ([TenantId], [ProjectId], [ProjectAnnualBudgetId]) REFERENCES [ProjectAnnualBudgets] ([TenantId], [ProjectId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation', N'10.0.10');

COMMIT;
GO

