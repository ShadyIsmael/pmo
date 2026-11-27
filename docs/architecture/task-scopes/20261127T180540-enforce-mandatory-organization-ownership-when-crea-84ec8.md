# UPI Task Context Manifest

Task: **Enforce mandatory Organization ownership when creating Portfolios; select automatically for one eligible Organization otherwise require selection; preserve Project.Create contextual lookups; ensure Program and Project ownership resolution; audit and prepare approval-gated remediation for Digital Transformation Portfolio; verify project creation with automatic Pre-Initiation assignment**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261127T141352-618e703`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationController.cs` | 1-170 | 2185 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationController |
| `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationalUnitController.cs` | 1-130 | 2124 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitController |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` | 1-10 | 185 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationService |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationalUnitService.cs` | 1-24 | 744 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationalUnitService |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` | 5-5, 20-53, 55-62, 64-64 | 320 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationProfileDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationResponseDtos.cs` | 11-35, 117-213 | 1437 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationResponseDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessContracts.cs` | 3-3, 5-5, 15-21, 23-26, 28-37 | 205 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrantTargetUserDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitDtos.cs` | 3-21, 23-27, 29-37, 39-47, 49-56 | 558 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitDto |
| `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` | 1-21 | 219 | filename-match:organization, module-mentioned, primary-module, symbol-match:Organization |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` | 9-21, 24-31, 37-47 | 485 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationPhaseGateConfiguration |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationSettingsGrant.cs` | 6-12 | 66 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrant |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationalUnit.cs` | 10-21 | 167 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnit |
| `apps/api/src/NEXUS.Modules.Organization/Domain/ResourceOrganizationalUnit.cs` | 6-15 | 124 | filename-match:organization, module-mentioned, primary-module, symbol-match:ResourceOrganizationalUnit |
| `apps/api/src/NEXUS.Modules.Organization/Infrastructure/OrganizationRepository.cs` | 1-30 | 288 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationRepository |
| `apps/web-react/src/features/organization/api/organization.ts` | 5-5, 6-6, 7-7, 8-8, 18-20 | 348 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationUnit |
| `packages/shared-types/src/types/organization.ts` | 3-14 | 73 | filename-match:organization, module-mentioned, primary-module, symbol-match:Organization |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/RiskManagement/RiskActionFollowUpScheduleResolver.cs` — REFERENCES_TYPE (inferred-token-match; Invalid)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Organization/Api/PhaseGateRequirementsController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/organizations)
- `apps/api/src/NEXUS.Modules.Organization/Application/BusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/CurrencyService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/DepartmentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/IBusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/ITenantProvisioningService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/ConfigurationSettingsService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/EfSettingsRepository.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/EffectiveConfigurationResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/FiscalYearCalendar.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/ISettingsRepository.cs` — REFERENCES_TYPE (inferred-token-match; SettingsPersistenceUnavailableException)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/ISettingsRepository.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/SettingDefinitions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/Settings/SettingsContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application.Settings)
- `apps/api/src/NEXUS.Modules.Organization/Application/TenantProvisioningContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/TenantProvisioningService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/TenantRegistryStatusValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/WorkItemStatusReferenceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `.recovery-runtime-launcher-1254e794d5d84794ad53b60d951f39c1/NEXUS.Api.deps.json` — REFERENCES_TYPE (inferred-token-match; Organization)
- `.recovery-runtime-verify-f30f8bffcdac4a339cc761761ac7a22c/publish-serialized/NEXUS.Api.deps.json` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` — REFERENCES_TYPE (inferred-token-match; Location)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9528** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
