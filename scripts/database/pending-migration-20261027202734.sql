BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027202734_AddStatusReportPaymentSnapshotFields'
)
BEGIN
    ALTER TABLE [ProjectStatusReports] ADD [ActualPaidSnapshot] decimal(19,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027202734_AddStatusReportPaymentSnapshotFields'
)
BEGIN
    ALTER TABLE [ProjectStatusReports] ADD [OutstandingPlannedPaymentsSnapshot] decimal(19,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027202734_AddStatusReportPaymentSnapshotFields'
)
BEGIN
    ALTER TABLE [ProjectStatusReports] ADD [PaymentsDueWithinPeriodSnapshot] decimal(19,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027202734_AddStatusReportPaymentSnapshotFields'
)
BEGIN
    ALTER TABLE [ProjectStatusReports] ADD [RemainingUnpaidAtPeriodEndSnapshot] decimal(19,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027202734_AddStatusReportPaymentSnapshotFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027202734_AddStatusReportPaymentSnapshotFields', N'10.0.10');
END;

COMMIT;
GO

