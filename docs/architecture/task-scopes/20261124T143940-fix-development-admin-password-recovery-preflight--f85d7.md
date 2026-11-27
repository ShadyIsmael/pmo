# UPI Task Context Manifest

Task: **Fix Development Admin password recovery preflight failure; diagnose why PowerShell recovery exits before requesting secure passwords, verify runtime publish, child process startup, SQL target and migration readiness, then repair and test the safe recovery flow.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261124T143831-c08b56f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Integration/DevelopmentWbsActivityOrderSqlServerPersistenceTests.cs` | 1-468 | 7183 | filename-match:development, filename-match:sql, primary-module, symbol-match:DevelopmentWbsActivityOrderSqlServerPersistenceTests |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectWorkflowConfigurationConfiguration.cs` | 1-25 | 467 | filename-match:flow, primary-module, symbol-match:ProjectWorkflowConfigurationConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/WorkflowDefinitionConfiguration.cs` | 7-26, 28-45, 47-65, 67-94, 96-108 | 1746 | filename-match:flow, primary-module, symbol-match:WorkflowDefinitionConfiguration |
| `migration_script.sql` | 1-9 | 84 | filename-match:migration, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; VersionNumber)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/ProjectWorkflowConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; ProjectWorkflowConfiguration)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/ProjectWorkflowConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinition.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowDefinition)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinition.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinitionVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDomain.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowInstance.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowState.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransition.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransitionAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransitionGuard.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransitionHistory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/web-react/src/features/workflow/api/workflows.ts` — REFERENCES_TYPE (inferred-token-match; ProjectWorkflowConfiguration)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinitionVersion.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowDefinitionVersion)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDomain.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowDomains)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowInstance.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowInstance)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowState.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowState)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransition.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowTransition)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransitionAction.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowTransitionAction)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9480** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
