# Frontend Data Integrity Remediation

## Phase 2 status: incomplete (blocker)
- Currency fallbacks removed from ProjectFinanceTab, PlannedPaymentsPage, ProjectOverviewTab, currency.ts, messages.tsx, CostsPage.
- Typecheck now fails due to financial DTO currency: string requirements and form state.
- Blocker: backend DTO contracts require non-null currency; missing currency handling needs aligned backend/frontend contract decision.
- No hardcoded USD/GBP fallbacks introduced.

## Current execution and acceptance (2026-10-28)

This section supersedes the earlier Phase 2 blocker below. The previous handover correctly recorded unresolved currency typing at that time; the current React TypeScript check now passes.

### Phase 2 — Currency and backend-driven rules

- Centralized currency, number, date/time, and file-size formatting under `apps/web-react/src/lib/formatting/` and delegated shared UI formatters to those helpers.
- Financial forms now use an authoritative project/base currency or an explicit selection, preserve currencies on existing records, and show an unavailable state when the source currency is missing. Historical report comparisons use each saved snapshot currency and do not compute a monetary delta across currencies.
- Removed the client-only password-length assumption from public invitation acceptance. Initial-password changes consume the API password policy; an absent or invalid temporary-password expiry fails closed rather than inventing a date.
- Added focused date and initial-password tests. Financial tests cover valid currencies, missing/invalid amounts and currencies, planned-payment currency behavior, and saved report snapshots.

### Phase 3 — Localization

- Applied EN/AR text and shared status/date/number/currency/file-size formatting across the active React/Vite surfaces, including loading/error/retry/dismiss and accessible labels. Business enum values and API contracts were not changed.
- Translation checks pass: EN↔AR parity covers **5,608 leaf keys**; used-key validation covers **3,685 literal keys**.
- Focused tests include Arabic rendering and the React route inventory. No live browser visual review was run in this pass.

### Phase 4 — FOPX PM identity

- Active React exports use the shared brand configuration for workbook authors, status-report document metadata, critical-path metadata, and export names. Production `dist/index.html` contains the FOPX PM title/application name and `/fopx-pm.svg` favicon.
- The React Firebase service-worker fallback title now says FOPX PM. The server-generated Firebase title/body in `apps/api/src/NEXUS.Modules.Notifications/Infrastructure/FirebaseNotificationChannel.cs` still says NEXUS; no API file was changed because the earlier task explicitly required preserving `apps/api`. An attempted change was rejected by the automatic approval review for that reason. Phase 4 remains partial until that server copy is separately authorized and updated.

### Final quality checks

- React TypeScript: **PASS** — `npm --prefix apps/web-react run typecheck`.
- React ESLint: **PASS**, 0 errors and 1 warning in untouched `src/features/resources/ResourceCostProfilesSheet.tsx` (missing `createMutation` dependency).
- React production build: **PASS** — 2,560 modules transformed. Vite reports existing large chunks above 500 kB; output was produced successfully.
- Focused React tests: **PASS, 142/142** across 22 files (117 financial/localization/export/missing-data checks and 25 authentication/invitation/temporary-password checks).
- EN/AR parity and used-key validation: **PASS** — 5,608 and 3,685 keys respectively.
- API build: **FAIL** — `dotnet build apps/api/NEXUS.slnx --no-restore` reports `CS0234` at `apps/api/src/NEXUS.Modules.Notifications/Infrastructure/NotificationDeliveryProcessor.cs:3`: namespace `NEXUS.Modules.Identity` cannot be resolved. That file is already modified in the shared working tree; it was not changed in this pass.
- No API endpoints, database objects, stored records, deployments, or running services were changed.
- `git diff --check` reports extra blank lines at EOF in already-modified `CostsPage.tsx`, `OperationsPagesFull.tsx`, and `PlanningImportSheet.tsx`; these were left unchanged to preserve the existing shared work.

## Final acceptance matrix

| Phase | Status | Evidence | Remaining issues |
|---|---|---|---|
| Phase 1 — Fake operational data | Previously completed | Retained prior evidence; `dashboard-widget-availability.test.ts` passed in this pass. No Phase 1 data source was changed here. | None identified in this pass. |
| Phase 2 — Currency and business rules | **PASS** | TypeScript pass; currency/planned-payment/report tests; API-policy and missing-expiry tests. | API build is blocked by the separate modified Notifications project file listed above. |
| Phase 3 — Localization and formatting | **PASS** | EN/AR parity, used-key validation, Arabic date/member and localized portfolio/program tests. | Live browser RTL visual review not run. |
| Phase 4 — FOPX PM branding | **PARTIAL** | React export metadata/names and built HTML are branded; React service-worker fallback is branded. | API-generated Firebase title/body still use NEXUS because API changes were expressly out of scope and the approval review rejected the edit. |
| Final quality gate | **PARTIAL** | React typecheck, lint, build, 142 focused tests, and translation checks pass. | API build fails with `CS0234`; one pre-existing ESLint warning and Vite chunk-size warnings remain. |
