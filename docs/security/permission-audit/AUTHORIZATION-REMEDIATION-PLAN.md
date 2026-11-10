# Authorization remediation plan and source status

**Backup-policy supersession (2026-11-10):** Development/UAT backups are optional for migrations and other authorized changes; independent storage and `RESTORE VERIFYONLY` are not prerequisites. Exact target, migration, and schema checks remain required. Destructive reset still requires explicit Product Owner authorization. This update does not authorize a reset or permission reconciliation. Production backup and recovery safeguards remain mandatory.

This file continues the existing audit plan. Source changes are present. Final build results are recorded below; runtime UAT and the requested Development database recreation remain pending.

## Source remediation

| Finding | Implemented source change | Evidence |
|---|---|---|
| Issue read API | Dedicated `Issue.View` policy; fail-closed tenant/project management scope. | Issue controller, service, catalog, policy registration, contract tests. |
| Plugin registry | Dedicated tenant-scoped `Plugin.View`; current-tenant and soft-delete filters. | Plugin controller/service, catalog, policy registration, contract tests. |
| Route/API mismatch | Canonical `Project.View`, `Planning.View`, `Audit.View`, `WorkItem.View`, `Settings.View`, and `Plugin.View` guards in supported React app. | `apps/web-react/src/routes/AppRoutes.tsx`, navigation, permission constants. |
| Optional Annual Budget draft | `ProjectAnnualBudget.Create` is checked independently of project creation. | Project API service and React project-create form. |
| Status report export | Server endpoint requires `StatusReport.Export` and returns Published reports only. | Project controller and React export flow; API authorization contract test. |
| Directory connection writes | Added `Directory.ManageConnection`, with View dependency and English/Arabic labels. | Canonical catalog, API policy, connection/scope actions, role editor metadata. |
| Financial object permissions | Added 24 granular Budget, BudgetRevision, Cost, Commitment, PaymentObligation, and PaymentObligationSettlement permissions; retired broad `ProjectFinance.Manage`. | Canonical catalog, canonical policy names, financial controllers, React permission map, matrix, negative source tests. |
| Stakeholder service entrypoints | Added service-level tenant, current-permission, actor, project-scope, and read-only checks. | Project stakeholder and invitation services/controllers; matrix and endpoint inventory. |
| User and role administration service entrypoints | Require fresh `Users.View`/`Roles.View` for service reads, bind supplied actor and tenant IDs to the authenticated principal, and enforce exact current persisted permissions before changes. | `UserAdministrationService.cs`, `RoleAdministrationService.cs`, controller result mapping, and service-entry tests; test execution remains pending. |
| Development seed role mapping | Normalized Status Report role grants to the seed's `Resource|Action` tuple format; added a regression assertion for manager-role grants. | `SeedDataProvider.cs` and `SeedDataProviderTests.cs`; final test execution is pending. |
| Assignment Recovery policy alias | Bound read routes directly to canonical `Governance.AssignmentRecovery.View` and removed `AssignmentRecovery.Discover`. | Controller and policy registration; source scan clean. |
| Payment request approve and settlement endpoints referenced missing policy registrations | Register each endpoint policy against its same canonical permission key and add exact-key policy contract coverage. | `Program.cs`, `FinancialPermissionPolicyRegistration.cs`, and `PermissionAuthorizationContractTests.cs`; source scan now finds every literal `[Authorize(Policy=...)]` registered. Test execution remains pending. |
| API inventory retained stale broad finance policy strings in 20 endpoint records | Refresh those rows to the current Budget, BudgetRevision, Commitment, Cost, PaymentObligation, and settlement action policies. | `API-ENDPOINT-INVENTORY.csv`; all 602 rows retained and no removed broad alias remains in policy evidence. |
| Resource authorization still exposed unsupported `Resource.Allocate` and broad `Resource.ManageRates` keys | Consolidate allocation on `Resource.Assign`; retire ManageRates and require ViewCostRate for cost disclosure and as the dependency for EditCostRate. | Catalog, seeds, React permission gates, services, matrix; negative UI/API source tests added; final execution pending. |

## Explicit Development permission reconciliation

`SeedDataProvider.RebuildCanonicalPermissionsAsync` is exposed only by `--rebuild-canonical-permissions` in Development. The command requires explicit SQL Server and database values and independently checks `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and `DB_ID()`; it refuses model changes or pending migrations. A backup may be created when explicitly requested, but independent backup storage and `RESTORE VERIFYONLY` are not prerequisites for this Development operation. A missing backup must not block it.

The transaction is tenant-scoped to the Development seed tenant. It creates missing active catalog rows, normalizes active names/descriptions, soft-deletes unknown/retired rows, removes only their role links, archives only their direct overrides, and audits each changed object. It does not add grants to existing roles or users or fan aliases into new operations. Existing active canonical grants remain unchanged.

The then-current user instruction requested a `NexusPmo` reset by applying EF migrations and running the complete seed; that request was not executed in this report. The final build/test pass is recorded below. The offline EF model is synchronized, but `sqlcmd -S tcp:localhost,1433 -d NexusPmo -E` failed with `Cannot generate SSPI context`, so the live identity and schema could not be verified. **Policy history:** the 2026-11-04 repository update permitted the exact Development `Shady/NexusPmo` reset without a backup only after explicit Product Owner authorization and exact target/environment checks. The current 2026-11-10 policy makes backups optional for all Development/UAT operations; it retains the secure Admin bootstrap, target, migration, schema, and destructive-operation authorization gates. The current backup-policy task does not authorize or resume the reset.

## Full action coverage

The raw API/UI inventories are retained as candidate evidence. The business action denominator is not deduplicated; no coverage percentage is reported. Consolidated UAT must trace each material operation through UI, API, service, tenant boundary, resource scope, and workflow assignment.

## Final validation gates

1. Finish all source changes and documentation; do not run build/tests or database tests during implementation.
2. After implementation is complete, verify the offline EF model/snapshot, exact Development target, provider, migrations, and seed entrypoint.
3. Before any separately authorized reset, confirm the secure Admin bootstrap value is available without exposing it, confirm explicit Product Owner authorization for the exact Development `Shady/NexusPmo` reset is recorded and referenced, and verify the live environment/server/database identity. Backup is optional; reset authorization and all safety gates remain mandatory.
4. Invoke only the Product Owner-authorized `Shady/NexusPmo` reset through the EF gate. It applies all repository EF migrations and runs the full seed twice; verify catalog, role grants, protected Admin baseline, and absence of retired permissions afterward.
5. Run the complete build/test set once at the end, including direct grant/deny, cross-tenant, and fresh-seed idempotency tests; record any gate that cannot execute.

## Completion status

| Gate | Status |
|---|---|
| Source catalog and policies | Implemented; build/test results recorded separately. |
| Supported React route guards | Implemented in `apps/web-react`. |
| Current Development database identity | Failed verification: SSPI context unavailable in current session. |
| Backup independently verified | No; no reset was attempted. |
| EF reset and seed | Pending identity and recovery gates. |
| Browser/runtime UAT | Not run; authenticated runtime session unavailable. |

## Final build and test evidence (2026-11-02)

- API solution build passed with 0 errors and 352 IKVM/MPP warnings.
- React production build and TypeScript check passed.
- EF gate project build passed with 0 warnings/errors; its offline model action reports 9 migrations and `EF Model synchronized: Yes` without opening a database connection.
- React tests: 724 passed, 38 failed across 20 files.
- API authorization test project failed before VSTest; no API tests executed.
- Direct HTTP authorization, browser UAT, SQL identity, backup, reset, and seed remain unverified. See `SECURITY-TEST-RESULTS.md` and `DATABASE-MIGRATION-SEED-REPORT.md`.
