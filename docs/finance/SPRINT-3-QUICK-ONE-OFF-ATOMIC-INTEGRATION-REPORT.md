# Sprint 3 / Task 04 — Quick One-off Payment Requests & Atomic Transaction Integration

**Date:** 2026-11-22  
**Implementation status:** Implemented; backend runtime verification is blocked by testhost startup.  
**Scope:** Quick One-off Payment Request flow inside Project Details → Financial Tab → Payment Requests.

## 1. Implementation Summary

Added a coordinated Quick One-off operation that composes the existing Planned Payment registration, Payment Request draft creation, and optional Payment Request submission operations under one serializable EF Core transaction. The coordinator creates one Planned Payment with one installment, the existing one-time Lifetime reservation, one linked Payment Request, and—when requested—the existing approval/funding submission records before it commits.

The existing standalone Planned Payment registration, Payment Request creation, and Payment Request submission operations retain their own serializable transactions when called outside the coordinator. The Financial Tab now offers the Quick One-off side sheet alongside the existing request-from-installment path. No financial operating model or approval engine was redesigned.

The API build, frontend typecheck, frontend production build, and focused frontend tests passed. The focused backend test assembly compiled, but VSTest could not connect to its testhost within the configured 90-second window. No backend test cases ran; the retry was skipped as instructed. No live database or SQL Server persistence tests were run.

## 2. Shared Transaction Architecture

- `ProjectPaymentRequestWorkflow.CreateQuickOneOffAsync` is the sole transaction owner for the composite operation. It begins one `Serializable` transaction, invokes existing services, and commits only after every requested step succeeds.
- `PlannedPaymentService.RegisterAsync`, `ProjectPaymentRequestWorkflow.CreateAsync`, and `SubmitAsync` join `Database.CurrentTransaction` when present. When called independently, each keeps its prior serializable transaction behavior.
- Existing project-row update locks and Annual Budget / Budget Line locks are reused. The code uses the same scoped `AppDbContext`, so writes, audit entries, approval records, and transactional notification outbox changes participate in the same transaction.
- Constituent failures return before the coordinator commits; disposal rolls back the transaction. Concurrency and persistence exceptions are mapped to a conflict response.
- The API endpoint invokes the coordinator directly; it does not call two independently committing endpoints.

## 3. Atomic Registration Sequence

For both Save Draft and Submit for Approval, the backend sequence is:

1. Validate writable service scope, create permission, required idempotency key, amount precision, and UTC due date.
2. Begin the coordinator-owned serializable transaction.
3. Register one Planned Payment with exactly one installment. The existing registration operation validates the Current Project Manager, Tenant/Organization/Project scope, canonical `PaymentObligation.Create` permission and Active Deny, configured Project Currency, and Lifetime Budget availability.
4. Let registration create the single Lifetime reservation and its Plan, Installment, and Reservation audit records.
5. Create the linked Payment Request through the existing request workflow. It validates the request’s installment, Annual Budget, exactly one Budget Line, currency, scope, Current Project Manager, and `ProjectPaymentRequest.Create` permission.
6. For Submit for Approval, invoke the existing `SubmitAsync` approval and funding workflow inside the same transaction. It checks `ProjectPaymentRequest.Submit`, approval configuration, approver eligibility, self-approval, Annual Budget capacity, and Budget Line capacity.
7. Commit once and return the Planned Payment, its installment, and the linked Payment Request.

Any validation or persistence failure before step 7 leaves no committed Plan, Installment, Request, reservation, submission, audit, or outbox record from this operation.

## 4. Lifetime & Annual Budget Reservation Verification

- Lifetime reservation remains immediate and occurs once in Planned Payment registration. Request creation and submission do not create another Lifetime reservation.
- Save Draft follows the existing lifecycle: it validates and stores the selected Annual Budget and one Budget Line on the request but does not reserve Annual Budget or Line funding.
- Submit for Approval uses the existing annual funding validation and reservation path. The Quick One-off coordinator does not implement a second funding engine.
- Each Quick One-off registration sends one installment equal to the requested amount and rejects any registration response that does not contain exactly one installment.
- These rules were verified by targeted source inspection and a passing API build. Runtime confirmation against SQL Server was not available in this run.

## 5. Idempotency & Concurrency

- A non-empty client idempotency key is required and passed through registration, request creation, and optional submission. Existing component idempotency records and content fingerprints bind retries to their respective Plan, Request, and approval content.
- Reusing a key with changed Plan, Request funding, or approval content is rejected by the existing fingerprint checks. A retry after a committed operation reuses those component records rather than creating a second Plan or reservation.
- The composite operation uses serializable isolation and existing Project and funding-row locks, retaining the current budget concurrency controls.
- A concurrent database uniqueness/concurrency failure returns a conflict. The caller can retry the same key after the winning transaction commits to retrieve the existing result.
- Idempotent replay, key-content mismatch, and SQL Server concurrent-capacity behavior were not executed in this run because the backend testhost did not start and no isolated SQL Server test database was available.

## 6. Authorization & Audit

- No new permission key or Finance Role was introduced. The existing canonical `PaymentObligation.Create`, `ProjectPaymentRequest.Create`, and, when submitting, `ProjectPaymentRequest.Submit` permissions are required. Existing service checks apply Active Deny and writable management scope.
- Backend constituent operations revalidate the active actor and Current Project Manager eligibility. Request submission reuses existing approval candidate checks and requester self-approval rejection.
- The frontend exposes Quick One-off only when the actor has both create permissions and the Financial Foundation identifies the actor as the Current Project Manager. Submit controls are separately gated by the existing Submit permission.
- Plan, Installment, Lifetime Reservation, Request, submission/approval, and notification records are written using the existing audit/outbox infrastructure and shared transaction.
- Permission Matrix documentation now records the composite permission requirements. Persisted effective grants and runtime authorization denials were not tested in this task.

## 7. Backend Changes

- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs`: added the coordinator and made existing request creation/submission transactions join an ambient transaction while preserving standalone ownership.
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs`: added `POST /api/projects/{projectId}/payment-requests/quick-one-off`, protected by the existing request-create policy and routed through the coordinator.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/PlannedPaymentService.cs`: registration now joins an existing transaction and records Installment and Lifetime Reservation audit entries in addition to its existing Plan audit.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs`: added Quick One-off input and composite-result DTOs.
- `apps/api/tests/NEXUS.Api.Tests/Integration/PlannedPaymentFoundationTests.cs`: added coverage for registration joining a caller-owned transaction, idempotent registration behavior, component audit creation, and rollback. The test assembly compiled, but test execution did not start; this is not reported as a passing test.

## 8. Frontend Changes

- Added Quick One-off creation to the existing Project Details → Financial Tab → Payment Requests workspace. The existing request-from-Planned-Payment-installment path remains available.
- The guided side sheet captures description, amount, purpose, due date, optional milestone, Annual Budget, exactly one Budget Line, and—when submitting—approval mode and approvers. It offers Save Draft and Submit for Approval actions.
- Lifetime availability, currency, Annual Budget/Line values, and reservation preview are loaded from existing financial APIs; the UI does not invent budget or currency values.
- Existing permission and Current Project Manager gating is applied. EN/AR labels and RTL direction are included.
- Focused React coverage verifies draft and submit request payloads, authoritative Lifetime Budget display, Arabic action rendering/RTL, and permission gating.

## 9. Migration / Database Status

No entity, relationship, `AppDbContext`, persistence mapping, schema, or migration change was identified as necessary for Task 04. No migration was generated or applied. No database reset, seed, or financial-data mutation was performed.

| Required evidence | Task 04 result |
|---|---|
| EF model synchronized with database | Not rechecked. No persistence model/schema change was made by Task 04; live synchronization was not claimed. |
| Pending migration count and names | Not checked in this run. The latest reported baseline was zero pending migrations, but that historical result was not reverified. |
| Target environment and database identity | Not verified in this run; no database connection or write was made. |
| Backup status | Not requested; no Development/UAT database write was performed. |
| Migrations applied | None. |
| Required seed mode and idempotency | Not run or reverified. Historical baseline reported Minimal System Seed success/idempotency, 8 roles, 190 permissions, and 0 Projects; this is not live evidence for this task. |
| Physical schema verified | Not run. |
| Persistence tests | No SQL Server persistence tests run. The focused API test assembly compiled, then VSTest failed before test cases could execute. |

## 10. Build & Test Results

| Check | Result |
|---|---|
| Cost Management module build | **Passed** — `dotnet build apps/api/src/NEXUS.Modules.CostManagement/NEXUS.Modules.CostManagement.csproj --no-restore -v:minimal -p:MSBuildEnableWorkloadResolver=false -p:BuildProjectReferences=false -m:1`; 0 warnings, 0 errors. |
| API build | **Passed** against the updated Cost Management module — `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -v:minimal -p:MSBuildEnableWorkloadResolver=false -p:BuildProjectReferences=false -m:1`; 0 errors, 352 IKVM/Java compatibility and unavailable-class warnings; elapsed 9m 38.73s. |
| Frontend typecheck | **Passed** — `npm run typecheck` in `apps/web-react`. |
| Frontend production build | **Passed** — `npm run build` in `apps/web-react`; Vite transformed 3,006 modules and completed in 2m 13s. Bundler emitted dependency annotation and large-chunk warnings; output completed successfully. |
| Focused frontend tests | **Passed: 11/11** — `npm run test -- src/test/quick-one-off-payment-request.test.tsx src/test/planned-payment-section.test.tsx`. |
| Focused backend tests | **Blocked before execution** — `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore -v:minimal -p:MSBuildEnableWorkloadResolver=false -p:FocusedPlannedPayments=true -p:BuildProjectReferences=false -m:1`. The focused test assembly compiled, but VSTest could not connect to testhost after 90 seconds and aborted. No cases passed or failed because none ran. Retry skipped per instruction. |
| SQL Server persistence/concurrency tests | **Not run** — no isolated database was available; the shared Development database was not used for test mutations. |
| Agent UAT | **Not run**, as required. |

## 11. Remaining Blockers

1. Backend focused behavior tests need a functioning testhost/test environment. The current evidence proves compilation, not that the coordinator’s composite transaction, rollback, replay, funding, approval, isolation, or audit cases pass at runtime.
2. SQL Server-specific locking, concurrency, and persistence behavior has not been exercised against an isolated database.
3. Product Owner browser UAT remains pending for the Quick One-off workflow, including sequential/parallel approval, failure cases, and EN/AR rendering.
4. Live migration history, database identity, seed status, and physical schema were not checked because Task 04 made no database/schema change and no database write was needed.

## 12. Sprint Closure Readiness

Task 04 implementation is present across the API, existing financial services, Financial Tab UI, Permission Matrix, and Master UAT Checklist. Build and focused frontend verification passed. The backend test runner and isolated SQL Server verification remain blockers, so this report does not assert full runtime financial-integrity verification or Sprint Closure readiness.

No Sprint Closure was performed, and no subsequent task was started.
