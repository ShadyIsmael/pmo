# Sprint 1 / Task 06 — Database Persistence & Migration Verification

**Repository:** `C:\Projects\pmo`  
**Date:** 2026-11-22  
**Status:** The reviewed precision migration was applied to the verified Development target. Model/schema synchronization is complete, and read-only Actual Paid reconciliation is clean. Focused financial tests passed, but the full test project remains blocked by pre-existing compilation errors and several non-SQL-Server harness failures.  
**Scope:** Development database gate, one authorized precision migration, persistence/reporting verification, read-only financial reconciliation, migration-source integrity, and build/test evidence. No reset, drop, truncate, reseed, destructive migration, historical rewrite, Production access, Agent UAT, or Unified Planned Payment redesign was performed.

## Execution status

| Work item | Status |
|---|---|
| Read repository instructions and existing Sprint 1 finance reports | Done |
| Development identity, history, physical schema, and target SQL gate | Done; all mutation prerequisites passed |
| Apply `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision` | Done; hash-bound apply, gate exit 0 |
| Independent post-apply identity/history/schema verification | Done; 34/34 applied, 0 pending |
| Read-only Actual Paid reconciliation | Done; obligation rail reconciled, no double-count or scope/currency mismatches |
| Migration-source integrity review | Done; 34 registered migrations, no history-only migrations, no deleted legacy source restored |
| API and focused test verification | Done with documented pre-existing blockers |
| This report | Done |

## 1. Development target identity and database gate

The active API Development configuration resolves to `localhost,1433 / NexusPmo` with integrated authentication. The gate was run with `ASPNETCORE_ENVIRONMENT=Development` and `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` loaded from `apps/api/src/NEXUS.Api/appsettings.Development.json` into the process only; no connection string was persisted or printed.

| Identity field | Result |
|---|---|
| Effective endpoint | `localhost,1433 / NexusPmo` |
| Expected repository target | `Shady / NexusPmo` |
| `SERVERPROPERTY('ServerName')` | `Shady` |
| `DB_NAME()` | `NexusPmo` |
| Database ID | `5` |
| Target verified before apply | Yes — Development |
| Target verified after apply | Yes — Development |

Read-only `sqlcmd` metadata queries and the EF Database Gate independently returned the same identity both before and after the migration. The gate reported:

- Environment: `Development`.
- Target Development DB verified: `Yes (server=Shady; database=NexusPmo; databaseId=5)`.
- EF Model synchronized: `Yes`.
- Repository migrations: `34`.
- Applied migrations: `34`.
- Pending migrations: `0`.
- Physical schema verified against last applied model: `Yes`.
- Objects missing from last applied model: `0`.
- Objects missing from current repository model: `0`.
- Gates exit code: `0`.

No history-only migrations were reported. No baseline-drift override was requested or required; the apply ran without `--allow-unrelated-baseline-drift-sha256`.

## 2. Precision migration review and application

The sole pending migration was:

`20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision`

Pre-apply source evidence:

- `Up()` contains exactly two `AlterColumn<decimal>` operations:
  - `ProjectAnnualBudgetClosings.EffectiveActiveAllocations`: `decimal(7,4)` → `decimal(19,4)`, non-null.
  - `ProjectAnnualBudgetClosings.ApprovedPaymentRequestAllocations`: `decimal(7,4)` → `decimal(19,4)`, non-null.
- `Down()` reverses those two definitions only.
- No table, index, foreign-key, data, seed, or permission operation exists in the migration.
- Pre-apply physical metadata confirmed both columns were `decimal(7,4) NOT NULL`, with no default constraints, and `ProjectAnnualBudgetClosings` contained `0` rows.

The target-specific SQL was regenerated from the verified live migration history and reviewed. It contains only:

1. A conditional drop of a default constraint for `EffectiveActiveAllocations` (no such constraint existed on the target).
2. `ALTER COLUMN ... decimal(19,4) NOT NULL` for `EffectiveActiveAllocations`.
3. A conditional drop of a default constraint for `ApprovedPaymentRequestAllocations` (no such constraint existed on the target).
4. `ALTER COLUMN ... decimal(19,4) NOT NULL` for `ApprovedPaymentRequestAllocations`.
5. The migration-history insert with `ProductVersion` `10.0.10`.

The regenerated SQL SHA-256 matched the previously reviewed value:

`5AC262122EEAE08331200113479E8448149AF608CBAA62738D50F19028A646BE`

The migration was applied with the gate's hash-bound `apply` command after identity, history, model, and schema checks passed. Gate exit code was `0`. Backup was **Not requested**, which is valid for Development under the repository policy and was not a blocker.

Post-apply verification:

| Check | Result |
|---|---|
| Applied migrations in `dbo.__EFMigrationsHistory` | `34` |
| Target migration history row | Present, `ProductVersion = 10.0.10` |
| Pending migrations | `0` |
| `EffectiveActiveAllocations` | `decimal(19,4) NOT NULL` |
| `ApprovedPaymentRequestAllocations` | `decimal(19,4) NOT NULL` |
| Closing rows affected | `0` before and after widening |
| EF model synchronized | Yes |
| Physical schema verified | Yes |

Source hashes recorded for this run:

| Artifact | SHA-256 |
|---|---|
| `artifacts/ef-review/development-pending.sql` | `5AC262122EEAE08331200113479E8448149AF608CBAA62738D50F19028A646BE` |
| `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.cs` | `8F8063D026ACD53AF8435C0FE385D7B53CAEEF7792BCBB420AB0704C815A6C68` |
| `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.Designer.cs` | `D573BD3FAEED28577FEBAED1E0A6D4B718C8E0C33FFAD17E10BF6FF664372040` |

## 3. Migration-source integrity

- EF discovery resolves `34` registered migrations. There are `35` migration `.cs` source files on disk because `20261117200000_AddProjectLifecycleCatalogs.cs` is an unregistered draft class (`PendingAddProjectLifecycleCatalogsDraft`) with no `[Migration]`/`[DbContext]` attributes and no Designer. It is not in migration history, is not pending, and does not create a duplicate migration ID.
- `AppDbContextModelSnapshot.cs` and the target Designer both record the two closing columns as `decimal(19,4)`. No other decimal/precision difference was identified between them.
- The deleted legacy Planned Payment sources remain deleted and were not restored:
  - `20261026160000_AddPlannedPaymentLifecycle.cs` and Designer.
  - `20261026170000_AddPaymentTenantScopedRelationships.cs` and Designer.
- No documentation evidence showed those deleted migrations had been applied to Development. Their history inserts exist only in historical SQL artifacts and preflight scripts; no runtime history row or new source restoration was created.
- No applied migration history was removed, rewritten, or reset.
- No source files were modified by this verification run. The reproducible review SQL under `artifacts/ef-review/` and this report are the only files created.

Source-level risk retained:

`AppDbContext` applies `GetDecimalPrecision(property.Name)` after entity configuration and hard-codes exact-name `(19,4)` exceptions for the two corrected closing properties. The correction is therefore correct for the current names, but fragile if either property is renamed. Other properties whose names contain `Allocation`, `Percent`, `Utilization`, or `Progress` still resolve to `decimal(7,4)`. This is not a current model/schema mismatch, but it should be addressed before adding new similarly named monetary fields.

## 4. Read-only Actual Paid reconciliation

Reconciliation was performed with read-only SQL against the verified Development database, using the owner-resolved rules implemented in `ActualPaidSettlementCalculator`:

- Settlements are the only cash facts.
- Obligation settlements join through `PaymentObligation` by tenant and owner ID.
- Request settlements would join through `ProjectPaymentRequestDetail.ApprovalRequestId`.
- Project is resolved from the owner, not from `PaymentSettlement.ProjectId` alone.
- Obligation settlement currency falls back to the obligation currency; request settlement currency is not guessed.
- Foreign currency is excluded from a different-currency total and surfaced rather than converted.

Live data profile:

| Measure | Result |
|---|---|
| Active settlements | `3` |
| Both owners on one settlement | `0` |
| No owner | `0` |
| Obligation-only settlements | `3` |
| Request-only settlements | `0` |
| Missing/implicit owner mismatch | `0` |
| Obligation/request project mismatch | `0` |
| Obligation/request tenant mismatch | `0` |
| Distinct resolved currency | `1` (`EGP`) |
| Total reconciled Actual Paid | `50,208.0000 EGP` |

Settlement detail:

| Settlement | Rail | Amount | Raw settlement currency | Resolved currency | Owner project |
|---|---|---:|---|---|---|
| `4C65BFF2-5940-4174-B2AB-533AB34A2199` | Obligation | `49,660.0000` | NULL | `EGP` (obligation fallback) | `B22DD6A1-0426-4000-BAD5-D996A471EDA5` |
| `E2738170-E629-450A-A139-C515FBDE0C69` | Obligation | `475.0000` | NULL | `EGP` (obligation fallback) | `B22DD6A1-0426-4000-BAD5-D996A471EDA5` |
| `D4AA1AF1-EAEF-4C93-BA44-2440AA49DE4C` | Obligation | `73.0000` | NULL | `EGP` (obligation fallback) | `B22DD6A1-0426-4000-BAD5-D996A471EDA5` |

Obligation reconciliation:

| Obligation | Amount | Settled | Outstanding | Currency |
|---|---:|---:|---:|---|
| `092EFFD9-8A40-4374-B57C-16424009DC18` | `548.0000` | `548.0000` | `0.0000` | `EGP` |
| `AFEB156C-5A86-4E18-89A7-C9BD661819F6` | `149,452.0000` | `49,660.0000` | `99,792.0000` | `EGP` |

Interpretation:

- Partial settlement is handled correctly: the two smaller settlements fully settle the `548 EGP` obligation; the larger obligation remains partially settled.
- The three settlements are each counted once under their owner; no obligation, its settlement, a linked request, and its settlement were summed as separate cash amounts.
- Project and tenant scope match the owning obligation for all three settlements.
- All raw settlement currencies are NULL, but the obligation fallback resolves them to active `EGP`; there is no unresolved currency and no foreign-currency exclusion in this dataset.
- The request rail contains no live settlements (`ProjectPaymentRequestDetails` and request-settlement counts are `0`), so request-currency validation was verified in source/tests but not exercised by live data.
- No FX conversion was applied, and no cross-year or closed snapshot was rewritten.
- No financial rows were inserted, updated, or deleted by this task.

## 5. Build and test evidence

| Check | Outcome |
|---|---|
| API build | Passed; `0 Warning(s)`, `0 Error(s)` |
| Gate restore/build | Passed with `NU1900` warnings caused by unavailable NuGet vulnerability feed |
| Full `NEXUS.Api.Tests` build | Failed with `26 Warning(s)`, `22 Error(s)` (pre-existing current-tree test compile errors) |
| Focused Actual Paid/reporting tests | Passed `15/15` |
| Focused Payment Request policy tests | Passed `8/8` |
| Focused permission/security regression subset | `44/49` passed; `5` failures |
| Focused project reporting subset | `0/1` passed; `1` SQLite-provider failure |
| Temporary Task 06 precision/settlement harness | `11/17` passed; `6` failures |
| Dedicated SQL Server finance persistence tests | Not available; no new SQL Server write test was created |
| Agent UAT | Not run |

Full test-project compile errors include missing `PaymentRequestStatuses`, `ITenantFiscalYearService`, `FiscalYearCalendarService`, and `ProjectMember` references, plus stale method signatures. These are unrelated to the applied decimal migration and existed in the concurrent test tree.

Focused runs that passed:

- `-p:FocusedActualPaidReporting=true` with `ActualPaidReportingTests` and `ProjectFinancialSummaryPersistenceTests`: `Passed: 15, Failed: 0`.
- `-p:FocusedPaymentRequestPolicy=true` with `PaymentRequestWorkflowPolicyTests`: `Passed: 8, Failed: 0`.
- The temporary Task 06 harness compiled `FinancialDecimalPrecisionMappingTests` and `PaymentObligationLifecycleTests` only and was removed after execution; it passed all precision-mapping assertions and most settlement lifecycle tests.

Observed failures:

- `FinancialPermissionDelegationTests.Annual_budget_configuration_delegation_requires_both_role_and_tenant_scope_authority` — captured SQLite error `near "max": syntax error`.
- Four `UserPermissionAdministrationTests` failures:
  - `Role_category_does_not_restrict_permission_grants_or_retained_grants_on_category_change`
  - `Existing_Planning_Edit_holder_can_still_assign_a_system_role_with_the_same_permission`
  - `Planning_bootstrap_assigns_a_custom_role_to_another_same_tenant_user`
  - `Role_api_rejects_new_inactive_grants_and_removes_existing_legacy_grants_on_reconciliation`
- `ProjectReportingPersistenceTests.Persisted_multi_plan_configuration_uses_weighted_EVM_and_freezes_generated_report_data` — SQLite `near "max": syntax error`.
- Temporary-harness settlement failures:
  - `PaymentAndSettlementPersistAcrossDbContextRefresh`
  - `SendDueNotificationsAsync_CreatesOnlyOneDueNotificationPerObligation`
  - `SettlementCannotReferenceMissingPayment`
  - `RegisterSettlementAsync_ConcurrentSettlementsCannotOverdrawTheObligation`
  - `SettlementCannotReferenceParentInAnotherTenant`
  - `GetPagedAsync_OnlyReturnsTheCurrentTenantPayments`

None of these failing tests opens the Development SQL Server database or exercises the two widened closing columns. The only real SQL Server opt-in persistence tests in the test project are WBS tests and are unrelated to finance; they were not run.

## 6. Security, authorization, and scope boundaries

- No permission, role grant, policy, or authorization catalog entry was changed.
- No Tenant, Organization, Project, or resource-level boundary was changed.
- No maker-checker, current-Project-Manager settlement, approval, reversal, or audit-history behavior was changed at runtime.
- Prior finance documentation marked maker-checker self-approval prevention and current-PM settlement authorization as dependencies; current source contains checks in `ProjectPaymentRequestWorkflow.cs`, but a remediation document records runtime verification as pending and permission-matrix/UAT work as deferred. This remains an external Security Hardening dependency, not a blocker for the precision migration.
- No Agent UAT, Product Owner UAT, Minimal Seed, Demo Seed, or reseed was run.
- No Planned Payment aggregate, installment model, or workflow redesign was introduced.

## 7. Required database completion fields

| Required field | Evidence |
|---|---|
| EF model synchronized | Yes |
| Pending migrations | `0`; none |
| Target environment and database identity verified | Yes — Development, `Shady / NexusPmo`, database ID `5`, verified before and after apply |
| Backup status | Not requested (valid for Development/UAT; not a blocker) |
| Migrations applied | `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision`; history count `34/34` |
| Required seed mode and idempotency | No seed required or run for this task. Migration SQL idempotency is guarded by `IF NOT EXISTS`; seed idempotency was not re-verified because no seed was requested |
| Physical schema verified | Yes — both target columns `decimal(19,4) NOT NULL`; gate reports zero missing objects in either direction |
| Persistence tests | Focused tests executed: Actual Paid/reporting `15/15`, payment policy `8/8`; full test build blocked and non-SQL-Server harness failures documented in §5. No dedicated SQL Server finance persistence test exists |
| Blockers | No database blocker for this migration. Full test project compilation, several focused non-SQL-Server tests, and Agent/Product Owner UAT remain outstanding and are documented above |

## 8. Handover

The precision correction is fully applied and verified. Development `Shady / NexusPmo` (database ID `5`) now has `34/34` migrations applied, no pending migrations, an EF-synchronized model, and the two closing allocation columns widened to `decimal(19,4) NOT NULL`. The reviewed SQL hash bound the apply to the exact generated script. Read-only reconciliation found `50,208 EGP` across three correctly owned obligation settlements, with no double-counting, scope mismatch, missing owner, unresolved currency, or foreign-currency merge.

Remaining work is outside the database migration itself: fix or isolate the pre-existing full test-suite compile errors, add a genuine SQL Server-backed finance persistence test if stronger runtime evidence is required, address the name-based decimal convention fragility, complete the documented authorization/permission UAT, and obtain Product Owner UAT before any cutover decision. No Unified Planned Payment redesign was attempted.
