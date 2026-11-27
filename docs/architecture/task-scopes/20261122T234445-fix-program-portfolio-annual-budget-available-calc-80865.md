# UPI Task Context Manifest

Task: **Fix Program Portfolio annual budget available calculations to reserve pending payment request allocations**
Primary module: **resources** (task-explicit-alias)
Architecture snapshot: `20261122T234254-6f2367f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ResourceManagement/Application/AllocationService.cs` | 1-331 | 4640 | filename-match:allocations, primary-module, symbol-match:AllocationService |
| `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IAllocationService.cs` | 1-17 | 416 | filename-match:allocations, primary-module, symbol-match:IAllocationService |
| `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/ResourceRequest.cs` | 1-19 | 212 | filename-match:request, primary-module, symbol-match:ResourceRequest |
| `apps/web-react/src/features/resources/api/allocations.ts` | 85-92, 103-110, 112-119, 130-132 | 367 | filename-match:allocations, primary-module, symbol-match:useAllocations |
| `apps/api/src/NEXUS.Modules.ResourceManagement/Api/ResourceController.cs` | 130-131 | 34 | primary-module, symbol-match:BadRequest |
| `apps/api/src/NEXUS.Modules.ResourceManagement/Application/CapacityService.cs` | 909-924 | 175 | primary-module, symbol-match:CalculateEarliestAvailableDate |
| `apps/api/src/NEXUS.Modules.ResourceManagement/Application/TeamService.cs` | 569-580 | 180 | primary-module, symbol-match:DetachPendingCreate |
| `packages/shared-types/src/types/resource.ts` | 64-77 | 96 | primary-module, symbol-match:ResourceRequest |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Api/ResourceCostProfileController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/resources)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/CostUtilizationAnalyticsService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/AllocationDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/CapacityDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/CostUtilizationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ProjectAllocationOverviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ResourceCostProfileDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ResourceDto.cs` — REFERENCES_TYPE (inferred-token-match; CreateResourceDto)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ResourceDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ResourceFinderDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/ResourceVendorLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/SkillDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/TeamDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/DTOs/VendorDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ICapacityService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ICostUtilizationAnalyticsService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IProjectRoleService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IResourceCostProfileService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IResourceFinderService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IResourceService.cs` — REFERENCES_TYPE (inferred-token-match; IResourceService)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IResourceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ISkillService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ITeamService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IVendorCategoryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/IVendorService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ProjectEvmLaborCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ProjectRoleService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Application/ResourceCostProfileService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ResourceManagement.Application)

## Budget

- Initial selected source: **6120** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
