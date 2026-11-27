# UPI Task Context Manifest

Task: **Portfolio management: Portfolio entity and PortfolioService Create require a valid Organization within creator eligible memberships; Portfolio API and React CreatePortfolioForm select eligible Organizations, auto-select only when exactly one; ProgramService Create inherits Portfolio Organization; ProjectService resolve direct Portfolio or Program hierarchy Organization for project Pre-Initiation. Keep contextual portfolio/program lookup authorized with Project.Create only.**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261127T141352-618e703`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` | 1-10 | 185 | filename-match:organization, filename-match:organizations, module-mentioned, primary-module, symbol-match:IOrganizationService |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationService.cs` | 1-267 | 4184 | filename-match:organization, filename-match:organizations, module-mentioned, primary-module, symbol-match:OrganizationService |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessContracts.cs` | 3-3, 5-5, 15-21, 23-26, 28-37 | 205 | filename-match:organization, filename-match:organizations, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrantTargetUserDto |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationSettingsGrant.cs` | 6-12 | 66 | filename-match:organization, filename-match:organizations, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrant |
| `apps/api/tests/NEXUS.Api.Tests/Integration/OrganizationalUnitManagementTests.cs` | 1-144 | 1913 | filename-match:management, filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitManagementTests |
| `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationController.cs` | 1-170 | 2185 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationController |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationalUnitService.cs` | 1-24 | 744 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationalUnitService |
| `apps/web-react/src/test/organization-module-tabs.test.tsx` | 69-73 | 90 | filename-match:organization, module-mentioned, primary-module, symbol-match:renderOrganization |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/RiskManagement/RiskActionFollowUpScheduleResolver.cs` — REFERENCES_TYPE (inferred-token-match; Invalid)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Organization/Api/PhaseGateRequirementsController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/organizations)
- `apps/api/src/NEXUS.Modules.Organization/Application/BusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/CurrencyService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/DepartmentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/IBusinessUnitService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/ITenantProvisioningService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationProfileDto)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationResponseDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Application)
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
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9572** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
