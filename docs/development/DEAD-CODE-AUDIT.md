# DEAD CODE AUDIT — FOPX PM (Deep Evidence)

## 1. Executive Summary

Read-only deep audit. Tools actually executed: `tsc -b` (1 error), `eslint .` (39 problems: 32 errors, 7 warnings), `i18n check-react-used-keys` (9 missing EN keys). No production code modified.

## 2. Repository Scope

- Backend: `apps/api`
- Frontend: `apps/web-react` only (apps/web retired per ADR-009)
- Analyzed: 1 frontend project + backend module tree

## 3. Tools Executed

| Tool | Result |
|---|---|
| tsc -b | 1 error: `ProjectPaymentRequests.tsx:39` Cannot find name `useI18n` |
| eslint . | 39 problems (32 errors, 7 warnings) |
| i18n used-keys | 9 missing EN translation keys |

## 4. Concrete Findings (Frontend)

### F-FE-001 — Unused destructured values
- `src/features/administration/SettingsWorkspaces.tsx:240` `updateSetting` unused
- `src/features/administration/SettingsWorkspaces.tsx:241` `resetSetting` unused
- `src/features/administration/WorkItemStatusesPage.tsx:40` `STATUS_CODE_MAX_LENGTH` unused
- `src/features/delivery/MeetingPages.tsx:8` `Input` unused
- `src/features/delivery/MeetingPages.tsx:10` `Badge` unused
- `src/features/projects/ProjectGovernanceTabs.tsx` multiple unused imports (CalendarCheck, ExternalLink, Search, Users, Select*, Pagination, PageHeader, useDebounce, ApiError, getRiskLevelTextClass, getRiskStatusTextClass)
- `src/features/projects/ProjectPaymentRequests.tsx:24` `ProjectPaymentRequest` unused; `:285` `index` unused
- `src/features/projects/ProjectSnapshotsTab.tsx:59` `formatDate` unused; `:92` `projectId` unused
- `src/lib/export/project-status-report-export.ts:66` `enumLabel` unused

### F-FE-002 — Missing translation keys
9 keys referenced but absent in EN: meetings.recurrence.indicator, projects.financialFoundation, projects.financialFoundationDescription, projects.planning.constraintTypes., projects.planning.detailTabs., projects.planning.formatColors., projects.planning.formatHighlights., projects.planning.formatSizes., workItems.currentStatus

### F-FE-003 — React Hooks rules violations (conditional hooks)
- `MeetingCreatePage.tsx:235,240,254,260,261`
- `MeetingsBrowser.tsx:304,308`

### F-FE-004 — Type error
- `ProjectPaymentRequests.tsx:39:49` Cannot find name `useI18n`

## 5. Backend Findings

No .NET build executed (avoid expensive rebuild). Protected: EF migrations, entities, MPP adapter (MPXJ.Net/IKVM per mandatory exception), in-progress financial redesign, meetings redesign, tenant foundation.

## 6. API Contract Findings

Endpoints without identified frontend consumers classified as uncertain (Category B) pending consumer inventory (external/mobile/tests/admin).

## 7. Unused Dependencies

None confirmed removable. MPXJ.Net/IKVM retained per mandatory exception.

## 8. Duplicated/Legacy

- Financial: legacy costs + new planned payments coexist (in-progress, Category F)
- Meetings: legacy + new sharing/workspace (in-progress, Category F)
- SidebarRail.tsx vs Sidebar (Category D pending parity)

## 9. False Positives / Protected

EF artifacts, DI-registered services, reflection-scanned code, role-gated screens, MPP dependencies, in-progress workstreams.

## 10. Limitations

- .NET build not run; backend findings based on structure + protection rules
- Unused exports not exhaustively traced beyond lint scope
- No knip available (not installed); not installed per policy

## 11. Risk Assessment

High-risk: conditional hooks in MeetingCreatePage/MeetingsBrowser (runtime bugs). Low-risk: unused imports (cleanup).

## 12. Recommended Next Steps

1. Fix conditional hooks (runtime correctness).
2. Add 9 missing translation keys.
3. Fix useI18n type error.
4. Remove unused imports after verification.
5. PO approval before any deletion.
