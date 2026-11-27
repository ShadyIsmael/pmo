# UPI Task Context Manifest

Task: **scripts/database/ef-database-gate.ps1 Development database environment identity and EF migration verification/apply commands; NEXUS.Api startup project; NEXUS.Persistence migration assembly; connection string settings.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `scripts/database/preflight-multi-tenant-configuration-settings.sql` | 1-229 | 2934 | filename-match:ef, filename-match:settings, module-mentioned, path-match:database, path-match:scripts, primary-module |
| `scripts/database/FreshNexusPmo-PlanningBaselineRepair-20261101.sql` | 1-175 | 2519 | filename-match:nexus, module-mentioned, path-match:database, path-match:scripts, primary-module |
| `scripts/database/pending-migration-20261027202734.sql` | 1-45 | 394 | filename-match:migration, module-mentioned, path-match:database, path-match:scripts, primary-module |
| `scripts/database/pending-migration-20261030161223.sql` | 1-21 | 410 | filename-match:migration, module-mentioned, path-match:database, path-match:scripts, primary-module |
| `scripts/database/pending-migration-20261101114229.sql` | 1-101 | 1376 | filename-match:migration, module-mentioned, path-match:database, path-match:scripts, primary-module |
| `apps/api/src/NEXUS.Persistence/EfProjectPhaseGateConfigurationReader.cs` | 1-122 | 1440 | filename-match:ef, filename-match:gate, module-mentioned, primary-module, symbol-match:EfProjectPhaseGateConfigurationReader |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:gate, filename-match:persistence, module-mentioned, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `migration_script.sql` | 1-9 | 84 | filename-match:migration, module-mentioned, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Organization/Domain/BusinessUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ConfigurationSetting.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/CostCentre.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Department.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Holiday.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Location.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Lookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationPhaseGateConfiguration)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationSettingsGrant.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationalUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ResourceOrganizationalUnit.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/ResourceReportingLine.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Team.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/TeamMember.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/WorkingCalendar.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/WorkingCalendarDayRule.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/BaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/CostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IMilestoneService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IPaymentObligationProjectLookupService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectChangeRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectHandoverRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9392** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
