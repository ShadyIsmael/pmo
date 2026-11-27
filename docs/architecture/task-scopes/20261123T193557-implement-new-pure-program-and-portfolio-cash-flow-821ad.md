# UPI Task Context Manifest

Task: **Implement new pure Program and Portfolio cash-flow aggregation from per-project CashFlow DTOs, new React ProgramPortfolioCashFlowView and API client, and CashFlow-only Excel/PDF builder. Reuse existing delivery financial rollup UI patterns and ExcelJS/pdfmake-rtl exports. Do not modify existing hierarchy controller, DTO, service, delivery.ts, FinancialRollupView, locale catalogs, or route wiring.**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowContracts.cs` | 1-28 | 208 | filename-match:flow, filename-match:ts, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionWorkflowService.cs` | 1-33 | 307 | filename-match:flow, module-mentioned, primary-module, symbol-match:ITemplateDeletionWorkflowService |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowRegistration.cs` | 1-17 | 220 | filename-match:flow, module-mentioned, primary-module, symbol-match:TemplateDeletionWorkflowRegistration |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowService.cs` | 19-19 | 16 | filename-match:flow, module-mentioned, primary-module, symbol-match:TemplateDeletionWorkflowService |
| `apps/web-react/src/api/workItemWorkflow.ts` | 1-38 | 701 | filename-match:flow, module-mentioned, primary-module, symbol-match:WorkflowMoveFailure |
| `apps/api/tests/NEXUS.Api.Tests/Unit/HandoverRequestControllerTests.cs` | 1-63 | 805 | filename-match:controller, filename-match:do, filename-match:ts, module-mentioned, primary-module, symbol-match:HandoverRequestControllerTests |
| `apps/api/tools/NEXUS.EFDatabaseGate/Program.cs` | 103-104, 1350-1364 | 263 | filename-match:program, module-mentioned, primary-module, symbol-match:VerifyExistingBackupAsync |
| `apps/api/tools/NEXUS.Persistence.DesignTime/Program.cs` | 1-10 | 86 | filename-match:program, module-mentioned, primary-module, symbol-match:Program |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionRequestController.cs` | 1-70 | 803 | filename-match:controller, module-mentioned, primary-module, symbol-match:TemplateDeletionRequestController |
| `apps/web/src/lib/api/costs.ts` | 182-196, 198-213, 215-218, 269-276 | 380 | filename-match:ts, module-mentioned, primary-module, symbol-match:CurrencyFinancialSummary |
| `apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj` | 1-15 | 179 | filename-match:per, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/ApiProblemDetailsTests.cs` | 128-138, 141-163 | 473 | filename-match:ts, module-mentioned, primary-module, symbol-match:Database_diagnostics_in_legacy_client_errors_are_replaced_with_safe_detail |
| `apps/api/tests/NEXUS.Api.Tests/Security/ApiAuthorizationFallbackTests.cs` | 38-56, 59-95 | 938 | filename-match:ts, module-mentioned, primary-module, symbol-match:Auth_controller_actions_have_explicit_authorization_or_anonymous_metadata |
| `apps/web-react/src/api/api-error-localization.ts` | 4-9 | 42 | module-mentioned, primary-module, symbol-match:ApiErrorLocale |
| `apps/web-react/src/api/workItemsFull.ts` | 9-10, 19-19, 20-20, 21-21, 59-64 | 720 | module-mentioned, primary-module, symbol-match:WorkflowRowVersion |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/TemplateDeletionRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ICostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IMeetingPublicIdLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IOrganizationLifecycleHandler.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectAudienceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectFinancialLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectOrganizationLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanMilestoneLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanProgressLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectResourcePlanningContextLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IResourceReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ISoftDeletable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantRegistryStatusValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **6141** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
