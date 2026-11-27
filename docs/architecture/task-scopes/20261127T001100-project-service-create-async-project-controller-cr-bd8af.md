# UPI Task Context Manifest

Task: **ProjectService CreateAsync ProjectController create project and save database; retrieve project by public ID through GetById API**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Documents/EfTemplateDeletionReferenceValidator.cs` | 1-133 | 1881 | filename-match:id, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionReferenceValidator.cs` | 1-23 | 251 | filename-match:id, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionRequestController.cs` | 1-70 | 803 | filename-match:controller, module-mentioned, primary-module, symbol-match:TemplateDeletionRequestController |
| `apps/api/tools/NEXUS.EFDatabaseGate/NEXUS.EFDatabaseGate.csproj` | 1-12 | 123 | filename-match:database, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Unit/HandoverRequestControllerTests.cs` | 1-63 | 805 | filename-match:controller, module-mentioned, primary-module, symbol-match:HandoverRequestControllerTests |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionStorageCleanupProcessor.cs` | 5-5, 7-7 | 55 | module-mentioned, primary-module, symbol-match:ProcessRequestAsync |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionStorageCleanupProcessor.cs` | 1-227 | 2995 | module-mentioned, primary-module, symbol-match:ProcessPendingAsync |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowService.cs` | 142-147, 148-157, 351-351, 456-465, 466-476 | 753 | module-mentioned, primary-module, symbol-match:RollbackQuietlyAsync |
| `apps/api/src/NEXUS.Api/Notifications/RiskActionFollowUpWorker.cs` | 1-63 | 670 | module-mentioned, primary-module, symbol-match:ExecuteAsync |
| `apps/web-react/src/api/workItemsFull.ts` | 18-18 | 135 | module-mentioned, primary-module, symbol-match:SaveWorkItemDto |
| `apps/api/tests/NEXUS.Api.Tests/ApiProblemDetailsTests.cs` | 128-138 | 172 | module-mentioned, primary-module, symbol-match:Database_diagnostics_in_legacy_client_errors_are_replaced_with_safe_detail |
| `apps/api/tests/NEXUS.Api.Tests/Security/ApiAuthorizationFallbackTests.cs` | 38-56, 59-95 | 938 | module-mentioned, primary-module, symbol-match:Auth_controller_actions_have_explicit_authorization_or_anonymous_metadata |

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

- Initial selected source: **9581** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
