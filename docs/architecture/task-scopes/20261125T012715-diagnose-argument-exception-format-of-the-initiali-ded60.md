# UPI Task Context Manifest

Task: **Diagnose ArgumentException 'Format of the initialization string does not conform to specification starting at index 0' at Reset-DevelopmentDatabase.ps1 line 170; trace connection string from secure user input through launcher to ef-database-gate.ps1 EF Database Gate connection-string parsing**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261124T184141-a585b4b`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/EfProjectPhaseGateConfigurationReader.cs` | 1-122 | 1440 | filename-match:ef, filename-match:gate, primary-module, symbol-match:EfProjectPhaseGateConfigurationReader |
| `apps/api/src/NEXUS.Persistence/EfProjectPhaseGatePhasePairCatalog.cs` | 1-61 | 778 | filename-match:ef, filename-match:gate, primary-module, symbol-match:EfProjectPhaseGatePhasePairCatalog |
| `scripts/database/preflight-multi-tenant-configuration-settings.sql` | 1-229 | 2934 | filename-match:ef, path-match:database, primary-module |
| `apps/api/src/NEXUS.Persistence/Configurations/OrganizationPhaseGateConfigurationMapping.cs` | 1-170 | 2790 | filename-match:gate, primary-module, symbol-match:OrganizationPhaseGateConfigurationMapping |
| `apps/api/src/NEXUS.Persistence/Migrations/20261112171603_AddProjectPhaseGateEvidenceCompliance.cs` | 1-86 | 808 | filename-match:gate, primary-module, symbol-match:AddProjectPhaseGateEvidenceCompliance |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:gate, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `apps/api/src/NEXUS.Persistence/Configurations/PlanningTablePreferenceConfiguration.cs` | 1-21 | 305 | filename-match:ef, primary-module |
| `apps/api/src/NEXUS.Persistence/Configurations/ResourceReportingLineConfiguration.cs` | 1-21 | 297 | filename-match:line, primary-module, symbol-match:ResourceReportingLineConfiguration |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentVersion.cs` — REFERENCES_TYPE (inferred-token-match; DocumentVersion)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/TemplateDeletionRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
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
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/CalendarPolicyConstants.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Deliverable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9587** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
