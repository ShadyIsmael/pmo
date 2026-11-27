# UPI Task Context Manifest

Task: **Inspect exact paths apps/web-react/src/features/projects/components/CreateProjectSheet.tsx and apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs, symbols CreateProjectSheet and CreateAsync; identify any existing API test path for project creation and reload**
Primary module: **management** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj` | 1-44 | 744 | filename-match:management, filename-match:modules, filename-match:nexus, module-mentioned, path-match:apps, primary-module |
| `apps/api/src/NEXUS.Api/Management/ManagementContextAccessor.cs` | 5-9 | 60 | filename-match:management, module-mentioned, path-match:apps, path-match:src, primary-module, symbol-match:ManagementContextAccessor |
| `apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs` | 1-427 | 7502 | filename-match:management, module-mentioned, path-match:apps, path-match:src, primary-module, symbol-match:ManagementContextMiddleware |
| `apps/api/src/NEXUS.Api/Management/ManagementController.cs` | 1-28 | 294 | filename-match:management, module-mentioned, path-match:apps, path-match:src, primary-module, symbol-match:ManagementController |
| `apps/web-react/src/test/management-scope.test.ts` | 1-19 | 296 | filename-match:management, module-mentioned, path-match:apps, path-match:src, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.SharedKernel/Management/IManagementContextAccessor.cs` — IMPLEMENTS_INTERFACE (source-class-declaration; ManagementContextAccessor declares IManagementContextAccessor)
- `apps/api/src/NEXUS.SharedKernel/Management/IManagementContextAccessor.cs` — REFERENCES_TYPE (inferred-token-match; IManagementContextAccessor)
- `apps/api/src/NEXUS.SharedKernel/Management/IManagementContextAccessor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel.Management)
- `apps/api/src/NEXUS.SharedKernel/Management/IManagementScopeResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel.Management)
- `apps/api/src/NEXUS.SharedKernel/Management/ManagementPerspective.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel.Management)
- `apps/api/src/NEXUS.SharedKernel/Management/ManagementReportingRules.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel.Management)
- `apps/api/src/NEXUS.SharedKernel/Management/ManagementScope.cs` — REFERENCES_TYPE (inferred-token-match; ManagementContext)
- `apps/api/src/NEXUS.SharedKernel/Management/ManagementScope.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel.Management)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/governance/assignment-recovery)
- `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs` — REFERENCES_TYPE (inferred-token-match; ManagementContextMiddleware)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects)
- `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/auth)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ManagerHandoverService.cs` — REFERENCES_TYPE (inferred-token-match; ManagerHandoverService)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Portfolio.cs` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Program.cs` — REFERENCES_TYPE (inferred-token-match; Program)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/TimerController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/timer)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` — REFERENCES_TYPE (inferred-token-match; ProjectService)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/Resource.cs` — REFERENCES_TYPE (inferred-token-match; Resource)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **8896** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
