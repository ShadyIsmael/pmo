# DEAD CODE CLEANUP BACKLOG — FOPX PM (Deep Evidence)

## Concrete Findings

| Priority | Finding ID | Location | Type | Evidence | Risk | Confidence | Recommendation |
|---|---|---|---|---|---|---|---|
| P1 | F-FE-BUG-001 | src/features/delivery/MeetingCreatePage.tsx:235,240,254,260,261 | Conditional React Hooks (rules-of-hooks) | eslint errors | High (runtime) | High | Fix hook ordering |
| P1 | F-FE-BUG-002 | src/features/delivery/MeetingsBrowser.tsx:304,308 | Conditional React Hooks | eslint errors | High | High | Fix hook ordering |
| P1 | F-FE-TYPE-001 | src/features/projects/ProjectPaymentRequests.tsx:39 | TS2304 Cannot find name useI18n | tsc | High | High | Fix import |
| P2 | F-FE-I18N-001 | 9 missing EN keys | Missing translations | i18n script | Medium | High | Add keys |
| P2 | F-FE-UNUSED-001 | SettingsWorkspaces.tsx:240-241 | Unused vars (updateSetting, resetSetting) | eslint | Low | High | Remove if truly unused |
| P2 | F-FE-UNUSED-002 | WorkItemStatusesPage.tsx:40 | Unused const STATUS_CODE_MAX_LENGTH | eslint | Low | High | Remove |
| P2 | F-FE-UNUSED-003 | MeetingPages.tsx:8,10 | Unused imports Input, Badge | eslint | Low | High | Remove |
| P2 | F-FE-UNUSED-004 | ProjectGovernanceTabs.tsx (multiple) | Unused imports | eslint | Low | High | Remove |
| P2 | F-FE-UNUSED-005 | ProjectPaymentRequests.tsx:24,285 | Unused type/arg | eslint | Low | High | Remove |
| P2 | F-FE-UNUSED-006 | ProjectSnapshotsTab.tsx:59,92 | Unused var/arg | eslint | Low | Medium | Verify |
| P2 | F-FE-UNUSED-007 | project-status-report-export.ts:66 | Unused enumLabel | eslint | Low | Medium | Verify |
| P4 | F-BE-PROTECT-001 | EF artifacts, migrations | Protected | AGENTS.md | N/A | High | Keep |
| P4 | F-BE-PROTECT-002 | MPP adapter (MPXJ.Net/IKVM) | Protected | Mandatory exception | N/A | High | Keep |
| P4 | F-FE-PROTECT-001 | Role-gated screens | Protected | Dynamic refs | N/A | High | Keep |
| P4 | F-FE-PROTECT-002 | In-progress financial/meetings/tenant | Protected | In-progress | N/A | High | Keep |

## Summary Counts

- Confirmed removable (high conf, low risk): 0 files (only unused local vars/imports; verify before removal)
- Requires verification: 7
- Duplicate implementations: 2 (SidebarRail, financial)
- Protected/active: 4
- In-progress: financial, meetings, tenant

## Tools Executed

- tsc -b → 1 error
- eslint . → 39 problems
- i18n used-keys → 9 missing
- knip → not installed (not added per policy)

## Limitations

- .NET build not run
- Backend unused symbols not exhaustively traced
- No dependency removal authorized
