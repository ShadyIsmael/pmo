# CODE REVIEW REMEDIATION 04 — Financial Data Integrity (P0-04)

## Status
**P0-04 SOURCE REMEDIATION COMPLETE — DATABASE/RUNTIME VERIFICATION PENDING**

No original `docs/security/code-review/*P0-04*` finding document was found in the repository (searched docs and git history). Remediation was performed against confirmed defects discovered/validated in the current `C:\Projects\pmo` working tree. No database migrations were applied, no database reset/seed/modification performed, no Production changes made, no browser UAT or additional UAT scenarios executed. Tests were added but not executed (deferred to consolidated verification).

## Confirmed fixes / residual closure
| ID | Confirmed issue | Source remediation | Status |
|---|---|---|---|
| D1 | `ProjectPaymentRequestWorkflow.GetActiveApprovedAllocationAsync` counted only `PaymentRequestStatuses.Approved`; submit/final approval availability checks could ignore submitted/pending reservations, allowing over-submission/ceiling bypass risk. `ReservesBudget` policy only treated `Approved` as reserved. | Availability aggregation includes `PaymentRequestStatuses.Approved` **and** `PaymentRequestStatuses.Pending` (submitted reservation). Draft/terminal requests do not reserve. Funding transfers/reversals/settlement integrity checks are applied to approved rows only. `PaymentRequestWorkflowPolicy.ReservesBudget` reconciled to `Approved || Pending` with explicit comment (availability must prevent over-submission). Row locks/serializable tx flow, idempotency, fiscal boundaries, and approved-CR-only ceiling behavior preserved. | Remediated (source); consolidated runtime/consumer verification pending |
| D2 | `BudgetAuthority.ValidateAllocationAsync`/`ValidateCommitmentAsync` did not clearly enforce authoritative project lifetime ceiling semantics and could emit unsafe currency assumptions. | `BudgetAuthority` treats `Project.Budget` (exposed via project reference as authoritative ceiling) as the lifetime ceiling, rejects when missing, normalizes project currency, and avoids silent GBP substitution in validation messages. | Remediated (source); consolidated verification pending |
| B | Unsafe GBP/default currency fallbacks in calculation/reporting/persistence-adjacent paths could mix currencies or silently substitute defaults. | `ProjectFinancialLookup` repaired (was left syntactically broken by an incomplete subagent edit) and resolves authoritative project currency; missing authoritative currency returns explicit non-mixed output (`Currency = ""`, `HasFinancialData = false`). `GlobalFinancialSummaryService.NormalizeCurrency` no longer returns GBP and missing authoritative project currency yields empty/zeroed breakdown entries. `BudgetRevisionService.ToDto` no longer emits `GBP` fallback. | Partially remediated; residual contract/domain defaults documented |
| C | Portfolio/Program code generation used max-in-memory code patterns; Program lacked schema parity for safe uniqueness/optimistic concurrency. | `ProgramService` create uses serializable transaction + bounded retry (3 attempts) on `DbUpdateException`; `PortfolioService` keeps serializable transaction and adds bounded retry. `ProgramConfiguration` adds `(TenantId,Code)` unique filtered index, `RowVersion` as rowversion, and property length parity. | Partially remediated; migration/EF reconciliation blocker open |

## Files changed
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` — D1 reservation/availability fix.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentRequestWorkflowPolicy.cs` — D1 policy reconciliation (`ReservesBudget` includes `Pending`).
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetAuthority.cs` — D2 authoritative ceiling/missing-ceiling/currency handling.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectFinancialLookup.cs` — repaired and currency-safe.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/GlobalFinancialSummaryService.cs` — restored from HEAD then remediated (see WIP recovery).
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — minimal `ToDto` currency fallback fix (uncommitted WIP preserved).
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs` — serializable create + bounded retry.
- `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioService.cs` — bounded retry.
- `apps/api/src/NEXUS.Persistence/Configurations/ProgramConfiguration.cs` — `(TenantId,Code)` unique filtered index, rowversion, property parity.
- `apps/api/src/NEXUS.Modules.CostManagement/NEXUS.Modules.CostManagement.csproj` — `InternalsVisibleTo` for regression tests.
- `apps/api/tests/NEXUS.Api.Tests/Security/SecurityHeadersTests.cs` — fixed confirmed CS0535 (`IDirectoryContents.Exists`).
- `apps/api/tests/NEXUS.Api.Tests/CostManagement/P004FinancialDataIntegrityTests.cs` — P0-04 regression tests (not executed).
- `docs/security/code-review/CODE-REVIEW-REMEDIATION-04-FINANCIAL-DATA-INTEGRITY.md` — this report.

## Financial invariants preserved / reinforced
- `Project.Budget` remains the authoritative lifetime financial ceiling; ceiling changes remain approval-governed (approved Change Requests) outside this remediation.
- Annual budget allocations remain independent per fiscal year; carry-forward not treated as new lifetime funding.
- Draft requests do not authorize spending; submitted `Pending` requests reserve availability for over-submission prevention.
- Approved commitments/settlements are not double-counted with pending reservations in D1 adjustments.
- Remediated calculation paths do not silently substitute GBP/USD for missing authoritative currency.
- Tenant isolation and protected authorization boundaries preserved.

## WIP recovery (Subagent C investigation)
- `GlobalFinancialSummaryService.cs`, `IGlobalFinancialSummaryService.cs`, and `GlobalFinancialSummaryDtos.cs` were tracked in HEAD (`a698e861...`) and appeared deleted in the working tree at remediation start. No untracked replacements for the interface/DTOs were found; `git reflog`/`stash` showed no file-specific recovery point for local WIP beyond HEAD.
- `GlobalFinancialSummaryService.cs` was restored from HEAD and then minimally remediated for P0-04 (missing authoritative currency handling + `NormalizeCurrency` no longer returns GBP). `git diff HEAD -- .../GlobalFinancialSummaryService.cs` shows only those P0-04 changes.
- **Conclusion:** no recoverable local-history evidence of overwritten uncommitted WIP was found. Restoring the tracked HEAD version replaced the working-tree deletion; any pre-delete unsaved/untracked WIP not present in HEAD/stash/reflog is **unrecoverable** and cannot be precisely identified from available repository state.

## Program/Portfolio uniqueness and EF migration status
- `ProgramConfiguration` configures `Code` max length 50 (nullable), unique filtered index `IX_Programs_TenantId_Code` on `(TenantId, Code)` where `Code IS NOT NULL AND IsDeleted = 0`, and `RowVersion` as rowversion. `Program` domain keeps `Code` nullable and `Currency = "USD"` default (domain compatibility preserved).
- `ProgramService`/`PortfolioService` use serializable create transactions and bounded `DbUpdateException` retries; retries are only applied on create conflict exceptions (generic `DbUpdateException` catch; unique-index violation type not further narrowed in source-only pass). Code generation still uses max-scan, not `EntityCodeSequences`.
- **Migration status:** no Program uniqueness/rowversion migration found in `NEXUS.Persistence\Migrations`; latest migrations observed are `20261102190318_AddStableSystemRoleIdentity` and planning sync migrations. **No migration created/applied.** A source-only migration for Program unique index/rowversion may be safe structurally, but creating/applying migrations is constrained by dirty persistence state, EF tooling/target verification, and repo policy (no migration application). Exact blocker/dependency: reconcile `Program.Code` nullability/domain expectations, generate/review migration SQL for actual Development target, verify no conflicting concurrent EF model/migration changes, and obtain explicit authorization before any apply. `EntityCodeSequences` exists in `AppDbContext` model (`DbSet<EntityCodeSequence>`) but sequence adoption remains blocked until runtime/table/migration behavior is verified.

## Regression tests added (not executed)
- `apps/api/tests/NEXUS.Api.Tests/CostManagement/P004FinancialDataIntegrityTests.cs`
  - `PaymentRequestWorkflowPolicy_ReservesBudget_For_Pending_And_Approved_Only` (D1 policy reconciliation)
  - `BudgetAuthority_Allocation_Rejects_When_Project_Ceiling_Missing`
  - `BudgetAuthority_Allocation_Rejects_When_Exceeding_Authoritative_Ceiling`
  - `BudgetAuthority_Commitment_Rejects_When_Project_Ceiling_Missing`
  - `BudgetAuthority_Commitment_Uses_Normalized_Project_Currency_In_Error`

**Not added (runtime/EF integration required):** true concurrent payment submissions and duplicate Program code DB-uniqueness races require integration infrastructure/real EF behavior and were not added as misleading unit tests. Existing pattern reference: `apps/api/tests/NEXUS.Api.Tests/Integration/TeamCodeConcurrencyTests.cs` (deferred to consolidated verification environment).

## Targeted compilation evidence
Executed (Debug, `--no-restore`), no repeated full solution builds:
- `dotnet build apps/api/src/NEXUS.Modules.CostManagement/NEXUS.Modules.CostManagement.csproj -c Debug --no-restore --nologo -v q` → **Build succeeded, 0 errors**.
- `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj -c Debug --no-restore --nologo -v q` → **Build succeeded, 0 errors** (pre-existing IKVM/MppAdapter warnings and `ASPDEPR005` unchanged).
- `dotnet build apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj -c Debug --no-restore --nologo -v q` → **inconclusive in this session**. The confirmed `SecurityHeadersTests.cs` CS0535 blocker was fixed in source; a subsequent targeted build was started and a `dotnet` process (PID 64604, observed active) had not produced an updated `NEXUS.Api.Tests.dll` timestamp (`04/11/2026 03:10:14 PM`) at last check. Re-run this targeted build in a clean shell after the process exits to confirm compilation.

## Remaining risks / open items
- **Currency defaults still present (compatibility/concurrent-work risk):** `IProjectReferenceLookup.ProjectReference.Currency` default remains `"GBP"` (file has uncommitted `Code` diff; not changed). CostManagement domain entities and DTOs still contain static currency defaults (`Budget/CostEntry/Invoice/PurchaseCommitment/PaymentObligation = "GBP"`, `CostManagementDtos.cs` defaults, Portfolio/Program DTOs include GBP/USD defaults). These were not mass-changed to avoid breaking uncommitted WIP/API compatibility; remediated calculation paths resolve authoritative project currency and avoid silent substitution.
- **Schema not enforced in DB:** Program unique index/rowversion and any `EntityCodeSequences` adoption require reviewed migration(s) and authorized apply.
- **Behavioral verification pending:** D1 `ReservesBudget` semantic expansion (now includes `Pending`) needs consolidated verification across reports/UI/policy consumers. Concurrent payment submissions, currency-mismatch runtime paths, and duplicate Program/Portfolio code races require execution/integration verification.
- **Uncommitted WIP preserved:** `BudgetRevisionService.cs`, `CostManagementDtos.cs`, `PortfolioDtos.cs`, `ProgramDtos.cs`, `IProjectReferenceLookup.cs`, payment workflow DTO/workflow files retain concurrent changes; only targeted P0-04 fixes applied.

## Skills applied
- Read and followed `AGENTS.md` (EF workflow caution, no Production/DB reset/migration apply, preserve concurrent work, frontend-data-integrity scope note).
- Reviewed `CODE-REVIEW-REMEDIATION-01/02/03`, `MASTER-UAT-CHECKLIST.md`, `FINANCIAL-FULL-CYCLE-UAT.md`.
- Reviewed `.kilo/skills/frontend-data-integrity/SKILL.md` (not applicable: backend/API remediation only).
- No additional project/global EF/security/testing skill files found under `.kilo`; applied repository EF/security/testing patterns (serializable tx/retries, authoritative source/currency handling, targeted compile only, no test execution).

## Actual subagent contributions
- **Main/integration:** D1/D2/B source fixes, D1 policy reconciliation, `ProjectFinancialLookup` repair, WIP investigation/reporting, CS0535 fix, regression test expansion, targeted builds, remediation report, preservation of concurrent/uncommitted work.
- **Subagent A (analysis):** D1/D2/financial workflow and ceiling validation findings.
- **Subagent B (analysis + partial edit):** currency fallback findings; incomplete edit broke `ProjectFinancialLookup` (repaired by main).
- **Subagent C (completed):** Program/Portfolio serializable create + bounded retry; `ProgramConfiguration` unique index/rowversion/property parity; migration blocker documented.
- **Subagent D (analysis):** regression/build/doc recommendations.
- **Residual closure pass:** completed via main-agent sequential integration after initial parallel analysis; no new parallel runtime agents were spawned in this pass.

