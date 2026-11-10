BEGIN TRANSACTION;
ALTER TABLE [ProjectAnnualBudgets] ADD [DraftApprovalConfigurationJson] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson', N'10.0.10');

COMMIT;
GO

