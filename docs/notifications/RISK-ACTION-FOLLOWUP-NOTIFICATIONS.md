# Risk Action Follow-up Notifications

**Execution update:** 2026-11-06  
**Source status:** Implemented; builds pass.  
**Development database status:** Not verified or updated.  
**Browser UAT:** Not run; reserved for the Product Owner.

## Behavior

NextFollowUpDate is a business date. The API contract uses yyyy-MM-dd/DateOnly; the existing SQL persistence type remains datetime2 and stores midnight. The user does not choose a notification time. UI display preserves the calendar date without browser-timezone shifting.

A hosted worker polls active Risk Actions once a minute and re-reads the Action, parent Risk, owners, Project, effective settings, Working Calendar, permissions, and management scope before dispatch. Due time is the configured start of the first eligible working day on or after the follow-up date. Weekday-specific work starts, non-working days, holidays, and the effective Organization timezone are honored. Daylight-saving gaps advance to the first valid local minute; ambiguous times use the earliest UTC instant. Server timezone and an assumed five-day week are not used.

## Effective Working Calendar

Resolution is Project calendar first, then the effective Organization setting, then its inherited Tenant setting (workingCalendar.defaultCalendarId). Organization override reset removes the override so the Tenant value is inherited. The calendar selector uses existing Tenant-scoped base calendar lookup and existing settings APIs; it does not add a new calendar relationship or invent a default schedule. Missing, inaccessible, ambiguous, or invalid configuration leaves the reminder pending and emits a rate-limited diagnostic.

Risk Action recipients are the current active same-Tenant Action Owner and Risk Owner System Users. Existing Resource-to-User ownership remains compatible. Recipients are deduplicated by user ID. Current effective Risk.View, Active Deny, Tenant boundaries, and Project management scope are rechecked before delivery and when opening the target. Existing Settings.View is accepted for reading the calendar lookup; existing Resource lookup permissions remain supported. No permission key or role grant was added.

## Delivery and retry semantics

INotificationService.SendIdempotentAsync and the existing notification delivery workers remain authoritative. Occurrence identity covers Tenant, Risk Action, date-only occurrence, and recipient. Retries, worker restarts, and concurrent processing do not create duplicates. A changed date or owner creates the corresponding new occurrence; already-delivered history is retained. ScheduledAt records the calculated due instant and SentAt records actual processing time, including recovery after a missed due instant.

## Migrations and schema

The source migration set contains 13 migration IDs. Three notification-related migrations were reviewed:

- 20261105184921_AddRiskActionPublicIdForFollowUp
- 20261105201430_AddRiskActionSystemUserOwners
- 20261105214717_AddMeetingNotificationPublicIdentifiers

The generated idempotent forward SQL was reviewed offline (SHA-256 5DDF94F6ED0040262AB7E3FD34F8D7A1813F4AC7969692E8213129F877538080). Its notification-related operations add public ID columns and indexes, nullable owner/reference columns; no forward DROP, delete, data update, or column alteration was found. Down operations would remove those newly added columns/indexes and therefore could discard values written after deployment. No migration was applied because the exact live Development database identity/history and required independent backup could not be verified from this execution environment.

## Verification — 2026-11-06

| Check | Result |
|---|---|
| EF model gate | Passed; model synchronized; 13 repository migration IDs; no live database connection was opened. |
| API build | Passed, 0 errors, 356 warnings. Warnings include existing IKVM compatibility and offline NuGet vulnerability-feed (NU1900) warnings. |
| Persistence design-time build | Passed, 0 errors; offline NuGet warnings remain. |
| API test-project build | Passed, 0 errors, 370 warnings. |
| Focused backend tests | Test project compiles, but VSTest could not connect to testhost after 90 seconds; a single-test retry with a 180-second timeout also failed before any test started. No test is reported as passed. |
| React typecheck and production build | Passed. Production build emitted existing chunk and import warnings. |
| Focused React lint and localization load | Passed with 0 lint errors and 3 existing hooks warnings; EN/AR JSON load check passed. |
| React Vitest | Not run; automatic review blocked the telemetry egress. No security review or telemetry control was bypassed. |
| SQL Server identity, service, Windows Authentication, migration history, physical schema, or persistence tests | Not verified; managed sandbox could not access the Windows host/SQL endpoint. |
| Independent backup | Not created or verified because the database identity and approved destination could not be checked. |
| Migration application or other database write | None. |
| Browser UAT | Not run. |

The agent attempted the authorized Windows execution route but the managed execution environment denied SQL TCP access (Access is denied) and Service Control Manager access. The computer-use route was stopped by the user's physical Escape key; it was not called again. No repeated SQL retries, ACL changes, credential invention, manual SQL, or alternate security bypass was used.

## Current closure

Source changes cover the date-only contract, effective Project/Organization/Tenant calendar resolution, localized selector, authorization, owner fan-out, idempotency, retry, and navigation. Runtime delivery, physical schema, database migration history, backup evidence, and focused test execution remain unverified. Do not treat this feature as deployed to the Development database until those gates can be completed in the authorized Windows environment.

## Development migration recheck — 2026-11-06

The live Development target was verified at that time as `Shady/NexusPmo` (database ID 5). `20261105184921_AddRiskActionPublicIdForFollowUp` and `20261105201430_AddRiskActionSystemUserOwners` were pending as migrations 11–12 of 13; live history ended at `20261102190318_AddStableSystemRoleIdentity`. The reviewed forward SQL adds a Risk Action public ID with a filtered unique index and nullable Risk/Risk Action system-user owner IDs. No migration was applied. The only host volume was C:, also holding the database files, so an independent backup could not be verified under the then-current policy. Sixteen additional current-model physical differences remained on Integration and Program tables. The [Development execution record](../development/ONE-PASS-DEV-EXECUTION-20261106.md) has the historical identity, history, SQL review, backup, and build/test evidence. The missing backup is no longer a Development/UAT blocker; physical schema differences, target re-verification, and feature gaps remain open.
