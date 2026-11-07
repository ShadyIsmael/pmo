# Combined Meeting and Risk Notification Readiness

**Date:** 2026-11-06  
**Scope:** Meeting Minutes/Notifications and Risk Action Follow-up  
**Decision:** **NO-GO for migration application or runtime/UAT readiness.** The migration source is reviewable, but the live Development target and migration history could not be verified. Risk follow-up also has two confirmed requirements gaps.

## Executive result

- Source contains **13 EF migration classes**. The offline EF model gate reports **synchronized: Yes**.
- The three feature migrations are source-present and their generated SQL was reviewed without execution. The exact Development applied/pending set remains **unknown**.
- The configured development JSON points to `tcp:localhost,1433 / NexusPmo` with Windows Authentication. This is configuration evidence only. Neither `sqlcmd` nor the repository EF `inspect` gate could open a socket, so the live SQL Server name, instance, database ID, login, migration history, and physical schema remain **unverified**.
- No migration SQL, database write, backup, restore, seed, or browser UAT was performed.
- Read-only review was split across Meeting behavior, Risk follow-up, migration inventory, and infrastructure/policy; the Main Agent integrated the findings. No subagent changed files or touched SQL Server.
- Meeting source behavior is substantially implemented. Risk Action Follow-up does not yet meet the stated date-only and Organization working-calendar requirements.
- React typecheck, focused lint, and production build passed. Focused API tests were not run. A prior React focused run was reported as 38 tests across 8 files; an existing implementation report says 41 across 8 files. That count discrepancy remains unresolved because the tests were not rerun after the earlier telemetry egress review block.

## Environment and database evidence

| Check | Result |
|---|---|
| .NET SDK | `dotnet --version` = 10.0.302. `dotnet --info` failed when the restricted process tried to open the Windows Service Control Manager (Access denied). |
| EF CLI | 10.0.10 |
| API Development configuration | `appsettings.Development.json` contains `Server=tcp:localhost,1433;Database=NexusPmo;Trusted_Connection=True;...`; no password was printed. The shell had no `ASPNETCORE_ENVIRONMENT` or connection override set before the read-only check. |
| Live server/database identity | **Not verified.** The task's expected target is Shady/NexusPmo; no live `SERVERPROPERTY('ServerName')`, `DB_NAME()`, or `DB_ID()` result was obtained. |
| Read-only `sqlcmd` | Failed before login: TCP socket access was denied by the execution environment; login timed out. |
| Repository EF `inspect` | Ran with Development environment and the configured connection. Gate build succeeded with four NU1900 audit-metadata warnings; inspect then failed with `SqlException` / `TCP Provider ... Access is denied`. Exit code 1. |
| API/runtime effective connection | **Not verified.** The browser log shows requests to `localhost:5000`, but no accessible running API process or server-side exception log was available. |
| `__EFMigrationsHistory` and physical schema | **Not inspected.** |

## Backup and recovery readiness

The documented ordinary EF apply workflow requires a new checksummed copy-only SQL Server backup on storage visible to SQL Server, independent of the database files, followed by `RESTORE VERIFYONLY`. The no-backup exception is limited to the separately authorized reset path, which is prohibited in this task. The repository documents this backup-and-verify sequence as its pre-apply recovery gate.

Historical records are not consistent enough to establish a current acceptable backup: older documentation reports a latest full/copy-only backup on 2026-10-28, while another later record references 2026-10-31; both place the database and backup media on `C:`. No live `msdb` query, backup-file check, independent destination, or current `RESTORE VERIFYONLY` result was available. **Backup status for this apply: not verified; prerequisite remains unsatisfied.** No backup was created or overwritten.

The live connection failure is the primary execution blocker. The expected host and database in the request and JSON configuration do not prove that the current API and inspection command resolve to the same database.

## EF model and migration inventory

Command: `scripts/database/ef-database-gate.ps1 model` — **exit 0**. It reported `NEXUS.Persistence.AppDbContext`, migration assembly `NEXUS.Persistence`, 13 repository migrations, and `EF Model synchronized: Yes`. This offline result does not prove that any migration is applied or that the physical schema matches.

| Migration ID | Purpose | In source | Applied to Development | Dependency / data and deployment risk |
|---|---|---:|---|---|
| `20261029155617_InitialFopxPmBaseline` | Initial FOPX PM schema baseline | Yes | Unknown | Foundational schema; high impact if unexpectedly pending. |
| `20261029181208_ProjectPaymentRequestPersistence` | Payment Request persistence | Yes | Unknown | Depends on baseline project/finance tables; inspect its complete table and constraint operations if pending. |
| `20261029183557_AddTenantRegistry` | Tenant registry persistence | Yes | Unknown | Depends on baseline identity/tenant model; impacts tenant boundaries. |
| `20261030122718_AddProjectAnnualBudgetFoundation` | Annual Budget persistence | Yes | Unknown | Depends on Project and Tenant schema; table/index additions. |
| `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation` | Associates Payment Requests with annual budgets | Yes | Unknown | Depends on the Payment Request and Annual Budget migrations; association/FK/index compatibility must be checked. |
| `20261031000210_AddFinancialPersistenceSprints3to6` | Financial persistence for later sprints | Yes | Unknown | Broad financial schema changes, including alterations; review target data and constraints before applying. |
| `20261031142036_AlignResourceProjectTeamsAndInvitations` | Aligns Resource, Project Team, and invitation persistence | Yes | Unknown | Contains SQL preflight/data-copy logic and constraint/index changes; data-dependent and not safe to assume non-destructive. |
| `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` | Annual Budget approval configuration | Yes | Unknown | Depends on annual budget/project schema; new tables, scoped FKs, and unique indexes. |
| `20261101213237_SynchronizePlanningPhysicalSchema` | Aligns planning schema with the model | Yes | Unknown | Tightens planning-column nullability and adds physical constraints; may fail against existing nulls/drift. |
| `20261102190318_AddStableSystemRoleIdentity` | Adds stable system-role identity | Yes | Unknown | Adds/backfills identity values and uniqueness/check constraints; existing duplicate or invalid data can block it. |
| `20261105184921_AddRiskActionPublicIdForFollowUp` | Adds public ID for Risk Action follow-up links | Yes | Unknown | Adds `RiskActions.PublicId` as `uniqueidentifier NOT NULL DEFAULT NEWID()` and filtered unique `(TenantId, PublicId)` index. Existing rows receive generated IDs; index build may scan/lock the table. |
| `20261105201430_AddRiskActionSystemUserOwners` | Adds resolvable Risk and Risk Action owners | Yes | Unknown | Adds nullable `OwnerUserId` GUIDs to `Risks` and `RiskActions`; no FK or backfill. Additive, but existing owner resolution remains null until populated. |
| `20261105214717_AddMeetingNotificationPublicIdentifiers` | Adds public IDs used by Meeting/Minutes notification links | Yes | Unknown | Adds nullable `Notifications.ReferencePublicId`, and non-null `Meetings.PublicId` / `MeetingMinutesComments.PublicId` with `NEWID()` defaults, then unique indexes. Existing rows receive generated values; index builds may scan/lock tables. |

**Pending count and names: unknown.** The current history could not be read. It is not safe to state that only the last three are pending. If an earlier migration is unapplied, EF ordering may require migrations with data-sensitive preflights or nullability changes before the notification migrations.

### Notification migration SQL review

Generated an idempotent script offline from `20261102190318_AddStableSystemRoleIdentity` through `20261105214717_AddMeetingNotificationPublicIdentifiers`, and reviewed it statically. It contains the three feature migrations only. The source/SQL operations reviewed are additive: `NEWID()` defaults for new required GUID columns, nullable owner/reference columns, and unique indexes. No table or row deletion was found in these three `Up` operations. The Meeting checked-in artifact is `scripts/database/20261105214717_AddMeetingNotificationPublicIdentifiers.idempotent.sql`; it guards each operation on the EF history ID and uses a transaction.

Risk migration SQL artifacts are not checked into `scripts/database`; their generated SQL was produced only in the temporary review step. No SQL was executed. SQL Server supports `NEWID()` as a default expression. The unique-index creation is the main operational lock/scan risk. `Down` drops the new public/owner/reference values; rollback after application use would lose those values and is not a safe recovery plan.

## Source behavior review

### Meeting Minutes and Notifications

- Starting a scheduled meeting persists `InProgress` and the actual UTC start. Completing an in-progress meeting persists `Completed` and the actual end; completion does **not** publish Minutes.
- A valid completed meeting without a Minutes row returns `200/null`; explicit empty Draft creation is idempotent and preserves existing content. API errors are not mapped to an empty Draft.
- React resolves the public route ID through Meeting Details, then uses the internal Meeting ID for Minutes API calls. Notification links use public IDs.
- Publication requires a completed meeting and an explicit Minutes publish operation. Draft save or meeting end does not trigger the Published event.
- Publisher/scheduler source checks active invitees, effective Meeting view/project scope, actor exclusions, persisted lifecycle state, recurrence/reschedule state, and deterministic idempotency keys. Comment/reply notification payloads do not include comment text.
- Focused API tests exist for completion, empty/preserved Drafts, ID mapping, lifecycle events, scheduler rescheduling, authorization/scope, and idempotency; they were not run successfully in this task.

### Risk Action Follow-up

Two requirements gaps are confirmed:

1. **Not date-only:** `RiskWorkflow.NextFollowUpDate`, API DTO fields, and the EF snapshot use `DateTime?`. The processor truncates to `.Date`/`DateOnly` for scheduling identity, but storage/API still accept a time. A conversion to SQL `date` needs a target-data check before any migration because existing time components are unknown.
2. **No Organization calendar association:** scheduling resolves a Project-selected Working Calendar or the single tenant default. Organization regional settings (including time zone) are resolved, but an Organization cannot own or supply the inherited Working Calendar required by the request.

Existing resolver/processor tests cover working-day starts, holidays, time zones/DST, invalid calendars, owner deduplication, status suppression, rescheduling, concurrency/restart, and tenant boundaries. The inspected tests do not explicitly cover Organization calendar configuration changes or transient delivery retry. Tests are source evidence only; runtime behavior remains unverified.

## Build, test, and telemetry results

| Check | Result |
|---|---|
| EF model gate build | Passed; compiled Persistence and referenced Meeting, Notification, and Risk modules. NU1900 warnings were emitted for unavailable NuGet vulnerability metadata. |
| Direct Persistence build (`--no-restore`) | Exit 1; no C# compilation errors reported. NU1900 was present; direct project build diagnostics also show SDK/workload resolver limitations in this environment. |
| Direct MppAdapter/API build | Failed before a usable compile result. Diagnostic output reports `MSB4276` because `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` were not resolved; `_GetProjectReferenceTargetFrameworkProperties` failed with zero surfaced compiler errors. A prior full API attempt in this task ran for about 4:34 and failed copying a locked `MppAdapter.dll` (`MSB3021`/`MSB3027`); its lock owner was not identified. No process was terminated. |
| NU1900 | NuGet could not load `https://api.nuget.org/v3/index.json` vulnerability metadata. This is an audit-service/network failure, not evidence of an affected package or C# defect. Security/audit settings were not disabled. |
| Focused API tests | **Not run to completion.** Prior attempts stalled before results amid the IKVM/MPXJ build path. No pass is claimed. |
| React typecheck | Passed (`npm run typecheck`; also included in production build). |
| Focused React lint | Passed for Meeting page, Notifications page, notification helpers, and focused tests. |
| React production build | Passed (`npm run build`, 2892 modules transformed). Vite emitted existing chunk-size and mixed static/dynamic import warnings; no build error. |
| Focused React tests | Prior in-session result reported **38 passed / 8 files**. The implementation report records **41 / 8 files**. No new run was made: a previous duplicate Vitest attempt was blocked by automatic review after npm egress to an untrusted external host, and no approved offline/telemetry-free mode was identified. Treat the result count as unresolved and rerun in an approved local environment. |
| Browser/UAT | Not run. |

The telemetry review record does not identify the initiating package, exact destination, payload, or whether data left the environment. No reviewer bypass or telemetry workaround was used.

## Browser errors supplied during the task

- `GET /api/auth/me` returned 401 twice. This is consistent with an absent/expired session and React development Strict Mode double effect, but the active authentication state was not available to confirm the cause.
- `GET /api/meetings?...` returned 500. The current Meeting list projection selects `Meetings.PublicId`, introduced by migration `20261105214717_AddMeetingNotificationPublicIdentifiers`; a missing column is a plausible cause, but this is **inference**, not a verified root cause. The SQL exception/server log and live schema were unavailable.
- The React DevTools notice is informational.

## Database delivery completion fields

| Required evidence | Status |
|---|---|
| EF model synchronized | **Yes** — offline gate. |
| Pending migration count and exact IDs | **Unknown** — live history unavailable. Source count is 13. |
| Development DB identity verified | **No** — socket access denied before server identity/login. |
| Independent backup verified | **No / unverified** — historical repository records do not establish a current independent, RESTORE-verified backup. No backup was created. |
| Migrations applied | **None**. |
| Required seed mode/idempotency | **Not run / not applicable** — this task is migration-only; no reset or seed was authorized. |
| Physical schema verified | **No**. |
| Persistence tests against Development SQL Server | **Not run**. |
| Production access or changes | **None**. |

## Deployment recommendation and exact blockers

Do not apply migrations or claim Development readiness from this execution. The authorized migration apply could not be carried out safely because:

1. The execution environment cannot reach SQL Server, so the exact host/instance/database ID, effective API target, actual applied history, and physical schema are unknown.
2. The ordinary repository apply workflow requires a freshly verified backup on storage independent of the database files. No current proof was available, and no backup was created.
3. It is unknown whether earlier, potentially data-sensitive migrations precede the three feature migrations in Development history.
4. Risk follow-up does not meet the requested date-only and Organization Working Calendar behavior; a date conversion also needs a data compatibility check.
5. API tests and a complete API build remain blocked by environment/security-review constraints; no backend runtime pass is available.

The next safe execution must occur in an approved host-connected Development environment: run the repository EF `inspect` gate to capture actual identity/history/schema; verify the independently stored recovery backup required by the ordinary apply policy; review the exact generated pending SQL; apply only eligible pending migrations through the EF gate; then verify migration history and physical schema and run focused tests in an approved local/offline environment. Do not bypass the SQL/network, backup, SDK, or telemetry controls.

## Follow-up live Development inspection — 2026-11-06

The earlier connectivity blocker is superseded by a fresh read-only gate run from the authorized local Windows context. The target is verified as `Shady/NexusPmo` (database ID 5, Windows integrated authentication); the effective API Development target matches. The live ten migration IDs match source migrations 1–10 exactly, and the pending chain is `20261105184921_AddRiskActionPublicIdForFollowUp`, `20261105201430_AddRiskActionSystemUserOwners`, and `20261105214717_AddMeetingNotificationPublicIdentifiers`. The twelve older migration IDs deleted from the shared worktree are not present in live history, so no applied-history/baseline conflict was detected.

The three forward migrations were reviewed from target-specific SQL (SHA-256 `E7868EF5CD5EFC2CCF28A3B1C896A4FB1BCD8DB9ECB4FD7ACBC756D723C0D65B`): additive public-ID/nullable owner/reference columns and unique indexes, with no forward drops, deletes, updates, column alterations, or foreign keys. Nine pending objects are absent, as expected. Sixteen other current-model differences remain on `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`; physical schema matches the last applied migration model but does not match the full current model.

No migration or backup was performed. The host has only one fixed C: volume, which also contains the database files, and no mapped network share; a same-volume copy does not meet the independent-backup requirement. The API build passed (0 errors, 4 warnings) and React typecheck passed. React build/Vitest were blocked by `spawn EPERM`; the narrowed API notification/date subset passed 16/16, while the broader SQLite/InMemory batch had 27 setup failures (`SQLite Error 1: 'near "max": syntax error'`) and did not validate SQL Server persistence. Full status and exact remaining gates are recorded in [`ONE-PASS-DEV-EXECUTION-20261106.md`](../development/ONE-PASS-DEV-EXECUTION-20261106.md).
