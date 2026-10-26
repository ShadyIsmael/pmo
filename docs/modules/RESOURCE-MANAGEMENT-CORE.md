# Resource Management Core

## Overview
The Resource Management core module manages the resource registry, skills taxonomy, vendor directory, contracts, and rate cards. It provides the foundational entities and API endpoints for all resource-related operations including allocation and capacity planning (see [RESOURCE-CAPACITY-AND-ALLOCATION.md](./RESOURCE-CAPACITY-AND-ALLOCATION.md)).

## Domain Entities

| Entity | Description | Key Fields |
|--------|-------------|------------|
| `Resource` | A person (staff member or vendor-supplied resource) who can be allocated to projects | Name, Type (`Staff`/`Vendor` API enum), Status, Seniority, DepartmentId, LocationId, UtilizationTarget, Skills |
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
| POST | `/` | Resource.Manage | Create resource |
| PUT | `/{id}` | Resource.Manage | Update resource |
| DELETE | `/{id}` | Resource.Manage | Soft-delete resource |

`ResourceType` is an API enum (`Staff`, `Vendor`), not a database lookup. JSON uses enum names. The enum metadata endpoint supplies localized labels and form metadata from the same enum. For backward compatibility, `Vendor` maps to the existing persisted `VendorResource` code; no database column, lookup row, migration, or stored data rewrite is introduced. Reads normalize known legacy values to the API enum names, while unknown historical strings are passed through for review instead of being coerced to another type. Resource create/update validation accepts only defined enum values. Resource type filters accept both the API code and the historical persisted code.

### Skills (`/api/skills`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list with search |
| GET | `/{id}` | Resource.View | Single skill |
| POST | `/` | Resource.Manage | Create skill |
| PUT | `/{id}` | Resource.Manage | Update skill |
| DELETE | `/{id}` | Resource.Manage | Soft-delete skill |

### Vendors (`/api/vendors`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list with search |
| GET | `/{id}` | Resource.View | Single vendor |
| POST | `/` | Resource.Manage | Create vendor |
| PUT | `/{id}` | Resource.Manage | Update vendor |
| DELETE | `/{id}` | Resource.Manage | Soft-delete vendor |

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

The registered policies allow `Resource.EditCostRate`, `Resource.ManageRates`, or `Resource.Manage` to view rates; writes allow `Resource.EditCostRate`, `Resource.ManageRates`, or `Resource.Manage`. Rate profiles are not returned in the general Resource DTO; use this separately authorized endpoint. Effective ranges are inclusive, must be valid, and cannot overlap another non-deleted profile for the same resource. Create accepts an optional `EffectiveTo`, supporting temporary/test rates.

## Authorization Policies

| Policy | Required Claims |
|--------|-----------------|
| `Resource.View` | `Resource.View` OR `Resource.Allocate` OR `Resource.Manage` |
| `Resource.Assign` | `Resource.Allocate` OR `Resource.Manage` |
| `Resource.Manage` | `Resource.Manage` |
| `Resource.ViewCostRate` | `Resource.ViewCostRate` OR `Resource.ManageRates` OR `Resource.Manage` |
| `Resource.EditCostRate` | `Resource.EditCostRate` OR `Resource.ManageRates` OR `Resource.Manage` |

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
Resource management has legacy English-only areas. The React cost-profile section and side sheet use the active EN/AR message catalogs, including rate periods, validation, loading, and error states.

## Resource form and persisted field limits

The React/Vite create and edit forms omit `EmployeeId` and `EmploymentType`. The form does not own either HR field, and `EmploymentType` has no operational consumer in the resource directory. The entity, database columns, API DTO properties, and directory-integration support remain for compatibility. Create requests omit both properties; update requests omit them so existing values are preserved.

The truncation root cause was that FluentValidation rules for some Resource fields existed, but the API write path did not register and execute those validators. An overlong value could therefore reach SQL Server and fail at the column boundary. The Resource table keeps its existing column sizes; no schema migration or data rewrite is required. Resource create and update requests are now checked at the controller boundary and again in `ResourceService` before persistence, using the same limits as the EF configuration: Name 200, EmployeeId 50, JobTitle 200, Email 256, Phone 50, AvatarUrl 500, Type 50, Status 50, EmploymentType 50, Seniority 50, Timezone 50, and Notes 2000 characters. Invalid values return field-keyed HTTP 400 validation errors before a database write. The React form also applies the matching limits for fields it exposes.

Added tests cover form payload omission and field limits, enum validation and legacy-code mapping, preservation of omitted legacy fields, controller validation responses, and Arabic RTL labels. The Resource Management API project builds successfully, and the focused React tests pass. The API xUnit project could not run: a transitive ProjectPlanning reference invokes IKVM's `MavenReferenceItemImport` task, and this environment cannot create its .NET x64 task host (`MSB4216`), even when project-reference builds are skipped. The live app and tenant database were not used for write verification; no database migration or seed change was made. Rebuild the running Vite/API processes from this source and validate against the Development tenant before considering the runtime check complete.
