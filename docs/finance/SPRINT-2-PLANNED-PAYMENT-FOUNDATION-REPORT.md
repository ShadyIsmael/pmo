# Sprint 2 Task 01 — Unified Planned Payment Foundation Report

Date: 2026-11-22  
Scope: Sprint 2 / Task 01 only. Task 02 was not started.  
Status: Implementation complete; runtime/Product Owner Browser UAT remains pending.

## 1. Outcome

Task 01 delivered the production-ready foundation for the unified Planned Payment rail:

- Registration reserves the full plan amount immediately against the project's **current approved lifetime project budget** (`Project.Budget` via `IProjectReferenceLookup.ProjectReference.CurrentApprovedProjectBudget`).
- The Annual Budget remains independent; it is neither consumed nor re-summed by this feature.
- PR approval remains authorization only. Actual Paid increases only from confirmed `PaymentSettlement` rows, and a settlement linked to a Planned Payment reduces the outstanding reservation instead of consuming lifetime budget a second time.
- One Planned Payment contains multiple installments. Each installment has an amount, UTC due date, optional same-project active milestone, description, request/settlement totals, and remaining requestable amount. The installment sum equals the plan amount.
- Financial terms become immutable after the first Payment Request. The lock is applied inside the existing request transaction; replay is idempotent; historical request fingerprints are unchanged.
- Current Project Managers create/amend; assigned approvers decide through the existing request workflow; self-approval remains blocked by that workflow. No new System Role, separate Finance Operations role, or `PlannedPayment.*` permission key was introduced.
- Minimum usable Project Details → Financial Tab registration/viewing exists in `apps/web-react` with authoritative API data, EN/AR localization, and Loading/Empty/Error/Unavailable states.

No reset, migration-history rewrite, backfill, legacy deletion, Production operation, or Agent UAT was performed. All unrelated uncommitted/concurrent work was preserved; the baseline HEAD remained `57f6368ea88d6bdb29658e51964214f98ccd0211`.

## 2. Authoritative decisions applied

| Decision | Implementation |
|---|---|
| Latest approved Product Owner financial decisions supersede older conflicting proposals | Lifetime ceiling is the authoritative current approved project budget; no PCR re-sum and no Annual Budget coupling. |
| Deleted legacy migrations must not be restored | No legacy Planned Payment migration was reused. A fresh additive migration was generated. |
| First-request lock must be transactional | `ProjectPaymentRequestWorkflow.CreateAsync` calls the lock inside its existing serializable transaction. Request detail stores the nullable installment link; plan state, timestamp, and audit commit atomically. |
| Historical request fingerprints must remain stable | The existing four-field Payment Request fingerprint logic was not changed. Lock metadata is carried on the plan/detail rows. |
| Canonical permissions only | Reused `PaymentObligation.View`, `PaymentObligation.Create`, and `PaymentObligation.Edit` with Active Deny, Tenant/Organization/Project/resource scopes, and no automatic sensitive grants. |
| Full Budget Line enforcement is outside Task 01 | Not implemented; no Budget Line key, table, or behavior was added. |

## 3. Backend implementation

Task-owned backend files include:

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/PlannedPaymentService.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IPlannedPaymentService.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICurrentProjectManagerLookup.cs`
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs`
- `apps/api/src/NEXUS.Persistence/Configurations/PlannedPayment*Configuration.cs`
- `apps/api/src/NEXUS.Persistence/Migrations/20261122003656_Sprint2AddPlannedPaymentFoundation.cs` and designer

Key controls:

- Serializable transaction plus `UPDLOCK, HOLDLOCK` project-row lock for registration/amendment.
- Project-scoped idempotency key with canonical SHA-256 request fingerprint; replay returns the original; key reuse with different content returns HTTP 409.
- Active-actor and current-Project-Manager checks independent of broad administrative permissions.
- Tenant, Organization (resolved project→organization mapping), Project, and resource-level checks.
- Server validation for positive four-decimal amounts, required UTC due dates, same-project active milestones, configured three-letter currency matching the project, installment reconciliation, row version, and lock state.
- Reservation math: `Available = max(0, L − C − R)`, where `L` is the authoritative lifetime ceiling, `C` is confirmed settlement fact total from `ActualPaidSettlementCalculator`, and `R` is active reservation net of settlement-linked plan consumption.
- Audit entries are written in the same unit of work for `Registered`, `Amended`, and `Locked`.

Two defects were found by the new focused tests and fixed before completion:

1. Newly added installments during a pre-lock amendment were removed by the stale-installment cleanup because their IDs were not marked retained.
2. Newly added installments with client-generated GUID keys were tracked as modified rather than inserted; they are now explicitly added to the `DbSet`.

## 4. Frontend implementation

Task-owned frontend files include:

- `apps/web-react/src/features/projects/api/planned-payments.ts`
- `apps/web-react/src/features/projects/components/PlannedPaymentSection.tsx`
- `apps/web-react/src/features/projects/ProjectFinanceTab.tsx`
- `apps/web-react/src/i18n/messages/en.json`
- `apps/web-react/src/i18n/messages/ar.json`
- `apps/web-react/src/test/planned-payment-section.test.tsx`

Behavior:

- Dedicated authoritative hooks are kept in `planned-payments.ts`; the legacy `PlannedPaymentsPage.tsx` single-amount model is not reused.
- The Financial Tab section renders budget summary, plan list/detail sheet, installments, and registration form from API data only.
- `PaymentObligation.View` gates viewing; `PaymentObligation.Create` gates registration.
- No Planned Payment KPI was added; the existing KPI-absence regression still passes.
- Loading, Empty, Error, and Unavailable states are explicit; no hardcoded business values, fake statuses, or unapproved currency fallbacks are present.
- EN/AR keys and RTL-safe labels were added.

## 5. Database gate evidence

The repository-documented gate was used with the real Development target. The effective config endpoint (`tcp:localhost,1433/NexusPmo`) was not treated as identity; the gate verified live `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID.

### Pre-change read-only gate

- Target: `Shady / NexusPmo`, database ID `5`
- Environment: Development
- Repository migrations: 34
- Applied migrations: 34
- Pending migrations: 0
- EF model synchronized: Yes
- Physical schema verified: Yes
- Writes: none

### Migration generation and review

- Generated migration: `20261122003656_Sprint2AddPlannedPaymentFoundation`
- Full rebuild reported repository migrations: 35 and model synchronized: Yes.
- Generated SQL: 221 lines in `.opencode/tmp/sprint2-planned-payment-review.sql`
- Independent SHA-256: `E8804B06FBADA3085D4714085FE148A7284B462DD3E8B79FC21F999E25AF6A49`
- The independently computed hash matched the gate's reviewed hash.
- Every statement was reviewed. The script is additive only: one nullable `ProjectPaymentRequestDetails.PlannedPaymentInstallmentId` column, three new Planned Payment tables, money columns as `decimal(19,4)`, check constraints, tenant/project-scoped indexes, unique idempotency/reference indexes, and foreign keys.
- No drop, delete, truncate, recreate, type change, data backfill, or destructive operation was present.

### Apply and post-apply gate

Command shape used:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:NEXUS_EF_DEVELOPMENT_CONNECTION_STRING = <Development connection from appsettings.Development.json>
.\scripts\database\ef-database-gate.ps1 apply `
  --environment Development `
  --expected-server Shady `
  --expected-database NexusPmo `
  --script-file .opencode\tmp\sprint2-planned-payment-review.sql `
  --approved-sha256 E8804B06FBADA3085D4714085FE148A7284B462DD3E8B79FC21F999E25AF6A49
```

Evidence:

- Target Development DB verified: Yes (`server=Shady; database=NexusPmo; databaseId=5`)
- Backup: Not requested. Development apply does not require backup storage or `RESTORE VERIFYONLY`.
- Pending migrations before apply: 1
- Migration applied: `20261122003656_Sprint2AddPlannedPaymentFoundation`
- Migrations applied: 1
- Repository migrations after apply: 35
- Applied migrations after apply: 35
- Pending migrations after apply: 0
- Applied migration IDs include all prior 34 plus the new Sprint 2 migration.
- Post-apply EF model synchronized: Yes
- Post-apply physical schema verified: Yes
- Objects missing from last applied model: 0
- Objects missing from current repository model: 0
- Post-apply baseline schema drift remaining: 0

## 6. Tests and build evidence

### Focused backend tests

Command:

```powershell
dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj `
  -p:FocusedPlannedPayments=true --no-restore -m:1
```

Result: **16/16 tests passed**, 0 failures.

Coverage includes:

- Registration and immediate lifetime reservation with accurate balance
- Insufficient lifetime budget and full transaction rollback
- Idempotent replay and key reuse conflict
- Concurrent duplicate registration producing one plan/reservation/audit
- Installment reconciliation, milestone resolution, and no double consumption
- Invalid amounts, missing/non-UTC dates, unconfigured/changed currency
- Missing permission, authoritative Active Deny, non-manager actor
- Tenant, Organization, Project, and foreign-resource isolation
- Pre-lock amendment, added/retained installments, reservation update, and stale token rejection
- First-request lock, replay, post-lock amendment rejection, no duplicate reservation
- Installment overcommitment rejection
- Audit entries and immutable history

Focused tests use an isolated SQLite financial model; no Development business data was written.

### API build

```powershell
dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1
```

Result: **Build succeeded, 0 errors, 1 warning**. The warning is an unrelated pre-existing nullable dereference in `NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs:450`; that file was not modified by this task.

### Frontend

```powershell
npm run typecheck
npx vitest run --pool=forks --maxWorkers=1 `
  src/test/planned-payment-section.test.tsx `
  src/test/project-finance-workspace.test.tsx `
  src/test/planned-payments-project-id.test.tsx
```

Results:

- Typecheck passed.
- **3 test files, 23 tests passed**.
- Existing KPI-absence and legacy project-ID regressions passed.

### Full backend test suite

The unfiltered `NEXUS.Api.Tests` build remains blocked by pre-existing Sprint 1 test-source compilation errors in files not modified by this task. Representative errors include missing `PaymentRequestStatuses`, missing `ITenantFiscalYearService`/`FiscalYearCalendarService`, missing `AddCanonicalFinancialActionPolicies`, ambiguous `IAuthorizationService`, and stale constructor signatures. The focused `FocusedPlannedPayments` filter was added specifically to isolate Task 01 verification from those unrelated errors.

## 7. Permission and UAT documentation

- `docs/architecture/permission-model.md` contains the Sprint 2 Planned Payment permission matrix and the rationale for reusing `PaymentObligation.View/Create/Edit`.
- `docs/quality/MASTER-UAT-CHECKLIST.md` contains the Task 01 matrix and scenarios `PPF-01` through `PPF-11`, covering authorization, Active Deny, no self-approval, cross-scope negatives, financial integrity, lock behavior, and localized Financial Tab behavior.
- Runtime permission testing and Product Owner Browser UAT are deferred to consolidated final UAT, as allowed by repository policy. No Agent UAT was run.

## 8. Completion checklist

| Required field | Result |
|---|---|
| Model synchronized | Yes; post-apply gate confirmed |
| Pending migration count and names | 0 pending. Applied: `20261122003656_Sprint2AddPlannedPaymentFoundation` |
| Target environment and database identity verified | Yes; Development, live `Shady / NexusPmo`, database ID 5 |
| Backup status | Not requested; valid for Development |
| Migrations applied | 1 reviewed additive migration |
| Required seed mode and idempotency verified | No seed required or invoked; schema-only additive migration with EF history guards. Application-level registration replay idempotency verified by focused tests. No demo/business seed was used. |
| Physical schema verified | Yes; 0 missing objects, 0 baseline drift |
| Persistence tests | Focused backend 16/16 passed against isolated SQLite; full unfiltered suite blocked by unrelated pre-existing compile errors |
| Blockers | None for Task 01 implementation. Runtime browser UAT and the pre-existing full-suite compile failures remain tracked separately. |

## 9. Deferred / out of scope

- Task 02 was not started.
- Controlled post-lock replacement workflow is a future change; post-lock financial terms are currently immutable.
- Full Budget Line enforcement remains outside Task 01.
- Runtime/Product Owner Browser UAT and Agent UAT were not performed.
- Pre-existing Sprint 1 full-test-suite compilation errors were not modified or repaired here.
