# NEXUS React/Vite parity gate

This document records the migration gate for `apps/web-react`. It is intentionally kept
next to the repository so a production cutover cannot be mistaken for a route-only port.

## Gate rule

The Next.js application remains the production canonical application until every active
surface below is either parity-complete or explicitly accepted as deferred by product,
and manual UAT plus the final merge/cutover approval are complete.

## Current audit

| Surface | React route | Status | Evidence / remaining work |
| --- | --- | --- | --- |
| Authentication, tenant context, management scope, read-only mode | `/login`, authenticated shell | Complete for migration scope | Auth refresh, management headers, permission guards and EN/AR shell are covered by React tests. |
| Home, projects, core project tabs | `/home`, `/projects`, `/projects/:id/*` | Partial | Core visible-first pages exist; planning, board/matrix/gantt and snapshots still need feature parity. |
| Planning / Gantt / baselines / dependencies / import | `/projects/:id/planning` | Blocked | React currently lists plans; Next has the full planning workspace. |
| Project workflows / visual designer | `/projects/:id/workflows`, `/workflows/*` | Partial | React renders definitions and a graph; state/transition/guard/action editing and preview are not complete. |
| My Work tasks, approvals, timesheets | `/my-work/*` | Complete for migrated scope | Existing React pages and API-backed queries are present. |
| Resources, vendors, teams, capacity | `/resources*` | Partial | Directory and core views exist; deeper analytics and management interactions require parity review. |
| Meetings and minutes collaboration | `/meetings*` | Partial | List/detail exists; minutes editor, checklist, comments and attachments remain to be ported. |
| Work items | `/work-items*` | Partial | List/detail exists; comments, attachments, activity, assignment and workflow actions remain to be ported. |
| Portfolios and programs | `/portfolios*` | Partial | Basic list/detail exists; full management views and handovers need parity review. |
| Risks, issues and finance | `/risks-issues/*`, `/costs/*` | Partial | Basic React views exist; Next dashboards and specialized workflows need parity review. |
| Organization hierarchy | `/administration/organization` | Partial | Tenant virtual root, CRUD, filtering and on-demand detail data exist; reporting view and persisted drag/reorder remain. |
| Administration, account, notifications, FCM, timer | `/administration/*`, `/account/*`, `/notifications` | Complete for migrated scope | API-backed notification center, device service worker placeholders, timer and permission-aware navigation are present. |
| Stakeholder portal | `/stakeholder/*` | Partial | Invitation and basic project views exist; full stakeholder workflow parity remains. |
| Reports, AI workspace, security audit, tickets, test management | matching deep links | Deferred / blocked | React keeps deep links safe with explicit deferred pages; Next remains canonical until product accepts a migration scope. |

## Required before cutover

1. Port the partial and blocked active surfaces above, or obtain explicit product acceptance
   for each deferred surface.
2. Run the full validation set: i18n parity, used-key validation, typecheck, lint,
   focused navigation/feature tests, production build, and API build.
3. Complete manual UAT and approve the final merge/cutover. This repository must not remove
   `apps/web` or switch the canonical production service before that approval.

## Production-safe React preview

`apps/web-react/Dockerfile` and `apps/web-react/nginx.conf` provide an optional static SPA
container. The nginx configuration proxies `/api/*` to the API service and only applies
the `index.html` fallback to non-API paths. The existing Next service remains unchanged.
