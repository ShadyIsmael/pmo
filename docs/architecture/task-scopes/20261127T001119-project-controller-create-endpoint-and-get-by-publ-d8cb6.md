# UPI Task Context Manifest

Task: **ProjectController create endpoint and get by public ID, ProjectService CreateAsync and GetByIdAsync, ProjectDto mapping for create and retrieval, project creation test**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Documents/EfTemplateDeletionReferenceValidator.cs` | 1-133 | 1881 | filename-match:id, primary-module |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionReferenceValidator.cs` | 1-23 | 251 | filename-match:id, primary-module |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionRequestController.cs` | 1-70 | 803 | filename-match:controller, primary-module, symbol-match:TemplateDeletionRequestController |
| `.recovery-runtime-launcher-1254e794d5d84794ad53b60d951f39c1/NEXUS.Api.staticwebassets.endpoints.json` | 1-1 | 16 | filename-match:endpoint, primary-module |
| `.recovery-runtime-verify-f30f8bffcdac4a339cc761761ac7a22c/publish-serialized/NEXUS.Api.staticwebassets.endpoints.json` | 1-1 | 16 | filename-match:endpoint, primary-module |
| `apps/web/src/lib/api/endpoints.ts` | 1-144 | 5634 | filename-match:endpoint, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Unit/HandoverRequestControllerTests.cs` | 1-63 | 805 | filename-match:controller, primary-module, symbol-match:HandoverRequestControllerTests |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionStorageCleanupProcessor.cs` | 5-5, 7-7 | 55 | primary-module, symbol-match:ProcessRequestAsync |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — REFERENCES_TYPE (inferred-token-match; Document)
- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — REFERENCES_TYPE (inferred-token-match; DocumentReview)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
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

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9461** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
