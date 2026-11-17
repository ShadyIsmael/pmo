# API Error Message Display — Handover

**Date:** 2026-11-17  
**Scope:** React/Vite frontend API error display and localization

## Work inherited from the previous Agent

- The shared API error localizer had been extended to recognize detailed handover, change-request, and scope-related server messages, including `FORBIDDEN` responses.
- The Project Handover page and overview side sheet had been changed to display request and capability errors in the affected UI section. The reusable `ErrorState` had also been given `role="alert"`.
- Localization and Project Handover UI tests were present as uncommitted work. The handover UI test file was untracked when this recovery began.
- The Arabic server-message translations were embedded directly in `api-error-localization.ts`; some handover panel loads and mutation failures still replaced specific API errors with generic text.

## Completed

- Kept the inherited error display and retry behavior, while completing the remaining handover error paths for capability and manager lookups, history, snapshots, and mutation failures.
- Added `getApiErrorMessage`, which displays sanitized `ApiError` messages and uses the supplied localized fallback for non-API exceptions.
- Used existing `managerHandover` catalog entries when they fit and moved the remaining EN/AR server-message translations into the authoritative `apiErrors` localization resources.
- Kept API routes, payloads, status codes, and RBAC behavior unchanged. No backend or database files were changed for this task.
- Updated tests to use API error instances and localization catalog values rather than stale fixtures or duplicated Arabic literals.

## Files changed in this work

- `apps/web-react/src/api/api-error-localization.ts`
- `apps/web-react/src/api/client.ts`
- `apps/web-react/src/components/ui/StateDisplay.tsx` (inherited accessibility change retained)
- `apps/web-react/src/features/delivery/ManagerHandoverPanel.tsx`
- `apps/web-react/src/features/projects/ProjectDetailPage.tsx`
- `apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx`
- `apps/web-react/src/features/projects/components/ProjectOverviewActionSheets.tsx`
- `apps/web-react/src/i18n/messages/en.json`
- `apps/web-react/src/i18n/messages/ar.json`
- `apps/web-react/src/test/api-error-localization.test.ts`
- `apps/web-react/src/test/portfolio-handover-panel.test.tsx`
- `apps/web-react/src/test/project-handover-tab.test.tsx` (inherited untracked test updated)
- `docs/quality/API-ERROR-MESSAGE-DISPLAY-HANDOVER.md`

Other unrelated modified and untracked files were present in the shared working tree and were preserved.

## Remaining

- No known implementation items remain in the affected handover and status-report error display paths.
- Browser/runtime UAT was not run. The verification below is automated frontend testing and production build only.

## Verification

- Focused tests: **37 passed, 0 failed** across `api-error-localization.test.ts`, `project-handover-tab.test.tsx`, and `portfolio-handover-panel.test.tsx`.
- Production build: `npm run build` **passed** (`tsc -b` and Vite production build).
- `git diff --check`: **passed**. Git printed only the existing LF-to-CRLF notices for the EN/AR JSON files.
- Build warnings: Rollup removed two unsupported Zod comment annotations; Vite reported the existing mixed static/dynamic import for `PlannedPaymentsPage.tsx` and oversized chunks. The build completed successfully.

## Unresolved blockers

- None for the requested frontend implementation. Browser/runtime UAT remains unverified.
