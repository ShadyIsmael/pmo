# Legacy Permission Removal Report

**Snapshot:** 2026-11-02

## Result

The active source catalog contains 154 unique canonical keys and no inactive compatibility entries. Historical aliases remain only as documented/retired matrix records or negative-test inputs. Runtime evaluation only returns active catalog keys; unknown and retired grants are filtered before authorization.

The updated matrix retains 32 retired/history rows. Of those, 28 contain distinct nonblank historical key strings; four retain a seed/action history record without an `Existing Key` value. These are historical records, not active permissions. `ProjectFinance.Manage` and `Resource.ManageRates` were removed as broad fallbacks. `Resource.Allocate` was merged into the already enforced `Resource.Assign` capability; no runtime alias remains.

Examples of retired broad or compatibility keys recorded in the matrix include `projects.read`, `finances.write`, `Roles.Manage`, `ReferenceData.Manage`, and `ProjectFinance.Manage`. Legacy `settings.read`/`settings.write` values were also removed from the active Platform Settings authorization handler; platform access remains restricted to the separate `PlatformAdministrator` role or deployment-managed administrator allowlist. Their appearances in negative tests or historical reports are intentional and do not grant authority.

## Rebuild delta

The prior audit reports recorded 133 active keys. This update added 24 granular finance permissions and retired `ProjectFinance.Manage`, `Resource.Allocate`, and `Resource.ManageRates`, for a net increase of 21 and a current total of 154. The task brief's 136-active figure has no matching repository snapshot; its three-key difference from the 133-key report baseline cannot be attributed from the available artifacts.

## Source-level evidence

- `PermissionCatalog.cs` contains 154 unique active keys and stable IDs; no inactive compatibility entry is returned by `ByKey`.
- The unused `LegacyMapping` field was removed from the catalog record and Roles API DTO; all 156 former values were null, and no frontend consumer existed.
- `PermissionCatalog.EnforceDependencies` discards unknown/inactive keys before enforcing declared dependencies.
- `Program.cs`, frontend permission constants, route guards, financial components, and active seed definitions use canonical keys; old strings remain only in retirement/negative-test evidence.
- The `AssignmentRecovery.Discover`, `AssignmentRecovery.Act`, and `Handover.Initiate` policy aliases have been removed. Assignment candidate and Handover lookup/cancel endpoints use canonical View policies; their application services still require the exact action permission, current tenant/resource scope, and requester/manager eligibility. `ProjectPaymentRequest.ViewOrAssignedReviewer` remains a scoped composite policy: payment-request detail re-evaluates current tenant-scoped effective permissions and requires an active pending reviewer assignment.
- The active policy inventory also found two canonical financial policies missing from the central registration: `ProjectPaymentRequest.Approve` and `PaymentSettlement.Record`. Both are now registered to require their exact matching permission claim. This closes a runtime policy-resolution defect; the final authorization tests are still required.
- Active runtime policy/source scans found none of the retired keys `ProjectFinance.Manage`, `Resource.Allocate`, `Resource.ManageRates`, `projects.read`, `finances.write`, `Roles.Manage`, `ReferenceData.Manage`, `settings.read`, `settings.write`, `users.write`, or the removed controller policy aliases. Remaining references are historical matrix entries or explicit negative tests. Persisted rows still require the authorized Development reset and SQL verification below.
- `Resource.EditCostRate` now declares `Resource.ViewCostRate` as a required dependency. Capacity and cost-utilization services disclose cost data only for the exact `Resource.ViewCostRate` claim; edit or retired ManageRates claims are not read fallbacks.
- The financial service/controller negative test asserts that `ProjectFinance.View`, `ProjectFinance.Manage`, and `finances.write` do not authorize payment-obligation creation and do not mutate state. Test execution is pending the final consolidated pass.

## Persisted data status

No SQL database query or write succeeded in this execution because Windows integrated SQL authentication failed with `Cannot generate SSPI context`. Consequently, removal of retired rows from persisted `Permissions`, role links, and direct overrides has not been verified. The requested clean Development reset and EF seed remain pending target identity and recovery verification.

## Final recovery session recheck - 2026-11-02

The active source review still found no retired permission aliases in backend or supported React authorization code. The current session process identity is `shady\shady`, but this is not evidence of SQL authentication. Read-only `sqlcmd` to `tcp:localhost,1433` failed before returning SQL Server identity with `TCP Provider: An attempt was made to access a socket in a way forbidden by its access permissions` followed by login timeout. No `MSSQLSERVER` service or TCP 1433 listener was visible to this executor. The local Development configuration names `tcp:localhost,1433` and `NexusPmo`, but the configured alias is not proof of the connected target. No SQL identity, persisted legacy-key state, backup, reset, migration, or seed was verified in this session.
