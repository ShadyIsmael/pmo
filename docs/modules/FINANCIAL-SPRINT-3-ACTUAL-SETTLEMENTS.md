# Financial Sprint 3 — Actual Settlements

**Date:** 2026-10-30  
**Status:** Source implemented / compile verification blocked / migration generation blocked / database deployment pending / runtime UAT deferred to Sprint 7.

## Scope delivered in source

Approved Project Payment Requests now have GET and POST settlement endpoints under the Project Financial API. The Financial Tab shows the approved amount, settled total, outstanding amount, derived settlement status, the Annual Budget reference/Fiscal Year, chronological payment history, and a permission-gated settlement form. The form captures payment date, amount, reference, and description; it sends no client-selected currency.

The implementation reuses `PaymentSettlements`. It does not create a second ledger. The same table continues to record `PaymentObligation` settlements and now has a separate optional Payment Request owner. The model requires exactly one owner, carries Tenant and Project identity, and uses composite Tenant/Project relationships. Legacy planned-payment records remain owned by their existing PaymentObligations.

Settlement totals are derived from valid, non-deleted settlement rows:

`Outstanding = Approved Payment Request Amount - Sum(Actual Settlements)`

`Not Settled` means total paid is zero; `Partially Settled` means the total is between zero and the approved amount; `Fully Settled` means outstanding is zero. Settlement status does not change the Payment Request's Approved status. Only Approved requests accept settlements. Currency is copied from the approved request, and the reporting Fiscal Year is derived from the Tenant fiscal calendar using the actual payment date.

Final settlement writes run in a SQL Server `Serializable` transaction and take the established tenant/project Project-row `UPDLOCK, HOLDLOCK` before rereading the request and settlement total. A tenant-scoped idempotency unique index, request-scoped unique reference index, and composite owner FKs provide additional retry and scope protections. No SQL Server runtime concurrency test was performed.

Annual Budget reservation is unchanged. A settlement pays against the existing approved obligation; it does not reserve or release the allocation, deduct budget again, create a CostEntry, or reassign the original request's Fiscal Year. No edit/delete route is exposed for posted settlements. Financial Sprint 4 adds only approved release of unpaid commitment. It does not reverse/correct a posted settlement or issue a refund; those behaviors remain excluded.

## Financial Audit

Successful Payment Request settlements add shared `AuditEntries` in the same `SaveChanges` transaction as the settlement row:

- `ActualSettlementRecorded` includes request and settlement references, Approved Amount, settlement amount/currency/date, Tenant/Project, reporting FY, Annual Budget reference/FY, actor and UTC timestamp, totals/outstanding, and before/after settlement status.
- `SettlementStatusChanged` is added when the derived status changes.

The Project Financial Activity action map has business-readable labels for both events. Idempotent replay returns the existing settlement and adds no new event.

## Permission matrix

| Action | Required authorization | Scope |
|---|---|---|
| View Payment Request settlement history | `finances.read` (or the existing read-capable finance access policy) | Authenticated Tenant and Project scope |
| Record settlement | `finances.write` **and** `PaymentSettlement.Record`; active actor; non-read-only context | Authenticated Tenant and Project scope |
| Access attachments | No new document route or permission introduced; attachments are optional and not implemented in this Sprint | Existing document policy remains unchanged |
| Correct, reverse, or refund a posted settlement | Not implemented; Sprint 4 authorizes unpaid commitment reversal only | Excluded from Sprint 4; separate policy approval required |

`PaymentSettlement.Record` is registered in the authoritative catalog and has EN/AR resource/action labels for Roles & Permissions. It is excluded from default Admin grants; no existing role grants were changed. The existing role-management flow only offers permissions held by the acting delegator. Therefore the initial authorized delegator/grant policy is unresolved, and settlement writes fail closed until Product Owner-authorized grants exist. The role UI itself is catalog-driven, so no duplicate static permission list was added.

## API contracts

- `GET /api/projects/{projectId}/payment-requests/{requestId}/settlements?page=1&pageSize=100` returns a derived summary and paged settlement history.
- `POST /api/projects/{projectId}/payment-requests/{requestId}/settlements` accepts `idempotencyKey`, `amount`, `paymentDate` (`YYYY-MM-DD`), `paymentReference`, and `description`. Tenant, Project, request currency, Annual Budget identity, reporting Fiscal Year, actor, and timestamps are server-derived.
- The legacy `POST /api/payment-obligations/{id}/settlements` remains available but now requires both `finances.write` and `PaymentSettlement.Record` at the API and service layers.

## Persistence/migration dependency

### Historical source-schema verification

The apparent currency contradiction in the Sprint 3B notes is resolved against the authoritative migration and last pre-Sprint-3 Designer. The historical `PaymentSettlements` columns are: `Id`, required `PaymentObligationId`, `IdempotencyKey`, `Amount`, `SettlementDate`, `PaymentReference`, `Notes`, `TenantId`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`, and `RowVersion`. Neither the baseline migration nor the 2026-10-30 pre-Sprint-3 Designer defines `ProjectId`, `Currency`, `Description`, or a Payment Request owner. The historical composite parent relationship is `(TenantId, PaymentObligationId)` to `PaymentObligations` `(TenantId, Id)`.

The historical `PaymentSettlement` EF shape likewise has a required `PaymentObligationId` and no settlement currency property. The current un-migrated source entity adds nullable `Currency`, nullable owner IDs for the two-owner design, `ProjectId`, and `Description`. That current class describes the target model, not the old physical table.

`PaymentObligation.Currency` is a required property on the parent obligation and is set from the Project currency on create/update. Before the Sprint 3 source guard, obligation updates could change that value after a settlement. It was not independently captured on the old settlement row. Therefore no stored, verifiable historical settlement currency source exists; historical settlement currency must remain `NULL` and must not be inferred from today's obligation currency. The service now rejects future obligation-currency changes after a settlement exists, while the UI clearly labels historical unknown currency.

The legacy reports aggregate only settlement rows whose `PaymentObligationId` matches the obligation set. Project Payment Request settlement totals are queried by their separate request owner; source review found no double aggregation between those two paths. Financial Activity events carry the ProjectId directly and use the existing shared `AuditEntries` store.

The focused migration is **not generated** and the snapshot is **not synchronized**. The safe migration design is:

- Add `ProjectId` nullable initially, `Currency` nullable, `Description` nullable, and `ProjectPaymentRequestId` nullable; make `PaymentObligationId` nullable.
- Backfill only `ProjectId` by joining each legacy settlement to its existing Payment Obligation through the current required `(TenantId, PaymentObligationId)` relationship. Before making `ProjectId` required, the migration must explicitly abort if any row has no matching obligation or remains without a project. Never fill unresolved ownership with a default or guessed project.
- **Do not backfill legacy Currency.** The baseline settlement table did not store it, and `PaymentObligation.Currency` is not a proven historical source: obligation currency could be changed after a settlement, so its current value does not establish the currency used for that old payment. Keep legacy settlement currency `NULL` and present it as “currency not recorded.” New Payment Request settlements must have a valid three-letter currency and nonblank description, enforced by a conditional database check. New planned-payment settlements store their obligation currency at recording time. The service now rejects changing an obligation's currency after a settlement exists.
- Preserve all existing settlement IDs, amounts, payment dates, references, notes, tenant IDs, audit metadata, and Payment Obligation ownership. Do not convert legacy rows into Payment Request settlements.
- Replace the old two-column obligation FK with a Tenant/Project/Obligation composite FK after the guarded ProjectId backfill; add the corresponding scoped Payment Request FK, required principal keys, exactly-one-owner constraint, request-specific currency/description constraint, lookup indexes, and filtered request-reference uniqueness.
- Update migration Designer and `AppDbContextModelSnapshot` from the actual current model.
- Generate and inspect additive SQL with the EF Database Gate, including its `ProjectId` backfill guard and preservation of null legacy currency. No database deployment is authorized here.

The source migration chain confirms the repository's historical model, but it does not prove the physical Development database matches that chain. The five current source migrations are `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, `20261029183557_AddTenantRegistry`, `20261030122718_AddProjectAnnualBudgetFoundation`, and `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`. The actual Development identity, applied history, physical schema, and independent backup remain unverified.

The history/source migrations and physical Development schema were not inspected in this Sprint for deployment readiness. The established documented database workflow must verify exact target identity, actual applied history, and physical schema before deployment. A backup may be created when explicitly requested, but independent backup storage and `RESTORE VERIFYONLY` are not Development/UAT prerequisites. Existing financial history must not be assigned to a Payment Request or Fiscal Year during this migration.

| Database gate field | Current evidence |
|---|---|
| EF model synchronized | **No** - the Sprint 3 model changes are not in the snapshot. |
| Sprint 3 source migration | **Not generated** — one focused migration is required; exact ID is unavailable until scaffolded. |
| Development DB identity verified | **No** for this Sprint 3 task. |
| Independent backup verified | **No** for this Sprint 3 task. |
| Migration applied | **No**. |
| Physical schema verified | **No**. |
| Persistence tests | **Not run** — deferred to Sprint 7 and require an isolated authorized SQL Server target. |

## Verification record

| Check | Result | Evidence / limitation |
|---|---|---|
| SDK selection | **PASS** | `Get-Content global.json` selected SDK `10.0.302`; `dotnet --version` returned `10.0.302`. This is not build verification and gives no evidence of SDK corruption. |
| SDK environment details | **BLOCKED** | `dotnet --info` failed while the SDK attempted to enumerate Windows services: `Cannot open Service Control Manager ... Access is denied` (Win32 error 5) through `WindowsUpdateAgent`. This establishes a permission limitation for that diagnostic command, not a C# build failure. |
| Telemetry investigation | **BLOCKED** | The prior automatic security review reported an attempted Visual Studio Telemetry connection and prohibited retrying by another route. It did not expose destination, process identity, or payload. The read-only `Get-Process -Name dotnet,MSBuild,devenv` and associated established-connection snapshot found no active process or connection at inspection time. It cannot reconstruct the earlier attempt or establish whether it completed; there is no evidence here proving data transfer or exfiltration. |
| Build/output-lock diagnosis | **NOT TESTED** | No build was run after the security denial; no file-lock inspection was available. The repository previously had two nullable `PaymentObligationId` compile errors; source queries were corrected, but the post-fix result remains unknown. |
| Settlement source review | **PASS, STATIC ONLY** | Both owner paths, exact-one-owner check, Tenant/Project composite relationships, request currency constraint, request/obligation scoped queries, idempotency, report aggregation, audit scope, and unchanged Annual Budget reservation behavior were reviewed. This is not database/runtime verification. |
| Locale and permission source review | **PASS, STATIC ONLY** | `PaymentSettlement.Record` is registered in seed/catalog source, requires `finances.write` at the API policy, is enforced in services, and gates both relevant frontend actions. EN/AR label resources are present. No automatic sensitive role grant was added; initial delegation authority remains an open Product Owner decision. |
| EF model check / migration scaffolding / SQL review | **BLOCKED** | No authorized runner was available for a fresh design-time build. No Sprint 3 migration, Designer, or snapshot update was generated and no SQL was reviewed. The exact final migration ID is unavailable. Do not use `--no-build` against unverified/stale compiled outputs. |
| Frontend TypeScript / production build | **BLOCKED** | Not run because no currently authorized build runner was available under the active restriction. No browser or runtime tests were run. |
| Unit, API, SQL Server, concurrency, browser, permission UAT | Not run | Product Owner has deferred these to consolidated Sprint 7 UAT. |
| Database identity, migration history, backup, schema, or deployment | **NOT TESTED** | No database connection or mutation was made; deployment remains unauthorized. |

## Files changed for Sprint 3 source

- Backend: `PaymentSettlement`, `ProjectPaymentRequestDetail`, settlement EF configurations, `PaymentObligationService`, finance DTOs, `ProjectPaymentRequestWorkflow`, `ProjectPaymentRequestController`, `PaymentObligationController`, `Program`, `SeedDataProvider`, and two Project Management reporting queries affected by nullable obligation ownership.
- Frontend: `ProjectPaymentRequests`, `ProjectFinanceTab`, `PlannedPaymentsPage`, `project-financials` API contracts/hooks, `permissions`, `ProjectFinancialWorkspace` event labels, and EN/AR locale messages.
- Documentation: this report, Planned Payments integration note, and the Master UAT checklist.

## Required next steps

1. Obtain an explicitly authorized build environment, or have the security reviewer disclose the exact telemetry process, destination, and payload for review. Do not bypass the rejection through a different shell, indirect process, or build path.
2. In that environment, build Persistence/design-time and API into approved isolated output, then scaffold the migration from the current compiled source; synchronize Designer/snapshot, inspect incremental SQL, and run the offline model gate. Confirm the ProjectId backfill abort condition before accepting the migration.
3. Resolve the Product Owner's initial delegation authority for `PaymentSettlement.Record` without automatic role grants.
4. Use the established database gate for verified Development identity/history/schema/backup and obtain separate deployment authorization.
5. Execute the documented positive, negative, cross-scope, integrity, audit, and concurrency cases during Sprint 7 UAT.

**Current classification: SOURCE IMPLEMENTED / COMPILE NOT VERIFIED (BUILD EXECUTION BLOCKED) / MIGRATION NOT GENERATED / SNAPSHOT NOT SYNCHRONIZED / SQL NOT REVIEWED / DATABASE DEPLOYMENT PENDING / RUNTIME UAT DEFERRED.**

## Sprint 5 dependency update

The Sprint 3 rule that a settlement's actual payment period can differ from the originating request period remains. Once an approved closing transfers the remaining unpaid commitment, the settlement must match the currently assigned, approved, open Annual Budget and use a payment date within that Tenant Fiscal Year. It records the active transfer reference; it does not reserve or release budget again. This is source-level only pending coordinated Sprint 7A EF reconciliation and Sprint 7B runtime UAT.

The legacy `PaymentObligation` settlement operation now resolves the payment date through the Tenant fiscal calendar and rejects a posting into a formally closed Project Fiscal Year. It serializes through the shared Project-row lock and records settlement plus audit in one transaction. This source change does not convert Payment Obligations into Annual Budget allocations; concurrency and closed-period enforcement still require Sprint 7B SQL Server verification.
