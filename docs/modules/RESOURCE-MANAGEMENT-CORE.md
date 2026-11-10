# Resource Management Core

## Overview
The Resource Management core module manages the resource registry, skills taxonomy, vendor directory, contracts, and rate cards. It provides the foundational entities and API endpoints for all resource-related operations including allocation and capacity planning (see [RESOURCE-CAPACITY-AND-ALLOCATION.md](./RESOURCE-CAPACITY-AND-ALLOCATION.md)).

## Domain Entities

| Entity | Description | Key Fields |
|--------|-------------|------------|
| `Resource` | A professional person profile that can be allocated to projects and may or may not have a login | Name, JobTitle, Email, Phone, Type (`Staff`/`Outsourced`/`Vendor` API enum), Status, availability, effective-dated cost profiles, Skills, optional linked UserId |
| `ResourceCostProfile` | Tenant-scoped, effective-dated confidential resource cost rate | ResourceId, EffectiveFrom, EffectiveTo, CompensationType, Amount, Currency, overtime options |
| `Skill` | A named, categorised skill used in resource matching | Name, Category, Description |
| `ResourceSkill` | Many-to-many join between Resource and Skill with proficiency | Proficiency, YearsOfExperience |
| `Vendor` | An external supplier of contracted resources | Name, ContactName, ContactEmail, ContactPhone, Category, Status |
| `Contract` | A contract binding a resource to a vendor or direct engagement | Type, StartDate, EndDate, Value, Currency, Terms, VendorName |
| `RateCard` | Standardised billing rates by role and seniority | Role, Seniority, HourlyRate, DailyRate, MonthlyRate, Currency, EffectiveFrom, EffectiveTo |
| `ResourceRequest` | A request for additional resource capacity | Role, Seniority, RequiredAllocation, StartDate, EndDate, Status |

## API Endpoints

### Resources (`/api/resources`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list with search, filters (status, type, department, location) |
| GET | `/reference-data` | Resource.View | Enum-derived bilingual resource types, seniority levels, and compensation options |
| GET | `/{id}` | Resource.View | Single resource by ID |
| POST | `/` | Resource.Create | Create resource |
| PUT | `/{id}` | Resource.Edit | Update resource |
| DELETE | `/{id}` | Resource.Delete | Soft-delete resource |

`ResourceType` is an API enum (`Staff`, `Outsourced`, `Vendor`), not a database lookup. JSON uses enum names. The enum metadata endpoint supplies localized labels and form metadata from the same enum. For backward compatibility, `Vendor` maps to the existing persisted `VendorResource` code; `Outsourced` has its own stored code. Reads normalize known legacy values to API enum names, while unknown historical strings are passed through for review instead of being coerced. Resource create/update validation accepts only defined enum values. Resource type filters accept API codes and the historical vendor code.

### Users, Resources, Teams, and external invitation

`User` is the login and identity record: full name, email, tenant, authentication/session state, roles, and effective permissions. Staff Resource creation and editing require one active linked User in the request tenant; the server derives the Resource name and email from that User, and Resource writes never update the User profile. The searchable resource-user lookup returns active users in the authorized tenant, excludes users linked to another Resource, and retains the current Resource's linked user while editing. Job title, phone, organizational assignment, capacity, skills, avatar, rate, and availability remain Resource-owned data; the supported User lookup does not supply a phone number. Non-Staff Resources retain their existing independent identity/contact entry and may remain unlinked. The historical User profile columns remain mapped as shadow columns so existing database values are not dropped. User administration no longer edits or emits those legacy profile fields. User lookup job title is derived from the linked Resource where needed.

`HoursPerDay` is required by the Resource Create/Edit form and by create requests, and accepts fractional values from 1 through 24 hours, inclusive. An update cannot explicitly clear the value; a partial update that omits it preserves the stored value. The capacity calculator continues to resolve calendar-derived capacity for legacy records whose stored value is null; changing those records through the form requires a valid daily-hours value.

`ResourceCostProfile` remains the source for a person's rate, currency, unit, and effective date. Availability remains derived from capacity/calendar inputs. `Team` is a reusable collection of Resources with effective-dated `TeamMember` history. The retired single-valued `Resource.TeamId` is removed from the Resource entity and API; the existing SQL column stays mapped as a shadow field and is backfilled by `20261031142036_AlignResourceProjectTeamsAndInvitations`. The old pointer carried no membership dates, so imported rows start at the migration cutover time; the system does not invent pre-cutover membership history. Project Team rows can target either one Resource or one Team; a Team assignment resolves its currently effective members dynamically. Allocation and capacity views deduplicate the same Resource across direct and Team paths within a Project.

An external invitation is a one-time, SHA-256-hashed token with a bounded expiry. Accept/decline records project participation consent; reports and notifications have separate opt-ins. Acceptance does not create a User, issue a JWT, or grant project API access. Email reports are sent only for the accepted project and tenant, to an active external stakeholder Resource with a matching address, and include detailed status content only when report consent is recorded. Existing SMTP delivery is reused.

Migration `20261031142036_AlignResourceProjectTeamsAndInvitations` adds the tenant-safe Resource-to-User uniqueness/FK, nullable ProjectMember Resource target plus Team target, Team relationship constraints, legacy Resource.TeamId membership backfill, and invitation consent/delivery fields. It marks pre-existing invitation delivery as `Untracked` rather than claiming it was sent or pending. The checked-in offline SQL is `scripts/database/20261031142036_AlignResourceProjectTeamsAndInvitations.sql`; target-specific SQL was also generated and reviewed against Development. The host-connected gate verified six applied migrations and this alignment migration as the sole pending migration. No migration was applied. Deployment remains blocked by physical-schema drift, a duplicate active ProjectMember allocation requiring safe reconciliation, and the need for current target verification and explicit migration authorization. A backup is optional for Development/UAT.

### Skills (`/api/skills`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list with search |
| GET | `/{id}` | Resource.View | Single skill |
| POST | `/` | Resource.Create | Create skill |
| PUT | `/{id}` | Resource.Edit | Update skill |
| DELETE | `/{id}` | Resource.Delete | Soft-delete skill |

### Vendors (`/api/vendors`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list with search |
| GET | `/{id}` | Resource.View | Single vendor |
| POST | `/` | Resource.Create | Create vendor |
| PUT | `/{id}` | Resource.Edit | Update vendor |
| DELETE | `/{id}` | Resource.Delete | Soft-delete vendor |

### Resource Finder (`/api/resource-finder`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| POST | `/find` | Resource.View | Search resources by role, skills, seniority, availability with match scoring |

### Resource Cost Profiles (`/api/resources/{resourceId}/cost-profiles`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.ViewCostRate | List effective-dated cost profiles for a resource |
| GET | `/{id}` | Resource.ViewCostRate | Read one profile belonging to the route resource |
| POST | `/` | Resource.EditCostRate | Create a profile; resource tenant is authoritative |
| PUT | `/{id}` | Resource.EditCostRate | Update a profile belonging to the route resource |
| DELETE | `/{id}` | Resource.EditCostRate | Soft-delete a profile belonging to the route resource |

The registered policies require `Resource.ViewCostRate` to view rates and `Resource.EditCostRate` to write them. `Resource.EditCostRate` depends on `Resource.ViewCostRate`. Rate profiles are not returned in the general Resource DTO; use this separately authorized endpoint. Effective ranges are inclusive, must be valid, and cannot overlap another non-deleted profile for the same resource. Create accepts an optional `EffectiveTo`, supporting temporary/test rates.

## Authorization Policies

| Policy | Required Claims |
|--------|-----------------|
| `Resource.View` | `Resource.View` |
| `Resource.Create` | `Resource.Create` |
| `Resource.Edit` | `Resource.Edit` |
| `Resource.Delete` | `Resource.Delete` |
| `Resource.Assign` | `Resource.Assign` |
| `Resource.ViewCostRate` | `Resource.ViewCostRate` |
| `Resource.EditCostRate` | `Resource.EditCostRate` and its catalog dependency `Resource.ViewCostRate` |

See [permission-model.md](../architecture/permission-model.md) for the full permission model.

## Frontend Pages

| Page | Route | Description |
|------|-------|-------------|
| Resource Directory | `/resources` | Main resource list with search, tabs (All/Internal/Outsourced) |
| Vendors | `/resources/vendors` | Vendor directory; shows vendor-specific data and filters |
| Outsourced Resources | `/resources/outsourced` | Vendor-supplied resource list |
| Vendor Categories | `/administration/reference-data/vendor-categories` | Manage vendor categories only; work item statuses and currencies remain separate destinations |

## Frontend Hooks

| File | Key Hooks |
|------|-----------|
| `hooks/resources/useResources.ts` | `useResources`, `useResource`, `useCreateResource`, `useUpdateResource`, `useDeleteResource` |

The React resource detail side sheet opens cost profiles only for users with cost-rate read access. Creating a profile additionally requires edit-rate permission and a mutable management context. The rates are fetched from the dedicated endpoint and are not included in general resource detail payloads.

## Test Status
Resource core tests exist in the API module; the React directory has focused component tests. Cost-profile service tests cover tenant stamping, end dates, range overlap, and route-resource ownership. The React cost-profile flow is covered by the resource-directory parity tests.

## i18n Status
The React Resource directory, project-team target selection, and external invitation consent flow use the active English and Arabic catalogs. Verify both directions and RTL during consolidated UAT.

## Resource form and persisted field limits

### Create/edit capacity and Staff identity — 2026-11-05

Create and edit forms require `HoursPerDay` from 1 through 24 hours, inclusive, with fractional hours supported. Empty, nonnumeric, below-1, and above-24 input is rejected by the frontend and API validators; updates cannot clear the value. The working-capacity calculator applies the same range to configured values while preserving its calendar-derived fallback for legacy Resources whose stored `HoursPerDay` is null. When both daily and weekly hours are configured, the existing calendar consistency check still applies. API regression cases cover empty input, invalid bounds, accepted boundaries, and a valid fraction; calculator cases also assert null legacy fallback.

The previous form accepted any positive daily value and used JavaScript `Number()` directly, so values below one were considered valid and localized Arabic digits/decimal separators were not normalized consistently between validation and request mapping. The shared locale-aware parser and both request mappers are in `apps/web-react/src/features/resources/resource-form.ts`; `ResourceFormSections.tsx` now exposes a required decimal text input. The API DTO validators and `ResourceWorkingCapacityCalculator` enforce the same inclusive range. Regression coverage is in `apps/web-react/src/test/resource-form.test.ts`, `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/ResourceInputValidationTests.cs`, and `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/ResourceWorkingCapacityCalculatorTests.cs`.

The previous Staff form always required an editable Resource name and exposed a non-searchable User select with no email context. Staff create/edit now selects an active same-tenant User, uses that User's full name and email as authoritative identity, and leaves phone and other professional/organizational fields on the Resource. Editing a Resource does not write to the User profile. The existing Resource↔User uniqueness and tenant checks remain the duplicate/cross-tenant protection; no new permission or schema change is introduced. The lookup contract is implemented in `apps/api/src/NEXUS.Modules.Identity/Api/UsersController.cs`; server-side Staff identity and link validation are in `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ResourceService.cs`; the searchable picker is in `apps/web-react/src/features/resources/ResourceFormSections.tsx`; current Resource id scoping is passed through `apps/web-react/src/features/resources/ResourcesListPage.tsx` and `apps/web-react/src/features/projects/lib/project-lookups.ts`. The picker, payload, and query regressions are covered by `apps/web-react/src/test/resources-directory-parity.test.tsx` and `apps/web-react/src/test/project-workflow-lookups.test.tsx`; Staff service cases are in `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/ResourceInputValidationTests.cs`.

**P1 verification (2026-11-05):** Focused Resource API regressions pass 46/46, including persisted create and update values at 1, 24, and 1.25 hours/day. The three focused React suites pass 30/30; web typecheck, React production build, and independent Resource Management and Identity module builds pass. A reference-rebuilding API test attempt encounters unrelated compile errors in the pre-existing modified `NEXUS.Modules.CostManagement/Application/ProjectFinancialLookup.cs`; focused tests were run with project-reference rebuilding disabled after the affected API modules built independently. No live database or browser UAT was performed, and no migration, SQL, seed, permission, or grant change was made.

### Resource default calendar and validation — 2026-11-05

Create/Edit retain the existing Base Calendar selector. The API validates and preserves explicit tenant calendars; when the field is empty, it resolves the single valid tenant default for capacity validation and keeps `BaseCalendarId` empty so project-calendar inheritance remains available. No Organization-level `WorkingCalendar` setting exists, and no calendar schedule or five-day week is invented. Daily-only resources remain saveable when no default calendar exists; comparing both daily and weekly hours requires a valid effective calendar.

Calendar access/configuration failures and actual capacity conflicts now have distinct stable codes and field locations. The API returns field-keyed ProblemDetails, and React localizes calendar, daily-hours, weekly-hours, and mismatch messages in English and Arabic. Focused API tests pass 33/33 and three focused React suites pass 36/36; the Resource Management module and API host compile successfully. The rebuilt Development API on localhost:5000 returned HTTP 200 from `/health` after startup database initialization was skipped. Full React typecheck remains blocked by an unrelated `EOF` identifier at `src/test/p0-05-frontend-quality.test.tsx:15`. No authenticated Resource Create/Edit request or database-backed runtime flow was performed, and no database reset, reseed, migration, direct SQL, permission bypass, or browser UAT was performed.

**P1 files updated:**

- Frontend: `apps/web-react/src/features/resources/resource-form.ts`, `ResourceFormSections.tsx`, `ResourcesListPage.tsx`, `apps/web-react/src/features/projects/lib/project-lookups.ts`, `apps/web-react/src/i18n/messages/en.json`, `apps/web-react/src/i18n/messages/ar.json`.
- Identity and Resource API: `apps/api/src/NEXUS.Modules.Identity/Api/UsersController.cs`, `apps/api/src/NEXUS.Modules.Identity/Application/UserLookupDto.cs`, `apps/api/src/NEXUS.Modules.ResourceManagement/Application/Validators/CreateResourceValidator.cs`, `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ResourceService.cs`, `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ResourceWorkingCapacityCalculator.cs`.
- Tests: `apps/web-react/src/test/resource-form.test.ts`, `apps/web-react/src/test/resources-directory-parity.test.tsx`, `apps/web-react/src/test/project-workflow-lookups.test.tsx`, `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/ResourceInputValidationTests.cs`, `ResourceManagementPhase1Tests.cs`, `ResourceWorkingCapacityCalculatorTests.cs`, `TestDbContext.cs`.
- Documentation: `docs/modules/RESOURCE-MANAGEMENT-CORE.md`, `docs/modules/RESOURCE-CAPACITY-AND-ALLOCATION.md`.
The React/Vite create and edit forms omit `EmployeeId` and `EmploymentType`. The form does not own either HR field, and `EmploymentType` has no operational consumer in the resource directory. The entity, database columns, API DTO properties, and directory-integration support remain for compatibility. Create requests omit both properties; update requests omit them so existing values are preserved.

The truncation root cause was that FluentValidation rules for some Resource fields existed, but the API write path did not register and execute those validators. An overlong value could therefore reach SQL Server and fail at the column boundary. The Resource table keeps its existing column sizes; no schema migration or data rewrite is required. Resource create and update requests are now checked at the controller boundary and again in `ResourceService` before persistence, using the same limits as the EF configuration: Name 200, EmployeeId 50, JobTitle 200, Email 256, Phone 50, AvatarUrl 500, Type 50, Status 50, EmploymentType 50, Seniority 50, Timezone 50, and Notes 2000 characters. Invalid values return field-keyed HTTP 400 validation errors before a database write. The React form also applies the matching limits for fields it exposes.

Added tests cover form payload omission and field limits, enum validation and legacy-code mapping, preservation of omitted legacy fields, controller validation responses, and Arabic RTL labels. The Resource Management API project builds successfully, and the focused React tests pass. The API xUnit project could not run: a transitive ProjectPlanning reference invokes IKVM's `MavenReferenceItemImport` task, and this environment cannot create its .NET x64 task host (`MSB4216`), even when project-reference builds are skipped. The live app and tenant database were not used for write verification; no database migration or seed change was made. Rebuild the running Vite/API processes from this source and validate against the Development tenant before considering the runtime check complete.

## Users–Resources–Teams alignment verification — 2026-10-31

The P0 ownership and invitation implementation, permission matrix, migration/preflight behavior, SQL review, build and focused test results are recorded in [Project Management Core](PROJECT-MANAGEMENT-CORE.md#users-resources-teams-and-project-team--2026-10-31); the host-connected database inspection is recorded in the [EF Core workflow record](../development/ef-core-database-workflow.md#host-connected-development-recheck--2026-10-31). The EF model is synchronized. A read-only Development audit found one linked User/Resource pair with different legacy `JobTitle` values; no backfill or value change was made, and Resource remains the professional-profile source. No legacy `Resources.TeamId` values or existing TeamMembers require cutover backfill. The migration remains unapplied and no business data was modified.

### Reusable Teams and Project Team allocation semantics

`TeamMember` rows are effective-dated and closed rather than deleted; `TeamService` prevents duplicate current membership and retains prior rows for history. A Project Team assignment may target one Resource or one reusable Team. A Team assignment dynamically includes Resources only while both the project assignment and the TeamMember row are effective. Its allocation percentage applies to each effective Resource individually; it is not divided across the Team. If direct and Team assignments overlap for one Resource in the same Project, capacity/conflict calculations use the highest effective dedication for that Resource once. Cross-project simultaneous allocations are still combined for the 100% guard. Team membership itself grants no system permission. Runtime and SQL Server verification remain pending Development target verification.
