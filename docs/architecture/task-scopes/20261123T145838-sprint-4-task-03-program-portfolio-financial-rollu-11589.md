# UPI Task Context Manifest

Task: **Sprint 4 Task 03 Program & Portfolio Financial Rollups: identify Program and Portfolio entities, Project hierarchy and ownership paths, Financial Reporting DTOs and Calculator, existing Program/Portfolio reporting services, API endpoints, frontend components and hooks**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261123T145702-f17777c`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tools/NEXUS.EFDatabaseGate/Program.cs` | 103-104 | 66 | filename-match:program, module-mentioned, primary-module, symbol-match:VerifyExistingBackupAsync |
| `apps/api/tools/NEXUS.Persistence.DesignTime/Program.cs` | 1-10 | 86 | filename-match:program, module-mentioned, primary-module, symbol-match:Program |
| `apps/web/src/lib/api/endpoints.ts` | 1-144 | 5634 | filename-match:endpoints, module-mentioned, primary-module |
| `apps/web/src/lib/api/costs.ts` | 182-196, 198-213, 215-218, 269-276 | 380 | module-mentioned, primary-module, symbol-match:CurrencyFinancialSummary |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — REFERENCES_TYPE (inferred-token-match; Permission)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — REFERENCES_TYPE (inferred-token-match; Baseline)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/Plan.cs` — REFERENCES_TYPE (inferred-token-match; Plan)
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
- `apps/api/src/NEXUS.Persistence/MeetingPublicIdLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/MeetingReminderScheduler.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/ProjectOrganizationLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs` — REFERENCES_TYPE (inferred-token-match; SeedDataProvider)
- `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemAssigneeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemAttachmentStorage.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemCommentAccess.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemCommentAuthorLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemStatusReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Persistence/WorkItemStatusUsageLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **6166** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
