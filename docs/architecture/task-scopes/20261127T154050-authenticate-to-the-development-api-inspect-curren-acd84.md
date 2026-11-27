# UPI Task Context Manifest

Task: **Authenticate to the Development API, inspect current user Organization memberships and effective role scope, and call Project.Create contextual Portfolio and Program lookup endpoints**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261127T141352-618e703`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Tenant/CurrentTenantController.cs` | 1-34 | 317 | filename-match:current, module-mentioned, primary-module, symbol-match:CurrentTenantController |
| `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationController.cs` | 1-170 | 2185 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationController |
| `apps/api/src/NEXUS.Modules.Organization/Api/OrganizationalUnitController.cs` | 1-130 | 2124 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitController |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` | 1-10 | 185 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationService |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationalUnitService.cs` | 1-24 | 744 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationalUnitService |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` | 5-5, 20-53, 55-62, 64-64 | 320 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationProfileDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationSettingsAccessContracts.cs` | 3-3, 5-5, 15-21, 23-26, 28-37 | 205 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrantTargetUserDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitDtos.cs` | 3-21, 23-27, 29-37, 39-47, 49-56 | 558 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitDto |
| `apps/api/src/NEXUS.Modules.Organization/Domain/Lookup.cs` | 1-17 | 171 | filename-match:lookup, module-mentioned, primary-module, symbol-match:Lookup |
| `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` | 1-21 | 219 | filename-match:organization, module-mentioned, primary-module, symbol-match:Organization |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationSettingsGrant.cs` | 6-12 | 66 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrant |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Organization/Domain/BusinessUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ConfigurationSetting.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/CostCentre.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Department.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Holiday.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Location.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationalUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ResourceOrganizationalUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ResourceReportingLine.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Team.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/TeamMember.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/WorkingCalendar.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/WorkingCalendarDayRule.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Persistence/AppDbContext.cs` — REFERENCES_TYPE (inferred-token-match; AppDbContext)
- `apps/api/src/NEXUS.Persistence/AppDbContext.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/ApprovedTemplateCatalog.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/ConfiguredStandardWorkingCalendarInitializer.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/EfDocumentCategoryReferenceValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/EfProjectPhaseGateConfigurationReader.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/EfProjectPhaseGatePhasePairCatalog.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/EmailNotificationProfileStore.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/FiscalYearConfigurationChangeGuard.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/MeetingMinutesAttachmentPolicy.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/MeetingMinutesAttachmentStorage.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/MeetingNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **7094** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
