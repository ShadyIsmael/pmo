# Document Category Shared Scope and Phase Assignment

**Date:** 2026-11-17  
**Scope:** Document Category lookup/editing and Project Phase association.

## Delivered

- Generic category lookup now returns active categories owned by the selected Organization plus active Tenant-shared categories. Existing organization/project upload and template lookups retain their scope checks.
- Tenant-shared seed/system categories can be edited and activated/deactivated in place when the caller has the existing explicit category permission and authorized Tenant/Organization context. Their category ID, Tenant ownership, nullable Organization ownership, and `CreatedBy` value are preserved. Duplicate names are checked within the category's actual scope.
- Category management now exposes a localized multi-select for the selected Organization's phases. It loads only phases in that Organization, shows loading/error/empty/permission states, and persists each change through the existing lifecycle update API with row-version validation.
- Organization phases may reference an active Tenant-shared category or an active category owned by that same Organization. Tenant-wide phases remain limited to Tenant-shared categories. Cross-Tenant and foreign-Organization references remain rejected.
- `Pre-Initiation` allows only its document category association to change; its identity, labels, ordering, default/active flags, and lifecycle configuration remain protected.
- Phase Gate template/version associations and history are unchanged.

## Verification

- `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1` — passed, 0 errors. Existing nullable and IKVM warnings remain.
- `npx vite build --config vite.config.ts` — passed. Existing chunk-size and static/dynamic import warnings remain.
- `npx vitest run --pool=forks --maxWorkers=1 src/test/reference-data-parity.test.tsx` — passed, 25/25.
- English and Arabic message files parse as valid JSON; `git diff --check` passed.
- `npm run typecheck` — blocked by existing type errors in `src/test/permission-business-module.test.ts` and `src/test/role-permission-eligibility.test.ts`, both referring to the unrelated `isMandatoryForSystemAdmin` field. No error was reported from the modified page or API client.
- `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~DocumentCategorySharedScopeTests|FullyQualifiedName~ProjectLifecycleDocumentCategoryScopeTests" -m:1` — passed, 12/12.

## Persistence and rollout

No entity/schema change or EF migration was needed. No migration, seed, reset, or database write was run. The existing phase-to-category relationship is sufficient. Development database state and authenticated live API behavior were not inspected as part of this code-only pass.

## Remaining validation

Product Owner browser/API UAT should confirm the selected Organization sees tenant-shared and local categories; unauthorized or foreign scope access is denied; edits preserve stable category identity and history; phase assignment handles concurrent row-version changes; and `Pre-Initiation` rejects every change except its category association.
