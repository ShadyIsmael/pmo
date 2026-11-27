# UPI Task Context Manifest

Task: **Project CreateProjectSheet payload, ProjectController create endpoint, ProjectService.CreateAsync, owning organization resolution, lifecycle phase assignment persistence, ProjectDto and ProjectMapping, project retrieval by PublicId, phase rendering after reload; end-to-end integration test**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Organization/Api/PhaseGateRequirementsController.cs` | 1-116 | 1676 | filename-match:controller, filename-match:phase, module-mentioned, primary-module, symbol-match:PhaseGateRequirementsController |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` | 9-21, 24-31, 37-47 | 485 | filename-match:organization, filename-match:phase, module-mentioned, primary-module, symbol-match:OrganizationPhaseGateConfiguration |
| `apps/web-react/src/features/organization/PhaseGateRequirementsCard.tsx` | 1-243 | 5439 | filename-match:phase, module-mentioned, path-match:organization, primary-module, symbol-match:PhaseGateRequirement |
| `apps/web-react/src/features/organization/api/phase-gate-requirements.ts` | 1-153 | 1834 | filename-match:phase, module-mentioned, path-match:organization, primary-module, symbol-match:PhaseGateRequirementType |
| `apps/api/src/NEXUS.Modules.Organization/Application/TenantRegistryStatusValidator.cs` | 13-18 | 103 | filename-match:id, module-mentioned, primary-module, symbol-match:IsActiveAsync |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/organizations)
- `apps/api/src/NEXUS.Modules.Organization/Application/BusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Application/CurrencyService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Application/DepartmentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/IBusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationalUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/ITenantProvisioningService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationResponseDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationContracts.cs` — REFERENCES_TYPE (inferred-token-match; IPhaseGateConfigurationService)
- `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/TenantProvisioningContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/TenantProvisioningService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/WorkItemStatusReferenceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Modules.Organization/Domain/BusinessUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ConfigurationSetting.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/CostCentre.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Department.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Holiday.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9537** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
