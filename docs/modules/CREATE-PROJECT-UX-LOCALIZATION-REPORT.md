# Create Project UX and Localization — Implementation Report

Date: 2026-11-11  
Status: Implemented; Product Owner browser UAT pending. Complete localization of arbitrary server-generated errors remains open.

## Screen and preserved behavior

The existing side sheet previously mixed classification, hierarchy and assignments. It now groups basic information, hierarchy, governance and assignments, schedule and finance, lifecycle, and review, with the existing classification and financial subheadings. It retains the existing side-sheet entry point and edit flow. Responsive grids, labels, validation feedback, focus for invalid fields, and duplicate-submit protection support the existing controls.

Portfolio selection filters the scoped program lookup; changing the portfolio clears an incompatible program. Selecting a program derives its portfolio. The existing scoped workflow lookup endpoints, Project.Create authorization, manager eligibility, sponsor assignment, and subsequent manager handover remain backend-owned. Server-side creation sets Pre-Initiation independently of Project Status; phase is not included in the create payload. No backend changes or database operations were made.

The project budget remains the lifetime ceiling. Existing authorized initial annual budget behavior is preserved: it is suggested from the ceiling, may be edited, and when submitted is created as a Draft, not an approved allocation. No finance calculation or approval workflow was changed.

## Localization and accessibility

Added English and Arabic section titles, instructions and validation messages; repaired missing English Create Project phase labels. The form uses logical icon spacing and LTR numeric/date/identifier entry. Direct translation keys used by the form were checked in both catalogs. Known schema validation messages are localized. Some backend-provided API errors can still be displayed in English, including arbitrary field and request errors; complete server-error localization is not verified and remains a limitation.

## Files changed for this task

- `apps/web-react/src/features/projects/components/CreateProjectSheet.tsx`
- `apps/web-react/src/features/projects/lib/project-lookups.ts`
- `apps/web-react/src/i18n/messages/en.json` and `ar.json`
- `apps/web-react/src/test/project-create-sheet-initial-budget.test.tsx`
- `docs/quality/MASTER-UAT-CHECKLIST.md` (CPUX-01–06)
- This report

Existing uncommitted changes in these and other files were preserved; the overall Git diff includes prior work.

## Verification

- React `npm run typecheck`: passed.
- Focused `npx vitest run --pool=threads --maxWorkers=1 src/test/project-create-sheet-initial-budget.test.tsx`: 7 passed. The default forks pool failed before collection with sandbox EPERM; threads succeeded.
- Focused tests exercise both locale directions, required name, dependent hierarchy, manager payload, omission of phase/status, budget behavior, and API-error value retention using mocked lookups. They do not verify a real backend or visual responsive layout.
- Backend build: not run; backend unchanged. Database operations: not run.
- Product Owner browser UAT: not run. Verify desktop, tablet and mobile layout; RTL alignment and keyboard focus; scoped lookups, hierarchy changes and manager assignment; financial values and server errors; creation and later lifecycle transition under real permissions. See CPUX-01–06 in the Master UAT Checklist.
