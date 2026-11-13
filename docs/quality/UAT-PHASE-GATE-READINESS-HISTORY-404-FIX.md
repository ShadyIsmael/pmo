# FOPX PM — Phase Gate Readiness & History 404 Fix

**Priority:** P1 — Confirmed UAT Defect  
**Status:** Fixed  
**Date:** 2026-11-13  

## Confirmed Root Cause

The `ProjectPhaseGateSideSheet.tsx` frontend component was using `project.id` (internal `Project.Id`) instead of `project.publicId` (external `Project.PublicId`) when calling phase gate API endpoints. This caused the `ProjectPublicIdRoutingMiddleware` to fail resolving the project by PublicId, resulting in HTTP 404 "The requested resource was not found."

### Affected Endpoints

1. **Readiness**: `GET /api/projects/{projectId}/phase-gate/readiness?targetPhaseId={targetPhaseId}`
   - Calls: `useProjectPhaseGateReadiness(projectId, ...)`
   - Previously used: `project.id` → middleware lookup by PublicId failed → 404

2. **History**: `GET /api/projects/{projectId}/phase-gate/history`
   - Calls: `useProjectPhaseHistory(projectId, ...)`
   - Previously used: `project.id` → middleware lookup by PublicId failed → 404

3. **Link Document**: `POST /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document`
   - Calls: `useLinkPhaseGateDocument(projectId)`
   - Previously used: `project.id`

4. **Unlink Document**: `DELETE /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document`
   - Calls: `useUnlinkPhaseGateDocument(projectId)`
   - Previously used: `project.id`

5. **Change Phase**: `POST /api/projects/{projectId}/phase-gate/change-phase`
   - Calls: `useChangeProjectPhase(projectId)`
   - Previously used: `project.id`

### Middleware Behavior

The `ProjectPublicIdRoutingMiddleware` (`apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs`):
- Extracts the `projectId` from the route (`{projectId:guid}`)
- Looks up the project by matching `item.PublicId == publicId && item.TenantId == tenantId`
- If found, rewrites the route value from PublicId to Internal Id: `context.Request.RouteValues["projectId"] = project.Id`
- If not found, returns 404 "The requested resource was not found."

When the frontend sent the internal `Project.Id` instead of the external `Project.PublicId`, the middleware could not find a project whose `PublicId` matched the internal ID, and returned 404.

## Files Changed

### Fixed: `apps/web-react/src/features/projects/components/ProjectPhaseGateSideSheet.tsx`

Changed all phase gate endpoint calls from `project.id` to `project.publicId`:

| Before (line) | After | Endpoint |
|---|---|---|
| `95: useProjectPhaseGateReadiness(project.id, ...)` | `useProjectPhaseGateReadiness(project.publicId, ...)` | Readiness |
| `96: useProjectDocuments({ projectId: project.id, ... })` | `useProjectDocuments({ projectId: project.publicId, ... })` | Documents list |
| `98: useLinkPhaseGateDocument(project.id)` | `useLinkPhaseGateDocument(project.publicId)` | Link document |
| `99: useUnlinkPhaseGateDocument(project.id)` | `useUnlinkPhaseDocument(project.publicId)` | Unlink document |
| `100: useChangeProjectPhase(project.id)` | `useChangeProjectPhase(project.publicId)` | Change phase |
| `153: if (!project.id)` | `if (!project.publicId)` | Upload check (line 153) |
| `154: form.set("ProjectId", project.id)` | `form.set("ProjectId", project.publicId)` | Upload form (line 154) |

### Already Correct: `apps/web-react/src/features/projects/ChangePhaseSettings.tsx`

The `ChangePhaseSettings` component was already correctly using `project.publicId` via the `phaseGateProjectId` variable (line 105):
```typescript
const phaseGateProjectId = project.publicId || projectId;
```

All endpoints use `phaseGateProjectId` which resolves to the PublicId.

## Verification

### Frontend Changes

- All phase gate API calls in `ProjectPhaseGateSideSheet.tsx` now use `project.publicId`
- The `ChangePhaseSettings.tsx` component was already correct and requires no changes
- Vite proxy configuration (`apps/web-react/vite.config.ts`) correctly proxies `/api` → `https://localhost:5001`
- Both frontend hooks call the correct backend routes through the middleware pipeline

### API Pipeline

- `ProjectPublicIdRoutingMiddleware` recognizes both `/phase-gate/readiness` and `/phase-gate/history` routes
- Route parameter `projectId:guid` is rewritten from PublicId to Internal Id after successful lookup
- Controllers and services consistently receive the expected identity type after middleware resolution
- Project exists within the authorized Tenant/Organization/Project scope after middleware resolution

### Authorization

- Authorization policies remain unchanged: `Project.View` + `Document.View` for readiness, `Project.View` for history
- legitimate NotFound behavior for unauthorized or cross-Tenant access is preserved
- Existing Project Details and Lifecycle Options routes are not regressed

## Test Coverage

### Existing Tests (already pass)

- `ChangePhaseSettings.test.tsx` - Uses `ChangePhaseSettings` with project having both `id` and `publicId`; mocks API calls
- `project-phase-gate-side-sheet.test.tsx` - Uses `ProjectPhaseGateSideSheet` with project having both `id` and `publicId`; mocks API calls

### Recommended Test Additions

Add focused regression tests verifying:

1. **Readiness with valid Project.PublicId** - Send external PublicId, verify 200 with correct data
2. **History with valid Project.PublicId** - Send external PublicId, verify 200 with records or `200 []` when empty
3. **History with zero records returning 200 []** - Project with no phase-change history returns `HTTP 200` with empty array
4. **Readiness for a phase with zero templates** - Valid request returns advisory statuses (Missing/Mismatch/NotVerified/PendingValidation), not 404
5. **Invalid Project PublicId** - Returns 404 (legitimate not-found, not routing failure)
6. **Cross-Tenant and cross-Organization access** - Access from unauthorized tenant returns 404
7. **Unauthorized access and Active Deny** - Missing required permissions returns 403, not 404 routing failure
8. **Invalid or inaccessible target Phase** - Returns appropriate error, not 404 from middleware
9. **Correct document-detail redaction without Document.View** - Document details remain protected by permissions
10. **Lifecycle Options 404 regression** - Existing Lifecycle Options routes still return correct 404s for invalid IDs

### Identity Semantics

- **Service-level tests**: Pass internal `Project.Id`
- **HTTP integration tests**: Send external `Project.PublicId` to exercise `ProjectPublicIdRoutingMiddleware`

## Authorization and Isolation Safeguards

- Tenant, Organization, Project scope enforcement remains via the middleware
- Active Deny enforcement preserved for all authorization checks
- Document details remain protected by `Document.View` permission
- Cross-Tenant and cross-Organization access rejection preserved
- Legitimate 404 responses for truly missing resources are not masked

## Product Owner Retest Instructions

1. Build the affected frontend: `cd C:\Projects\pmo\apps\web-react && npm run build`
2. Start the development server: `npm run dev:web` (or `vite --config vite.config.ts`)
3. Navigate to a project's Phase Gate side sheet or Change Phase settings
4. Verify the Readiness and History endpoints load correctly with the project's PublicId
5. Verify History returns `200 []` when the project has no phase-change records
6. Verify invalid Project PublicIds still return 404 (not masked successes)
7. Verify cross-Tenant access still returns 404 as expected
8. Run the existing test suites:
   - `npm test -- --run change-phase-settings`
   - `npm test -- --run project-phase-gate-side-sheet`
9. Confirm no regression to Create Project, Lifecycle Options, or other Phase Change functionality

## Summary

**The defect was caused by the frontend sending internal `Project.Id` instead of external `Project.PublicId` to phase gate API endpoints. The `ProjectPublicIdRoutingMiddleware` expects PublicIds in the route parameters and looks them up in the database. When it receives an Internal Id, the lookup fails and returns 404.**

**The fix** changes `ProjectPhaseGateSideSheet.tsx` to use `project.publicId` for all phase gate endpoint calls, aligning it with the existing correct implementation in `ChangePhaseSettings.tsx` and the expected routing middleware behavior.

**No backend changes were required** - the fix is purely frontend, updating the identity sent in API requests from internal to external format.