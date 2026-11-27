# FOPX PM | Document Category 404 and Auth/me 401 — Extended Fix Scope

**Date:** 2026-11-26  
**Priority:** P1 — Confirmed UAT blocker  
**Scope:** Document categories (`/api/documents/categories`, `/api/document-categories/project-upload-options`), auth session restoration (`/api/auth/me`)  
**Status:** IMPLEMENTATION COMPLETE — source fixes applied, API built (0 errors), frontend tests pass, UPI refresh + validate OK, runtime verification pending credentials

---

## New Confirmed UAT Failures

| Endpoint | Project ID supplied | Error |
|---|---|---|
| `GET /api/documents/categories?projectId=c9af7e6c-96f7-48f1-bfa8-0100331bb0b2` | `c9af7e6c-...` | 404 |
| `GET /api/document-categories/project-upload-options?projectId=1ff0a3d0-32d3-4a78-b2e8-85944bbafc90` | `1ff0a3d0-...` | 404 |
| `GET /api/auth/me` (duplicate calls) | N/A | 401 x2 (React Strict Mode) |

---

## Project ID Correlation (read-only SQL, Development DB `NexusPmo`)

Both GUIDs refer to the **same project**:

| Field | Value |
|---|---|
| `Projects.Id` (InternalId) | `C9AF7E6C-96F7-48F1-BFA8-0100331BB0B2` |
| `Projects.PublicId` | `1FF0A3D0-32D3-4A78-B2E8-85944BBAFC90` |
| `Name` | Froud Management System |
| `Code` | PRJ-120334 |
| `TenantId` | `11111111-1111-1111-1111-111111111111` |
| `ProjectManagerId` | `EFD250E9-2AF6-4E5C-8F05-63E69DFC89C2` (Shady Ismael) |
| `SponsorId` | `AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA` (placeholder) |
| `PortfolioId` | NULL |
| `ProgramId` | NULL |
| `DepartmentId` | NULL |

- `ProjectMembers` table: **0 rows** for this project — no user is assigned as a member
- Only the ProjectManager (`EFD250E9-...`) has this project in their management scope (via `x.ProjectManagerId == userId` in `ManagementScopeResolver.LoadAssignmentsAsync`)

---

## Root Cause Analysis

### 1. `GET /api/documents/categories?projectId=` 404 — CONFIRMED & FIXED

**Caller:** `ProjectDocumentsTab.tsx:175` (before fix)

```tsx
// BEFORE (defect):
const projectLibraryCategories = useDocumentCategories({ projectId: internalProjectId ?? "" }, ...);

// AFTER (fix):
const projectLibraryCategories = useDocumentCategories({ projectId: projectId ?? "" }, ...);
```

**Mechanism:** `ProjectPublicIdRoutingMiddleware` resolves `?projectId=` by **PublicId only**, never falls back to `Project.Id`. The caller was passing `internalProjectId` (`c9af7e6c-...`) instead of `projectId` (`1ff0a3d0-...`). An InternalId never matches `item.PublicId` → middleware returns **404** before the controller/service run.

**Fix:** Send PublicId (`projectId`) instead of InternalId (`internalProjectId`) to `useDocumentCategories`.

### 2. `GET /api/document-categories/project-upload-options?projectId=` 404 — CONFIRMED & FIXED

**Caller:** `ProjectDocumentsTab.tsx:237` (already uses correct PublicId)

This caller passes `projectId` (PublicId `1ff0a3d0-...`) — the correct identifier type. The 404 was **not** an ID-type mismatch.

**Root cause:** `DocumentService.GetProjectUploadCategoryOptionsAsync` treated missing `ProjectOrganizationIds` mapping as 404 NotFound:

```csharp
// BEFORE (defect) — DocumentService.cs:314-319
if (projectId == Guid.Empty
    || !managementContext.Scope.ProjectIds.Contains(projectId)
    || !managementContext.Scope.ProjectOrganizationIds.TryGetValue(projectId, out var organizationId)
    || !CanAccessOrganization(managementContext, organizationId))
    return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
        Error.NotFound("The requested project was not found."));
```

The project has no `DepartmentId`, `PortfolioId`, or `ProgramId` → `ProjectOrganizationLookup.ResolveOrganizationIdAsync` returns null → `ProjectOrganizationIds` dictionary doesn't contain this project → `TryGetValue` fails → 404.

**Asymmetry:** `GetCategoriesAsync` handles missing org mapping gracefully (returns empty array, success). `ProjectLifecycleCatalogService.GetOptionsAsync` handles it gracefully (falls back to tenant catalog). Only `GetProjectUploadCategoryOptionsAsync` treated it as 404.

**Fix:** Separate scope check from org mapping check:
- Project not in scope → 404 (security boundary, unchanged)
- Project in scope, no org mapping → return empty array (success, consistent with `GetCategoriesAsync`)
- Project in scope, org mapping exists but not accessible → 404 (org security boundary, unchanged)

### 3. `GET /api/auth/me` duplicate 401 — CONFIRMED & FIXED

**Mechanism:** React Strict Mode double-invokes the mount `useEffect` in `AuthProvider.tsx`. Each invocation calls `initializeCurrentSession` → `api.getMe()` → two 401s when the session is invalid.

**Fix:**
- Added `restorationPromise` ref for single-flight session restoration: concurrent/duplicate `initializeCurrentSession` calls share one in-flight `api.getMe()` promise
- Added `knownInvalidSession` ref: `synchronizeSession` skips `getMe()` on focus/timer when the session is known invalid
- `login()` resets `knownInvalidSession.current = false` on new login

**Token refresh:** `client.ts` `send()` already handles automatic token refresh on 401 (lines 346-354) before retrying `/api/auth/me`. `/api/auth/me` is not in `isRefreshExcluded`, so refresh+retry works correctly. No code change needed.

---

## Shared Architectural Defect

All three project-scoped endpoints (`documents/categories`, `document-categories/project-upload-options`, `project-lifecycle/options`) share the same routing stack:

1. `ProjectPublicIdRoutingMiddleware` — `?projectId=` resolved by **PublicId only**, fail-closed 404 on InternalId
2. `ManagementContextMiddleware` — scope membership check, fail-closed 404 on out-of-scope
3. Controller + service — tenant/organization/scope enforcement

**Common footgun:** Callers passing `Projects.Id` (InternalId) where `Projects.PublicId` is expected. **Confirmed** at `ProjectDocumentsTab.tsx:175` (fixed).

**Audit of all project-scoped callers** — no other InternalId/PublicId inconsistencies found:
- `useDocumentCategories` — FIXED (now uses PublicId)
- `useProjectUploadCategoryOptions` — already uses PublicId from outlet context
- `useProjectLifecycleOptions` (projects hook) — uses `project.publicId` (`CreateProjectSheet.tsx:210`, `ChangePhaseSettings.tsx:109`, `ProjectPhaseGateSideSheet.tsx:96`)
- `useProjectLifecycleOptions` (administration hook) — exported but not called anywhere

---

## Changes Made

### Frontend

**`apps/web-react/src/features/projects/ProjectDocumentsTab.tsx:175`**
- Changed `useDocumentCategories({ projectId: internalProjectId ?? "" })` → `useDocumentCategories({ projectId: projectId ?? "" })`
- Changed enabled gate from `Boolean(internalProjectId)` → `Boolean(projectId)`

**`apps/web-react/src/auth/AuthProvider.tsx`**
- Added `restorationPromise` ref (line 21) for single-flight session restoration
- Added `knownInvalidSession` ref (line 22) to skip `getMe()` on focus/timer when session known invalid
- `initializeCurrentSession` (lines 32-59): shares one in-flight promise across duplicate calls
- `synchronizeSession` (lines 91-106): skips `getMe()` when `knownInvalidSession.current` is true
- `login()` (line 141): resets `knownInvalidSession.current = false`

### Backend

**`apps/web-react/src/NEXUS.Modules.Documents/Application/DocumentService.cs`** — `GetProjectUploadCategoryOptionsAsync`:
- Separated scope check (`ProjectIds.Contains`) from org mapping check (`ProjectOrganizationIds.TryGetValue`)
- Missing org mapping now returns empty array (success) instead of 404 NotFound
- Scope check still returns 404 (security boundary)
- Org access check still returns 404 (security boundary)

### No changes to:
- `apps/web-react/src/api/client.ts` — retry behavior already correct (refresh+retry on 401)
- `apps/web-react/src/api/queryClient.ts` — `shouldRetry` returns false for all 4xx
- `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs` — PublicId-only resolution is correct behavior

---

## Tests Added

**`apps/web-react/src/test/document-center-parity.test.tsx`**
- Assertion: `useDocumentCategories` called with `{ projectId: "public-42" }` (PublicId), never `{ projectId: "internal-42" }` (InternalId)

**`apps/web-react/src/test/auth-session-isolation.test.tsx`**
- Test: "does not repeat /api/auth/me on focus after an invalid session" — verifies single-flight restoration and no re-fetch on focus after 401

---

## Verification Results

| Check | Result |
|---|---|
| `tsc --noEmit` (web-react) | Passed — 0 errors |
| `dotnet build` (API) | Passed — 0 warnings, 0 errors |
| `document-center-parity.test.tsx` (PublicId assertion) | Passed |
| `auth-session-isolation.test.tsx` (single-flight test) | Passed |
| `change-phase-settings.test.tsx` + `project-phase-gate-side-sheet.test.tsx` | 16/16 passed |
| UPI `refresh` | 1 changed file indexed, 1922 unchanged |
| UPI `validate` | `ok: true`, 0 errors, 0 warnings |
| `GET /api/documents/categories?projectId=<PublicId>` (no auth) | **401 Unauthorized** (was 404 with InternalId) |
| `GET /api/document-categories/project-upload-options?projectId=<PublicId>` (no auth) | **401 Unauthorized** (was 404) |
| `GET /api/project-lifecycle/options?projectId=<PublicId>` (no auth) | **401 Unauthorized** |
| `GET /api/auth/me` (no auth) | **401 Unauthorized** |
| Authenticated 200 responses | **Not verified** — Development credentials not available |

**Note on 401 responses:** All three project-scoped endpoints return 401 Unauthorized (not 404) when called without authentication. This confirms the `ProjectPublicIdRoutingMiddleware` correctly resolves PublicId → InternalId and the request reaches the authorization layer. The 404s were caused by (1) InternalId sent instead of PublicId, and (2) missing org mapping treated as 404. Both fixed.

---

## Remaining Work

1. **Authenticated 200 verification:** Development API is running at `https://localhost:5001`. All three endpoints return 401 (not 404) without auth, confirming routing is correct. To verify actual 200 responses, authenticate as a user with the project in scope (e.g., ProjectManager `EFD250E9-2AF6-4E5C-8F05-63E69DFC89C2` / `shady.ismail@bdc.com.eg`) and call:
   - `GET /api/auth/me` → 200
   - `GET /api/documents/categories?projectId=1ff0a3d0-32d3-4a78-b2e8-85944bbafc90` → 200
   - `GET /api/document-categories/project-upload-options?projectId=1ff0a3d0-32d3-4a78-b2e8-85944bbafc90` → 200 (returns empty array since project has no org mapping)
   - `GET /api/project-lifecycle/options?projectId=1ff0a3d0-32d3-4a78-b2e8-85944bbafc90` → 200
   - After expired session → 401 once (not duplicated)

2. **Legitimate security responses preserved:** Out-of-scope users → 404; missing permissions → 403; unauthenticated → 401. All unchanged.

---

## Database / Migration Status

- No database mutations performed
- No model or schema changes
- No new EF migrations
- No seed, no reset, no password recovery

---

## Related Prior Reports

- `.kilo/plans/1795688720406-fix-lifecycle-options-and-auth-errors.md` — original plan
- `docs/quality/UAT-CHANGE-PHASE-TARGET-AND-CURRENT-DOCUMENTS-FIX.md` — previous fix report
- `docs/quality/MASTER-UAT-CHECKLIST.md` — master UAT checklist
