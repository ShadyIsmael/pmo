# Tenant Fiscal Year Configuration — Sprint 0

**Scope:** Tenant-level fiscal calendar prerequisite for Annual Budget Foundation.
**Status:** Source implementation complete; focused verification passed. Runtime database verification was not performed for this task.
**Database:** No EF model change, migration, or database write is required by this delivery.

## Configuration contract

- Setting key: `financial.fiscalYearStartMonth`.
- Type and range: integer month `1`–`12`.
- Default: January (`1`) for a Tenant with no stored override.
- Scope: Tenant only. Organizations and Projects inherit the Tenant calendar. Platform, Organization, Project, and User writes are not allowed.
- Storage: existing typed `ConfigurationSettings` rows. No Tenant entity column or independent settings store is introduced.
- Time zone: use the Tenant's existing effective `regional.defaultTimeZone`; use UTC when absent. Server-local time is never used. No timezone setting or override is added here.

## Fiscal-year calculation

The label is the ending year. January-start fiscal years match the calendar year. For a start month other than January, dates on or after that month belong to the fiscal year ending in the next calendar year; earlier dates belong to the fiscal year ending in the current calendar year.

For a specified fiscal-year label `Y`, a January period starts on `Y-01-01`. Any other period starts on the first day of the configured month in year `Y-1`. The inclusive end date is one day before the same start date in the following year. `EndDateExclusive` supports half-open timestamp queries. Tenant-local start-of-day boundaries are converted to UTC using the configured Tenant time zone, including time zones whose daylight-saving transition occurs at midnight.

Examples:

| Start month | Label | Start | Inclusive end |
|---|---:|---|---|
| January | FY 2027 | 2027-01-01 | 2027-12-31 |
| April | FY 2027 | 2026-04-01 | 2027-03-31 |
| July | FY 2027 | 2026-07-01 | 2027-06-30 |
| December | FY 2027 | 2026-12-01 | 2027-11-30 |

## API, authorization, and user interface

- `GET /api/settings/tenant` returns the effective Tenant setting.
- `GET /api/settings/tenant/fiscal-year` returns a preview for the current setting; optional `startMonth=1..12` previews a draft choice.
- Existing `PUT /api/settings/tenant/{key}` and reset endpoints persist the value under the `Settings.Write` policy. Invalid months are rejected.
- Tenant identity comes only from the authenticated `tenantId` claim; no Tenant identifier is accepted from the preview query or update body. Preview/read uses `Settings.Read`.
- `ConfigurationSettingsService` independently enforces `Settings.Read` for Tenant/Organization reads and `Settings.Write` for Tenant/Organization writes and resets. Controller policies remain the HTTP boundary. The general `/api/settings/runtime` projection exposes regional presentation settings only; fiscal-year configuration is omitted.
- Administration > Tenant Settings > Financial Configuration shows January–December, a server-calculated FY label/start/end preview, Save/Cancel, and localized status/error feedback. The control is read-only without the existing Tenant write permission. Organization and Platform editors do not expose this setting.
- English and Arabic labels are provided. The existing i18n provider supplies RTL direction for Arabic and supports the shared light/dark theme tokens.

## Permission matrix

| Action | Existing permission | Scope and enforcement |
|---|---|---|
| Read Tenant settings and fiscal-year preview | `settings.read` (`Settings.Read`) | Authenticated Tenant from the `tenantId` claim; checked by the controller and settings application service. Preview accepts no Tenant ID. |
| Change or reset Tenant fiscal-year month | `settings.write` (`Settings.Write`) | Authenticated Tenant from the `tenantId` claim; checked by the controller and settings application service. The setting definition permits Tenant scope only. |
| Configure permissions for roles | Existing `settings.read` / `settings.write` catalog entries | Identity catalog and Roles UI place them under Settings with English/Arabic labels. The catalog only exposes permissions the acting role administrator can delegate. |

No permission key was introduced and no existing role grant was added or changed for this feature. Platform, Organization, Project, and User scopes cannot override the fiscal-year setting. Positive, negative, and cross-scope runtime cases are recorded in the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#tenant-fiscal-year-configuration-security).

## Historical and audit safety

Changing the start month changes future fiscal-period interpretation. It is allowed only when no fiscal-year-dependent persisted records exist for that Tenant. The guard checks all `Budget` (`FiscalYear`/`FiscalQuarter`) and `PortfolioBudget` (`FiscalYear`/`FiscalQuarter`) rows, including soft-deleted rows, within the same scoped serializable transaction as the settings update. A detected record produces a conflict with an actionable message. No historical period, transaction, closed period, Project budget ceiling, or Payment Request allocation is rewritten.

Successful effective changes append `FiscalYearStartMonthChanged` to the existing append-only `AuditEntries` store in the settings transaction. The event includes the Tenant, authenticated actor, UTC `Timestamp`, and previous/new month. A repeated write that leaves the effective month unchanged does not emit a change event. No separate audit store is created.

No annual budget, closing, carry-forward, payment cutover, or Program/Portfolio Finance behavior is implemented. Fiscal-year-dependent entities added later must be added to the change guard before they are persisted.

## Database and migration status

This feature uses `ConfigurationSettings`, already part of the current model. It does not edit `AppDbContext`, entity mappings, the snapshot, or migrations. Source migration count remains three: `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, and `20261029183557_AddTenantRegistry`.

The latest recorded Development delivery reports all three migrations applied to `Shady/NexusPmo` (database ID 9), no pending migrations, and no fiscal or project financial records in the identity-only seeded environment. See [database Sprint 04 delivery](database-sprint04-tenant-foundation.md#development-delivery-record-2026-10-30). This task does not reconnect to that database, apply SQL, or alter database data. Runtime confirmation remains subject to the established documented database workflow.

## Verification record

| Acceptance criterion | Status | Evidence |
|---|---|---|
| Default January and Tenant-only scope | PASS | Focused API test `NewTenantDefaultsToJanuaryAndJanuaryYearUsesCalendarLabel`; tenant-only scope asserted. |
| April, July, December boundaries and FY ending-year labels | PASS | Focused API tests `AprilAndJulyFiscalYearsUseEndingYearLabels` and `DecemberFiscalYearAndDateResolutionRespectBoundary`. |
| Leap-day and half-open timestamp handling | PASS | Focused API test `FiscalPeriodHandlesLeapDayAndUsesHalfOpenTimestampRange`. |
| Date resolution and invalid month rejection | PASS | Focused API tests `DecemberFiscalYearAndDateResolutionRespectBoundary` and `InvalidStartMonthIsRejected`. |
| Different Tenant calendars and claim-based isolation | PASS | Focused API tests `TenantFiscalYearServiceKeepsTenantCalendarsIsolated` and `AuthenticatedTenantClaimScopesSettingsReadAndUnauthorizedWriteFailsClosed`. |
| Settings.Write authorization and fail-closed identity | PASS | Controller policy and missing-Tenant-context behavior asserted in `AuthenticatedTenantClaimScopesSettingsReadAndUnauthorizedWriteFailsClosed`. |
| Permission catalog, Roles UI, and service-level enforcement | PASS (source/tests) | Reuses existing `settings.read` and `settings.write` catalog entries and bilingual labels; controller and application service enforce access; source test covers allowed read and denied read/write. Existing role grants were not changed. |
| Fiscal-dependent historical records block changes | PASS (source test) | EF InMemory test `AnyTenantBudgetHistoryBlocksCalendarChangesButNotOtherTenants`; SQL Server transaction/locking behavior was not runtime-tested. |
| Audit event contains Tenant, actor, UTC time, old/new values | PASS (source test) | Focused API test `FiscalYearChangeAuditCapturesTenantActorUtcAndOldNewMonths`; production database transaction behavior was not runtime-tested. |
| EN/AR and RTL UI | PASS | `tenant-fiscal-year-settings.test.tsx`: 2 tests passed, including Arabic/RTL preview, cancel, and save; typecheck and production build passed. |
| Light/Dark theme rendering | NOT TESTED | UI uses existing shared theme tokens; no explicit theme-toggle test was run. |
| Save/Cancel and permission-based editing | PASS (UI/source test) | Focused UI test verifies draft preview, cancel, and save; component disables editing when Tenant write permission is absent. |
| No EF schema change or migration required | PASS | Existing configuration table and source diff; no migration command run |
| Database remains unchanged by this task | PASS | No database connection or write command used |
| API project compile check | PASS | `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -p:BuildProjectReferences=false -p:OutputPath=C:\Users\shady\AppData\Local\Temp\FopxPmApiPermissionBuild\ -p:NoWarn=NU1900 -p:MSBuildEnableWorkloadResolver=false -m:1 --verbosity minimal`; temporary output, 0 errors and 352 IKVM/MPP warnings. A normal shared-output build was blocked because the running API process held output DLLs open (`MSB3021`/`MSB3027`). |
| Focused API tests | PASS | `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore -p:FocusedFiscalYearConfiguration=true -p:NoWarn=NU1900 -p:MSBuildEnableWorkloadResolver=false -m:1 --filter FullyQualifiedName~FiscalYearConfigurationTests --verbosity minimal`; 11/11 passed, including service-level allow/deny cases. |
| Frontend verification | PASS | `npm run typecheck`; focused fiscal UI test 2/2; `npm run lint` exit 0 with 7 existing warnings; `npm run build` exit 0. |
| Runtime API/database integration | NOT TESTED | No fiscal-setting request was run against Development in this task. No migration or database operation was performed. |
| Consolidated runtime permission scenarios | DEFERRED | See the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#tenant-fiscal-year-configuration-security). |

## Annual Budget Foundation handoff

Annual Budget Sprint 1 uses `ITenantFiscalYearService` for the Tenant's effective start month and current period, and `IFiscalYearCalendarService.ForFiscalYear` for a selected year. It preserves the ending-year label and date-only boundaries. The fiscal-year configuration change guard now includes Project annual budget rows (including soft-deleted rows) once `dbo.ProjectAnnualBudgets` exists. Before the additive migration is deployed, the guard checks for that table first so an existing database without the new table does not fail with SQL error 208. Closing and carry-forward execution remain deferred.

### Project Annual Budget Foundation Sprint 1 update - 2026-10-30

The separate [Project Annual Budget Foundation](../modules/PROJECT-ANNUAL-BUDGET-FOUNDATION.md) is authorized at source level. Its migration is not applied by this feature work. This update supersedes the earlier handoff's statement that all annual budget creation remained deferred; it does not change the Sprint 0 verification record above or imply database runtime verification.
