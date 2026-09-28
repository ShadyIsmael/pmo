# Resource Management Core

## Overview
The Resource Management core module manages the resource registry, skills taxonomy, vendor directory, contracts, and rate cards. It provides the foundational entities and API endpoints for all resource-related operations including allocation and capacity planning (see [RESOURCE-CAPACITY-AND-ALLOCATION.md](./RESOURCE-CAPACITY-AND-ALLOCATION.md)).

## Domain Entities

| Entity | Description | Key Fields |
|--------|-------------|------------|
| `Resource` | A person (employee or contractor) who can be allocated to projects | Name, Type (Employee/Contractor), Status, CostPerHour, BillableRate, Seniority, DepartmentId, LocationId, UtilizationTarget, Skills |
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
| GET | `/{id}` | Resource.View | Single resource by ID |
| POST | `/` | Resource.Manage | Create resource |
| PUT | `/{id}` | Resource.Manage | Update resource |
| DELETE | `/{id}` | Resource.Manage | Soft-delete resource |

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

## Authorization Policies

| Policy | Required Claims |
|--------|-----------------|
| `Resource.View` | `Resource.View` OR `Resource.Allocate` OR `Resource.Manage` |
| `Resource.Assign` | `Resource.Allocate` OR `Resource.Manage` |
| `Resource.Manage` | `Resource.Manage` |
| `Resource.ViewCostRate` | `Resource.ViewCostRate` OR `Resource.ManageRates` OR `Resource.Manage` (no isolated rate endpoint currently exists) |
| `Resource.EditCostRate` | `Resource.EditCostRate` OR `Resource.ManageRates` OR `Resource.Manage` (no isolated rate endpoint currently exists) |

See [permission-model.md](../architecture/permission-model.md) for the full permission model.

## Frontend Pages

| Page | Route | Description |
|------|-------|-------------|
| Resource Directory | `/resources` | Main resource list with search, tabs (All/Internal/Outsourced) |
| Outsourced Resources | `/resources/outsourced` | Vendor-supplied resource list |

## Frontend Hooks

| File | Key Hooks |
|------|-----------|
| `hooks/resources/useResources.ts` | `useResources`, `useResource`, `useCreateResource`, `useUpdateResource`, `useDeleteResource` |

## Test Status
No test project exists. No unit, integration, or E2E tests for resource management core.

## i18n Status
Resource management strings are not externalised to translation files. Hardcoded English throughout frontend pages.
