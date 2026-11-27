# UPI Task Context Manifest

Task: **Sprint 5 Task 01 Cash Flow Foundation: authoritative project cash-flow API/calculator for planned payment installments, approved unpaid payment request balances, confirmed settlements by settlement date, executed refund/correction net adjustments, currency-aware day/month buckets, quality metadata, project authorization, anti-double-counting and focused tests.**
Primary module: **permissions** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` | 1-72 | 1566 | filename-match:flow, path-match:authorization, primary-module, symbol-match:WorkflowLookupPolicyRegistration |
| `apps/api/src/NEXUS.Api/Authorization/ApiAuthorizationDefaults.cs` | 10-15 | 59 | filename-match:authorization, primary-module, symbol-match:ApiAuthorizationDefaults |
| `apps/api/tests/NEXUS.Api.Tests/Security/PlatformSettingsAuthorizationTests.cs` | 1-42 | 474 | filename-match:authorization, primary-module, symbol-match:PlatformSettingsAuthorizationTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/TenantRuntimeSettingsAuthorizationTests.cs` | 1-93 | 1249 | filename-match:authorization, primary-module, symbol-match:TenantRuntimeSettingsAuthorizationTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/PermissionCatalogArchitectureTests.cs` | 223-237, 602-612 | 473 | primary-module, symbol-match:Protected_admin_baseline_contains_approved_portfolio_program_and_handover_keys_but_not_cross_portfolio_scope |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligation)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/Meeting.cs` — REFERENCES_TYPE (inferred-token-match; Meeting)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Portfolio.cs` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Program.cs` — REFERENCES_TYPE (inferred-token-match; Program)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/BaselineController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/HandoverRequestController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/MilestoneController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/PaymentObligationProjectLookupController.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligationProjectLookupController)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/PaymentObligationProjectLookupController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectBaselineController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectChangeRequestController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectLifecycleCatalogController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectPeopleController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectPhaseGateController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectStakeholderController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/StakeholderInvitationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/StakeholderProjectController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/TimeEntryController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/TimerController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Api)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/Resource.cs` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/api/src/NEXUS.Modules.RiskManagement/Domain/Risk.cs` — REFERENCES_TYPE (inferred-token-match; Risk)
- `apps/api/src/NEXUS.Modules.WorkItems/Domain/WorkItem.cs` — REFERENCES_TYPE (inferred-token-match; WorkItem)
- `apps/api/tools/NEXUS.Persistence.DesignTime/Program.cs` — REFERENCES_TYPE (inferred-token-match; Program)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `packages/shared-types/src/types/meeting.ts` — REFERENCES_TYPE (inferred-token-match; Meeting)
- `packages/shared-types/src/types/portfolio.ts` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; Project)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **3821** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
