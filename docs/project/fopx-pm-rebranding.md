# FOPX PM branding — safe partial implementation

Date: 2026-10-25. Branch: feature/dashboard-premium-redesign (observed HEAD 388b680).

## Conflict audit and boundary

The worktree already contains extensive tracked modifications and untracked work. In particular, ProjectOverviewTab, ProjectRisksTab, ProjectPlanningTabFull, PlanningGrid, PlanTimelineGantt, PlanningRibbon, their tests, Header, App, both locale catalogs, the i18n provider, report exporters and the Firebase worker were already modified. Ownership cannot be reliably attributed to an individual agent from Git status. These files were treated as occupied and were not edited.

Existing worktrees were listed but not reused. Repository Git metadata is read-only under the session permission profile; no linked worktree/branch, commit, staging, stash, reset, merge or checkout was performed. Only initially clean independent files and new uniquely named branding files were changed. No shared service was started, stopped or restarted. Process inventory through CIM was denied, so active processes/agent ownership could not be independently verified.

Read first: docs/architecture/ARCHITECTURE-STATUS.md, docs/ui/design-system.md, docs/quality/react-vite-parity-gate.md and docs/ui/dashboard-premium-redesign.md. ADR-009 establishes React/Vite as the sole supported frontend. Read AGENT-CONTEXT.md for current frontend guidance; migration parity notes are historical.

## Applied scope

- brand.json: canonical parent FOPX / fopx.one; product FOPX PM / FOPX Project Management; description, tagline, intended domain, EN/AR copy and favicon location. Intended domain is descriptive only.
- src/lib/brand.ts: typed presentation access and locale selection without altering the existing locale provider or message keys.
- LoginPage: product name and F monogram in loading and normal states, localized description/tagline and parent attribution. Credentials and login behavior remain intact.
- Sidebar: product name, expanded/collapsed F monogram, localized tagline and branded user fallback. Existing classes, geometry, dark sidebar, navigation, storage and event identifiers are preserved.
- index.html / vite.config.ts: build/dev HTML metadata comes from canonical identity; application title, description and favicon. No canonical URL, DNS or deployment configuration was introduced.
- public/fopx-pm.svg: F monogram favicon using existing navy theme color.
- LicensePluginsPages: demo corporate attribution and bundled plugin author labels use the parent company identity. The license identifier was preserved.

## Remaining reference classification and follow-up

| Classification | Examples | Treatment |
| --- | --- | --- |
| User-facing, occupied files | src/i18n/messages/{en,ar}.json (settings, search, directory integration, import guidance, admin, reports); src/App.tsx; public/firebase-messaging-sw.js | DEFERRED: overlapping unfinished work. This includes the sidebar search label that still resolves through the occupied catalog. Integrate canonical product placeholders through the existing localization system after ownership is released. |
| Generated report metadata and filenames, occupied files | src/lib/export/{project-export,project-status-report-export}.ts; src/features/projects/lib/{planning-export,critical-path-export}.ts | DEFERRED: modified/untracked ongoing exporters. Wire brand.product.name into author/header/file labels without changing export calculations or historical reports. |
| Technical/compatibility identifiers | package scope @nexus-pmo; nexus CSS classes/tokens; localStorage/session keys; custom events; import field names and error matching | PRESERVED: renaming may break preferences, sessions, API contracts or styling. |
| Credentials and license identifiers | admin@nexus.com on login; NEXUS-ENT demo license identifier | PRESERVED: do not cosmetically change a working credential or license identifier. These visible compatibility values mean the literal zero-legacy goal is not achieved. |
| Backend identity and API metadata | NEXUS.slnx, assemblies/namespaces, Program.cs Swagger title, configuration and auth issuer/audience | DEFERRED: coordinated migration requires dependent module owners and compatibility plan. Program.cs is already modified. No API routes/contracts changed. |
| Database identity/history | NexusPmo connection/database names, applied EF migrations and snapshot | PRESERVED: no database commands, schema changes, migration, seed, startup or backup operations performed. |
| Historical / third-party material | existing audit records, stored reports, signed documents, external integration names | UNCHANGED. No attempt to rewrite records to force zero search matches. |
| Generated artifacts | existing dist / build outputs | UNCHANGED; validation build is directed to a separate temporary output directory. It may still contain deferred legacy strings. |

The UI rebrand is intentionally incomplete. Functional work takes precedence; do not merge over unfinished changes or deploy to pm.fopx.one. No claim of full frontend integration, browser UAT, visual Light/Dark verification or completed technical migration is made.

## Validation

Completed results are recorded below. Full release gates do not pass; the safe branding patch is not a release approval.

### Recorded checks

- PASS: actual branding diff reviewed; no business logic, styling tokens, auth/session keys, routes or database files changed by this task. git diff --check passes.
- PASS: EN/AR identity JSON and SVG XML syntax validated.
- PASS: Node/Vite TypeScript configuration check after correcting ES library compatibility.
- PASS: targeted ESLint for changed source files; Vite config is ignored by the existing ESLint configuration.
- FAIL: application TypeScript: ProjectOverviewTab.tsx lines 359 and 730 have implicit-any callback parameters; planning-grid.test.tsx line 202 supplies onUndo not accepted by the current component. These occupied files were not modified by this task.
- FAIL: full ESLint: unused Send import in occupied ProjectOverviewTab.tsx; existing warnings in currency.ts and ResourceCostProfilesSheet.tsx. No fixes attempted in occupied functional work.
- NOT TESTED: live browser integration, EN/AR visual layout, Light/Dark visual inspection, deployed authentication and database reads. No shared service was restarted.

Vite command scripts explicitly select vite.config.ts because generated vite.config.js also exists beside it. No generated config was edited or deleted by this task. This prevents metadata configuration selection from depending on an older generated file.

### Final observed results

Focused suite: 96 tests, 89 PASS / 7 FAIL across 11 files. Passing files: auth-guard, auth-session-isolation, plan-timeline-gantt, planning-ribbon, project-gantt-parity, project-portfolio-program-localization, project-risks-parity, risk-dashboard-parity and routing. Four failures are Planning Grid bulk linking/unlinking/scheduling/progress controls; three are Project Overview Schedule Variance reasons and canonical EVM fallback expectations. These are occupied functional files untouched by the branding patch. No pre-brand baseline was run, so this report does not assert a proven baseline comparison.

PASS: isolated Vite production bundling completed (2m 6s); inspected output HTML contains FOPX PM title/application-name, canonical product description and /fopx-pm.svg. Existing bundle-size warnings remain. Full npm build gate remains FAIL because application TypeScript fails. PASS: second build explicitly selecting vite.config.ts completed in 2m 12s.

## Delivery status

| Area | Expected result | Actual status |
| --- | --- | --- |
| Ongoing work protected | No conflicts | PASS — occupied files left untouched; no staging/commit/reset/restart |
| Parent company identity | FOPX | PASS — canonical config and changed surfaces |
| Product identity | FOPX PM | PASS — canonical config and changed surfaces |
| React UI branding | Updated | DEFERRED — login/sidebar/admin attribution applied; occupied locale surfaces remain |
| Browser metadata | Updated | PASS — output HTML inspected |
| Logos and favicons | Updated | PASS — F monograms and SVG favicon; visual browser QA NOT TESTED |
| Reports and templates | Updated | DEFERRED — exporters and notification templates overlap active work |
| EN/AR | Verified | NOT TESTED — localized brand copy/syntax and existing localization suite pass; complete visual branding not verified |
| Light/Dark | Preserved | NOT TESTED — existing classes/tokens untouched; no visual UAT |
| Project Overview | No regression | FAIL — three focused assertions fail; no branding edits to this surface |
| Risk Overview | No regression | PASS — focused risk tests; browser integration NOT TESTED |
| Planning Grid/Ribbon | No regression | FAIL — four Grid failures; Ribbon and both Gantt suites pass |
| Authentication | Preserved | PASS — guard/session tests; no auth identifiers/behavior changed |
| Database and business data | Unchanged | PASS — no database actions or changes by this task |
| Technical renaming | Completed or safely deferred | DEFERRED — repository-wide identity/compatibility migration |
| Frontend tests/build | Passing | FAIL — TypeScript/lint/tests gate fails; Vite bundling and targeted changed-source lint pass |
| Legacy branding scan | Completed | PASS — remaining visible and technical references classified above; zero-visible-legacy target remains DEFERRED |

No DNS changes, production changes, deployment or publishing were performed. No live integration claim is based on a health endpoint.

## Follow-up pass — 2026-10-28

This follow-up updates only the active React/Vite brand findings above; the 2026-10-25 test results remain historical evidence for that earlier worktree.

- React-generated project, planning, status-report, and critical-path exports now use the shared `brand.json` presentation identity for names and metadata. The production HTML metadata and favicon resolve to FOPX PM.
- The React service-worker fallback notification title is FOPX PM. Server-generated Firebase title/body remain NEXUS because `apps/api` was explicitly kept unchanged.
- Agent guidance now identifies FOPX PM as the product and apps/web-react (React/Vite/React Router) as the sole frontend. The retired Next app is excluded from build, deployment, and audit targets.
- Verification: React typecheck and production build passed; ESLint had no errors and one warning in untouched `ResourceCostProfilesSheet.tsx`; 142 focused React tests passed. Translation parity and used-key checks passed.
- The API build remains blocked by the current shared worktree error `CS0234` in `NotificationDeliveryProcessor.cs:3` (`NEXUS.Modules.Identity` unresolved). No API source change was made in this follow-up.
