# Superseded scaffold capture

These files capture an intermediate EF scaffold generated during the Sprint 2 persistence work. It was superseded before any database operation because its index narrowed the shared `ApprovalRequests.Type` and `Status` columns. It is not an active migration and must not be applied.

The active source migration is `apps/api/src/NEXUS.Persistence/Migrations/20261029181208_ProjectPaymentRequestPersistence.cs`.
