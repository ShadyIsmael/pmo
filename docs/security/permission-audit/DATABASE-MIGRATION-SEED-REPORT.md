# Database Migration and Seed Report

**Snapshot:** 2026-11-02  
**Target requested by owner:** Development SQL Server Shady, database NexusPmo  
**Current status:** SOURCE MODEL READY; LIVE DATABASE NOT VERIFIED OR MODIFIED

**Policy addendum (2026-11-04):** This report's verification and backup statuses are historical observations from 2026-11-02. Current repository policy allows a no-backup reset only for explicitly Product Owner-authorized Development `Shady/NexusPmo`, after exact environment/server/database verification. All EF migrations, the required reset-specific seed, and post-seed checks remain required; ordinary writes retain the backup gate.

**Minimal Seed addendum (2026-11-04):** For the clean Admin-bootstrap reset, the required post-migration seed is now `--seed-minimal-only` run twice; `--seed-only` remains the separate unchanged Demo Seed and is not used. Historical statuses below remain unchanged: no reset, migration, or seed was executed in the recorded snapshot, and the current live database contents remain unverified by this report.

## Model and migrations

The repository EF gate reports:

- Context: NEXUS.Persistence.AppDbContext
- Migration assembly: NEXUS.Persistence
- Source migrations: 10
- EF Model synchronized: Yes
- Database connection opened by offline model check: No

The tenth source migration is 20261102190318_AddStableSystemRoleIdentity. It adds the immutable Role.SystemRoleKey identity used for the protected tenant Admin role. Source migrations and snapshot synchronize offline. This result does not establish the live database's applied migration history or physical schema.

## Required documented database workflow

| Gate | Result |
|---|---|
| Actual SQL Server identity | Not verified |
| Authenticated SQL principal | Not returned |
| Exact Development database identity | Not verified |
| Live pending migration count/names | Not run |
| Independent backup | Not verified |
| Migrations applied to Development | No |
| __EFMigrationsHistory verified | Not run |
| Physical schema verified | Not run |
| Persistence tests against Development | Not run |

The process identity was shady\shady. The read-only Windows-authenticated sqlcmd probe to tcp:localhost,1433 failed before authentication with a socket access-permission error and login timeout. It returned no SQL server, login, database, or DB_ID. The local MSSQLSERVER service/listener was not visible from this executor. A successful process identity or TCP-port probe would not establish the SQL principal or target.

At the time of this 2026-11-02 snapshot, process variables ASPNETCORE_ENVIRONMENT, NEXUS_EF_DEVELOPMENT_CONNECTION_STRING, and NEXUS_ADMIN_BOOTSTRAP_PASSWORD were absent, and no SQL operation succeeded; the database was not dropped, created, migrated, read, or seeded. The reset's current backup exception is documented in the 2026-11-04 policy addendum above; missing target verification, Product Owner authorization, or bootstrap readiness still blocks a reset.

## Seed readiness from source

SeedDataProvider.cs defines active canonical permission rows, deterministic system-role identities, tenant bootstrap membership, and the catalog-derived Tenant Admin baseline. The protected role identity is tenant.admin. Current source baseline is 48 permissions, derived dynamically from active Tenant-scoped Administration metadata plus approved Portfolio, Program, and Handover keys. Platform, CrossTenant, SystemOwner, and unapproved Organization-level permissions remain excluded.

Source inspection and the EF model gate do not prove that the seed runs against a fresh SQL database. The backend testhost failed to connect before tests executed, so seed tests and persistence tests are NOT EXECUTED. The secure Admin bootstrap password variable is absent.

## Required execution after host connection and explicit Product Owner authorization

1. Set the process environment to Development and provide the approved secure Development connection through the repository gate's supported secret/environment mechanism.
2. Confirm @@SERVERNAME, SYSTEM_USER, DB_NAME(), DB_ID(), and the expected Shady/NexusPmo target before writes.
3. Confirm Product Owner authorization to reset only Development `Shady/NexusPmo` is recorded in the active task/conversation and provide its reference to the EF gate. No backup is required for this exact reset exception.
4. Review the full empty-database migration path and execute the repository EF gate reset action only after the live environment/server/database identity is confirmed.
5. Confirm all ten migrations in __EFMigrationsHistory and verify the physical schema.
6. The reset gate runs `--seed-minimal-only` twice with the secure bootstrap secret after all migrations; verify both pass results and idempotency, exact canonical keys, zero duplicates, required roles, bootstrap membership, protected Admin grants, settings defaults, and reference data. Do not substitute the full Demo Seed (`--seed-only`).

**Database completion fields:** model synchronized: Yes; live pending migrations: Not run; Development identity verified: No; backup verified: No; migrations applied: No; physical schema verified: Not run; persistence tests: Not run; seed twice: No; blocker: SQL socket is inaccessible from this execution environment and required Development/bootstrap variables are absent.
