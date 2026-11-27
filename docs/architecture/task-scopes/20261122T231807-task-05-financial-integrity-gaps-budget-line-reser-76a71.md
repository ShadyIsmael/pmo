# UPI Task Context Manifest

Task: **Task 05 financial integrity gaps: budget line reservations, status report actual paid currency quality, permission scopes, planned payment outstanding reporting and program/portfolio pending request annual availability**
Primary module: **permissions** (task-explicit-alias)
Architecture snapshot: `20261122T230530-8e78adf`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `packages/config/src/permission-config.ts` | 80-80 | 38 | filename-match:permission, module-mentioned, primary-module, symbol-match:PermissionCode |
| `apps/web-react/src/test/permission-catalog-cache.test.tsx` | 11-37 | 264 | filename-match:permission, module-mentioned, primary-module, symbol-match:catalogPermission |
| `apps/web-react/src/test/user-permission-overrides.test.ts` | 1-17 | 201 | filename-match:permission, module-mentioned, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` — REFERENCES_TYPE (inferred-token-match; PermissionCatalogItemDto)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/web-react/src/features/administration/RolesPage.tsx` — REFERENCES_TYPE (inferred-token-match; PermissionCatalogItemDto)
- `apps/web-react/src/features/administration/api/admin.ts` — IMPORTS (verified-static-import; ../features/administration/api/admin)
- `apps/web-react/src/features/administration/api/admin.ts` — REFERENCES_TYPE (inferred-token-match; PermissionOverrideEffect)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Organization)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — REFERENCES_TYPE (inferred-token-match; PermissionOverrideEffect)
- `packages/config/src/index.ts` — IMPORTS (verified-static-import; ./permission-config)

## Budget

- Initial selected source: **503** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
