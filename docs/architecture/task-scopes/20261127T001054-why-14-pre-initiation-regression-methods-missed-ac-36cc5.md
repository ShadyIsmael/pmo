# UPI Task Context Manifest

Task: **Why 14 Pre-Initiation regression methods missed actual project creation UAT; identify integration-test architecture that exercises ProjectController create endpoint, persisted service creation, and retrieval endpoint after reload**
Primary module: **integrations** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Integrations/Api/DirectoryIntegrationController.cs` | 1-162 | 2934 | filename-match:controller, filename-match:integration, primary-module, symbol-match:DirectoryIntegrationController |
| `apps/api/src/NEXUS.Modules.Integrations/Api/IntegrationController.cs` | 1-40 | 420 | filename-match:controller, filename-match:integration, primary-module, symbol-match:IntegrationController |
| `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIntegrationException.cs` | 1-7 | 77 | filename-match:integration, primary-module, symbol-match:DirectoryIntegrationException |
| `apps/api/src/NEXUS.Modules.Integrations/Application/IDirectoryIntegrationService.cs` | 1-25 | 1007 | filename-match:integration, primary-module, symbol-match:IDirectoryIntegrationService |
| `apps/api/src/NEXUS.Modules.Integrations/Application/IIntegrationService.cs` | 1-9 | 116 | filename-match:integration, primary-module, symbol-match:IIntegrationService |
| `apps/api/src/NEXUS.Modules.Integrations/Application/IntegrationConnectionDto.cs` | 8-21, 23-33 | 304 | filename-match:integration, primary-module, symbol-match:IntegrationConnectionDto |
| `apps/api/src/NEXUS.Modules.Integrations/Application/IntegrationService.cs` | 1-109 | 1194 | filename-match:integration, primary-module, symbol-match:IntegrationService |
| `apps/api/src/NEXUS.Modules.Integrations/Domain/IntegrationConnection.cs` | 1-18 | 190 | filename-match:integration, primary-module, symbol-match:IntegrationConnection |
| `apps/api/src/NEXUS.Modules.Integrations/Domain/IntegrationSync.cs` | 1-17 | 186 | filename-match:integration, primary-module, symbol-match:IntegrationSync |
| `apps/api/src/NEXUS.Modules.Integrations/Domain/IntegrationWebhook.cs` | 1-13 | 112 | filename-match:integration, primary-module, symbol-match:IntegrationWebhook |
| `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/IntegrationRepository.cs` | 1-21 | 171 | filename-match:integration, primary-module, symbol-match:IntegrationRepository |
| `apps/api/src/NEXUS.Modules.Integrations/ModuleRegistration/IntegrationsModuleRegistration.cs` | 1-26 | 415 | filename-match:integration, primary-module, symbol-match:IntegrationsModuleRegistration |
| `packages/api-client/src/integrations.ts` | 4-15 | 137 | filename-match:integration, primary-module, symbol-match:createIntegrationsApi |
| `packages/shared-types/src/types/integration.ts` | 3-14, 16-26, 28-36 | 253 | filename-match:integration, primary-module, symbol-match:IntegrationConnection |
| `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIntegrationDtos.cs` | 1-158 | 1700 | filename-match:integration, primary-module |
| `apps/api/src/NEXUS.Modules.Integrations/NEXUS.Modules.Integrations.csproj` | 1-24 | 349 | filename-match:integration, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIdentityMatcher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Domain)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIntegrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Domain)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Application)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryPreviewService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Domain)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryProviderContracts.cs` — REFERENCES_TYPE (inferred-token-match; DirectoryProviderTestResult)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryProviderContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Domain)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectorySynchronizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Domain)
- `apps/api/src/NEXUS.Modules.Integrations/Application/ISyncProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Application)
- `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/DirectoryLdapOptions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Infrastructure)
- `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/DirectoryProviderException.cs` — REFERENCES_TYPE (inferred-token-match; DirectoryProviderException)
- `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/DirectoryProviderException.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Infrastructure)
- `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/EntraDirectoryProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Application)
- `apps/api/src/NEXUS.Modules.Integrations/Infrastructure/LdapDirectoryProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.Infrastructure)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.SharedKernel/PreAuthenticationTenantExecutionContext.cs` — REFERENCES_TYPE (inferred-token-match; Scope)
- `apps/api/src/NEXUS.SharedKernel/TrustedTenantExecutionContext.cs` — REFERENCES_TYPE (inferred-token-match; Scope)
- `apps/web-react/src/features/administration/DirectoryIntegrationPage.tsx` — REFERENCES_TYPE (inferred-token-match; DirectoryConnectionDto)
- `apps/api/src/NEXUS.Api/Program.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Integrations.ModuleRegistration)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIntegrationService.cs` — REFERENCES_TYPE (inferred-token-match; IDirectoryIntegrationService)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryIntegrationService.cs` — IMPLEMENTS_INTERFACE (source-class-declaration; DirectoryIntegrationService declares IDirectoryIntegrationService)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectoryPreviewService.cs` — REFERENCES_TYPE (inferred-token-match; DirectoryFieldMappingDto)
- `apps/api/src/NEXUS.Modules.Integrations/Application/DirectorySynchronizationService.cs` — REFERENCES_TYPE (inferred-token-match; DirectoryFieldMappingDto)
- `apps/api/src/NEXUS.Modules.Integrations/Domain/SyncConflict.cs` — REFERENCES_TYPE (inferred-token-match; IntegrationSync)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9565** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
