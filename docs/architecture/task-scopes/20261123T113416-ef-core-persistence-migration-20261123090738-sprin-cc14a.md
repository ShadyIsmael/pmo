# UPI Task Context Manifest

Task: **EF Core persistence migration 20261123090738_Sprint3Task05BRefundExecutionGovernance in NEXUS.Persistence; AppDbContext and Development migration execution gate for Shady NexusPmo; review Sprint3Task05BRefundExecutionGovernance.sql; update docs/finance/SPRINT-3-CONSOLIDATED-CLOSURE-REPORT.md.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/Migrations/20261123090738_Sprint3Task05BRefundExecutionGovernance.cs` | 1-165 | 1849 | filename-match:20261123090738_sprint3, filename-match:brefund, filename-match:ef, filename-match:execution, filename-match:governance, filename-match:sprint, filename-match:sprint3, filename-match:task05, module-mentioned, primary-module, symbol-match:Sprint3Task05BRefundExecutionGovernance |
| `apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj` | 1-44 | 914 | filename-match:nexus, filename-match:persistence, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Integration/DevelopmentWbsSqlServerPersistenceTests.cs` | 1-278 | 3791 | filename-match:development, filename-match:persistence, filename-match:sql, module-mentioned, primary-module, symbol-match:DevelopmentWbsSqlServerPersistenceTests |
| `apps/api/tests/NEXUS.Api.Tests/Testing/SqliteTestAppDbContext.cs` | 1-61 | 723 | filename-match:app, filename-match:context, filename-match:db, filename-match:sql, module-mentioned, primary-module, symbol-match:SqliteTestAppDbContext |
| `apps/api/src/NEXUS.Persistence/Migrations/AppDbContextModelSnapshot.cs` | 14-14 | 18 | filename-match:app, filename-match:context, filename-match:db, module-mentioned, primary-module, symbol-match:AppDbContextModelSnapshot |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:gate, filename-match:persistence, module-mentioned, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `apps/api/src/NEXUS.Persistence/EfProjectPhaseGateConfigurationReader.cs` | 1-122 | 1440 | filename-match:ef, filename-match:gate, module-mentioned, primary-module, symbol-match:EfProjectPhaseGateConfigurationReader |
| `migration_script.sql` | 1-9 | 84 | filename-match:migration, module-mentioned, primary-module |
| `scripts/database/pending-migration-20261027202734.sql` | 1-45 | 394 | filename-match:migration, module-mentioned, primary-module |

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

- Initial selected source: **9448** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
