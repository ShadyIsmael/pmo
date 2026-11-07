# System action inventory

**Snapshot date:** 2026-11-02. This is a static inventory of the supported source tree, not runtime traffic or a semantic business-action census.

## Inventory counts

| Surface | Static count | Interpretation |
|---|---:|---|
| API C# source files | 855 | `.cs` files under `apps/api/src`, excluding build output. |
| HTTP method attributes | 602 | GET 268, PUT 84, POST 194, DELETE 48, PATCH 8; each source attribute is one row in `API-ENDPOINT-INVENTORY.csv`. |
| React TS/JS source files | 408 | `.tsx`, `.ts`, `.jsx`, `.js` under `apps/web-react/src`. |
| React route declarations | 130 | `<Route>` declarations in `apps/web-react/src/routes/AppRoutes.tsx`; declarations can be nested or wildcard routes. |
| Selected UI callback/event tokens | 1,774 | `onClick` 894, `onSubmit` 107, `onChange` 720, `onKeyDown` 42, `onDrop` 3, `draggable` 8. Repeated and non-business callbacks are included. |
| API module directories | 21 | Directories matching `apps/api/src/NEXUS.Modules.*`. |

The HTTP endpoint inventory includes the added `GET /api/projects/{projectId}/status-reports/{reportId}/export` endpoint. Its policy is `StatusReport.Export`. It also records the controller-level `Stakeholders.Manage` invitation policy and public one-time-token invitation acceptance/consent endpoints as self-service exceptions.

## Raw inventory fields

`API-ENDPOINT-INVENTORY.csv` records HTTP verb, controller/class, method name when statically recoverable, route template, nearby authorization attributes, and file/line. Class-level policies are now recorded for Issue and Plugin reads. A blank or nearby-only policy annotation is not proof that no service or inherited authorization exists.

`UI-HANDLER-SITES.csv` remains a candidate list of event-token occurrences and source lines. Route guards and permission checks are recorded in the existing action matrix; a click, input change, or drag event is not automatically a distinct permission-bearing action.

## High-level UI/API trace findings

| User-facing surface | Current source enforcement | Audit status |
|---|---|---|
| Projects | `Project.View` route/navigation guard and API policy | Canonical key aligned. |
| Issues | `Issue.View` API policy; tenant and assigned project scope fail closed | Source gap closed; runtime scope tests remain open. |
| AI workspace | `Planning.View` route guard; operation actions remain separate | Source gap closed. |
| Security audit | `Audit.View` route guard | Source gap closed. |
| Tickets and test management | `WorkItem.View` route guard | Source gap closed; confirm screen semantics in UAT. |
| Integrations | `Settings.View` generic route; exact Directory action policies | Source gap closed. |
| Plugins | `Plugin.View` route/API policy; tenant-filtered reads | Source gap closed; no global authority inferred. |
| Status report export | Server endpoint requires `StatusReport.Export` and exports Published reports only | Source gap closed. |
| Initial annual budget draft | `ProjectAnnualBudget.Create`, separate from `Project.Create` | Source gap closed. |
| Stakeholder list and administration | `Stakeholders.Read` for reads; `Stakeholders.Manage` for writes and invitation administration, with tenant/project management scope rechecked in services | Source checks mapped; runtime grant/deny tests pending. |

## Coverage statement

The matrix has 202 rows: 154 active catalog capabilities, 15 additional source action/surface/exception rows, 32 retired/history rows, and 1 documentation count row. Six rows explicitly record public token-based invitation and authenticated own-assignment stakeholder self-service exceptions. This is not a deduplicated semantic business-action denominator, so no percentage is reported. Material actions still require tracing through route, API, service, tenant boundary, resource scope, and workflow assignment. See `ACTION-PERMISSION-MATRIX.csv` and `PERMISSION-GAP-REPORT.md` for the existing audit records and remaining runtime gaps.
