# FOPX PM — Project Lifecycle Options 404 Investigation

**Date:** 2026-11-13  
**Status:** **PARTIALLY IDENTIFIED — the source narrows the likely branch, but the live failing branch is not confirmed.**  
**Scope:** Read-only diagnosis and remediation plan. No application code, database, migration, or runtime state was changed.

## Executive finding

The earlier claim that `ProjectPublicIdRoutingMiddleware` fails to convert the public project ID is not supported by the current source. The middleware resolves `Project.PublicId` within the authenticated tenant, rewrites `projectId` to `Project.Id`, stores both identifiers in the request, and runs exactly once before management-scope enforcement and MVC model binding. Existing HTTP integration coverage uses the external PublicId and expects this route to return 200.

The supplied response detail, `The requested project was not found.`, matches the lifecycle service's own `Error.NotFound` branches. The middleware and `ManagementContextMiddleware` use a different generic detail, `The requested resource was not found.` The lifecycle service has four predicates that can produce the supplied detail:

1. The internal-ID project lookup returns no row.
2. The project is missing from `ManagementContext.Scope.ProjectIds`.
3. The project-to-Organization lookup returns null.
4. A supplied `organizationId` conflicts with the resolved Organization.

For the exact URL provided, no `organizationId` query parameter is present, so predicate 4 does not fit. Under the checked-in pipeline, predicates 1 and 2 are also poor fits: the earlier public-ID middleware must already have found the same active project in the authenticated tenant, and the management middleware checks the same resolved internal ID against the same management scope before it calls the controller. Those earlier failures would return the generic resource 404. **The strongest source-based explanation is therefore that `IProjectOrganizationLookup.ResolveOrganizationIdAsync` returned null.** This remains an inference, not a confirmed production root cause, because the active API process, its loaded build, the request claims, and this project's ownership graph could not be inspected in this session.

## Evidence and provenance

### Supplied by the Product Owner

- Request: `GET /api/project-lifecycle/options?projectId=dc99079f-0a9c-4045-aacf-9a3f7a7f3afa`.
- Response: HTTP 404, code `NOT_FOUND`, detail `The requested project was not found.`, trace ID `0HNQ1D8GSO4ML:00000001`.
- Windows SQL evidence identifies `Shady` / `NexusPmo` and the row as `Project.PublicId=dc99079f-0a9c-4045-aacf-9a3f7a7f3afa`, `Project.Id=7abb23ff-07c8-4695-82fe-a885fe1b2225`, `TenantId=11111111-1111-1111-1111-111111111111`.
- At the time of this investigation, the supplied row did **not** include labeled values for `IsDeleted`, `ProgramId`, `PortfolioId`, `DepartmentId`, the owning Organization and its active/deleted state, the request's authenticated user/tenant claims, the effective management scope, or the API process's effective database target. The Product Owner later supplied an unlabelled 38-value row that conditionally suggests a null Portfolio Organization and empty Department joins; because it is short of the expected 40 columns and has no headers, it is not independent confirmation. See the [Project Ownership Integrity & Lifecycle Options 404 Recovery](PROJECT-OWNERSHIP-INTEGRITY-404-RECOVERY.md) for the current evidence and decision required.

### Verified in the current working tree

- API pipeline order: `Program.cs:813-823` runs authentication and permission claims before `ProjectPublicIdRoutingMiddleware` (`:819`), then `ManagementContextMiddleware` (`:820`), `ProjectResourceScopeMiddleware`, authorization, and controllers.
- Both middleware classes gate their detailed Development routing/scope logs on a `/api/projects/.../shell` request (`ProjectPublicIdRoutingMiddleware.cs:27,279-281`; `ManagementContextMiddleware.cs:22,284`). The lifecycle options URL does not match that diagnostic path, so those logs do not establish the PublicId rewrite or scope result for this request.
- The middleware only resolves authenticated project requests and requires a valid `tenantId` claim (`ProjectPublicIdRoutingMiddleware.cs:51-101`). It parses a single query `projectId` (`:123-133`), looks up **PublicId only** with matching tenant and `!IsDeleted` (`:212-218`), and does not fall back to internal IDs (`:220-223`). On success it rewrites the query to `Project.Id` and stores both IDs (`:240-261`).
- The controller binds `organizationId` and `projectId` from query and forwards them to `GetOptionsAsync` (`ProjectLifecycleCatalogController.cs:129-138`).
- The service checks permission and trusted management context first (`ProjectLifecycleCatalogService.cs:114-124`), then queries by `management.TenantId`, internal `Project.Id`, and `!IsDeleted` (`:129-137`). It next checks the scope and Organization resolution, returning the identical detail for the four conditions listed above (`:134-151`).
- `ManagementContextMiddleware` rejects a resolved project outside the current scope before the controller (`ManagementContextMiddleware.cs:197-227,267-271`); that middleware response uses the generic resource detail (`:418-425`).
- The Organization lookup loads a non-deleted project by internal ID and tenant, then resolves the owner through Program → Portfolio → Organization, Project → Portfolio → Organization, or Department → Business Unit → Organization (`ProjectOrganizationLookup.cs:14-61`). Program and Portfolio rows must match the tenant and not be deleted; Department, Business Unit, and the final Organization must also be active. Missing, deleted, cross-tenant, or absent rows can make this method return null.
- Production DI registers `IProjectOrganizationLookup` (`Program.cs:468`) and the Project Management module registers the lifecycle service (`ProjectManagementModuleRegistration.cs:12-20`). A missing registration is therefore not the leading source-level explanation.
- The middleware tests cover PublicId-to-Id query rewriting and reject an internal ID supplied as the external identifier (`ProjectPublicIdRoutingMiddlewareTests.cs:121-153`). The lifecycle HTTP integration test runs the middleware in production order and requests the endpoint with a PublicId (`ProjectLifecycleCatalogOptionsTests.cs:134-180`); it also tests an out-of-scope project and Organization mismatch (`:175-193`) and unresolved ownership (`:210-249`). These are fixture tests using EF InMemory and a mocked scope resolver, not observations of the reported SQL row or authenticated user.

## Root-cause assessment

| Hypothesis | Assessment | Evidence and limit |
|---|---|---|
| PublicId-to-Id conversion fails or converts twice | **Not supported by current source; low likelihood.** | Middleware uses PublicId, rewrites once, and is registered once. A middleware miss returns generic resource detail before the lifecycle service. The current runtime binary was unavailable, so source/runtime parity is unverified. |
| Project row is missing, soft-deleted, or in another tenant | **Possible only if runtime/source state differs, a race occurs, or the management tenant differs.** | The middleware must find a non-deleted same-tenant PublicId row before the controller is reached. The service's subsequent query uses that row's internal ID. The exact live tenant claim and database target were not captured. |
| Project is outside `Scope.ProjectIds` | **Poor fit to this response in the normal pipeline.** | `ManagementContextMiddleware` checks the resolved internal ID before controller execution and returns a different generic detail on a miss. The actual runtime scope and response path are still unobserved. |
| Organization lookup returns null | **Leading hypothesis; unconfirmed.** | This is the remaining service branch consistent with the supplied URL and exact detail if the current pipeline is running. Confirm the project's Program/Portfolio/Department ownership chain and each row's tenant, soft-delete, and required active status. |
| Supplied `organizationId` conflicts with the owner | **Does not fit the supplied URL.** | The exact request includes only `projectId`; verify the original request record to exclude an additional parameter or a different caller URL. |
| Separate bootstrap/authentication request returned 401 | **Unknown and separate from the reported 404.** | The browser session available to this investigation was `about:blank`; no authenticated network trace was available. The 404 trace does not establish whether another request received 401. |

One specific ownership edge deserves inspection before changing code: when the project has `ProgramId`, `ProjectOrganizationLookup` replaces its initially selected `Project.PortfolioId` with the Portfolio ID read from that Program (`:24-31`). If that Program cannot be resolved, the direct Project Portfolio is not retained for the next lookup. Whether this is a defect depends on the actual row and the canonical ownership rule; do not add a fallback until the intended ownership authority is confirmed.

## Runtime and database verification limits

- PID `43476`, reported earlier, is not present in the current environment. No listeners were observed on ports `4100`, `5000`, or `5001`; the available browser tab is blank. The process's loaded assembly, startup environment, and runtime logs could not be read.
- `vite.config.ts:48-54` configures the local Vite proxy to `https://localhost:5001`; `Properties/launchSettings.json` has a Development launch profile on `https://localhost:5001;http://localhost:5000`. These are launch/source settings, not proof of a currently running API or its effective environment.
- `Program.cs:452-456` configures SQL Server from `ConnectionStrings:DefaultConnection`, with a source fallback of `localhost` / `NexusPmo`. The current process configuration was unavailable. A `localhost` value cannot verify the connected SQL Server identity.
- The `Shady` / `NexusPmo` identity and Project identifiers above are treated as Product Owner-supplied SQL evidence; this investigation did not reconnect to SQL or independently verify that the API process used that target.
- The service's successful phase-options path calls `EnsurePreInitiationPhaseAsync` (`ProjectLifecycleCatalogService.cs:164-171`). That helper opens a serializable transaction and can change defaults, restore a deleted catalog entry, or insert one before saving (`:797-875`). Therefore an authenticated replay of this GET is **not a safe read-only diagnostic** against the live database. It was not replayed.
- The current working tree contains broad existing uncommitted changes (779 status entries at inspection). They were left intact except that this requested investigation file replaced its 12-byte `Test content` placeholder. No test, build, API request, SQL query, migration, or database write was run by this investigation.

## Read-only confirmation plan

1. **Correlate the original request, without replaying it.** Search the API/proxy logs for trace ID `0HNQ1D8GSO4ML:00000001`. Confirm method, path, final status/body, authenticated principal and tenant claim presence, query parameter names, API process/build identity, and whether this trace is the lifecycle request rather than a bootstrap call. Do not log or export bearer tokens, cookies, or connection strings.
2. **Verify the API target from the authorized Windows host.** Obtain the effective environment and connection source from the running API process/configuration without printing secrets. Use the repository's documented host-side Development preflight to verify live `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID. Compare the result with the supplied `Shady` / `NexusPmo` evidence before interpreting any row data. Do not infer identity from `localhost`, the Vite proxy, or a health response.
3. **Inspect the exact row and ownership chain read-only on that verified target.** For the supplied PublicId, record Project Id, TenantId, IsDeleted, ProgramId, PortfolioId, and DepartmentId; then inspect the matching Program/Portfolio or Department/BusinessUnit and final Organization for existence, tenant equality, soft-delete, and required active flags. Also capture whether the exact request contained `organizationId`.
4. **Resolve the actual actor scope using trusted server state.** From the same trace, identify the tenant/user claims and whether the resolved management context contains internal Project.Id `7abb23ff-07c8-4695-82fe-a885fe1b2225`. Do not infer authorization from a tenant-owned row or from `NOT_FOUND` alone.
5. **If the trace lacks those values, add narrowly scoped diagnostic logging only after separate implementation approval.** Log branch outcomes and non-secret IDs at the middleware/service boundary, with access-controlled retention. Avoid a live endpoint replay until the `EnsurePreInitiationPhaseAsync` write side effect has been isolated or the diagnostic is run against a disposable copy.

## Minimal remediation plan, conditional on evidence

- **If the Organization chain is missing or invalid in data:** correct the owning relationship through the approved project/portfolio/organization workflow after the authoritative owner is established. Preserve tenant boundaries and historical project identity. Do not grant access or bypass the Organization check.
- **If the Program/Portfolio edge is inconsistent:** first establish whether Program → Portfolio or Project → Portfolio is authoritative for projects assigned to Programs. Then make the smallest resolver correction and add a regression test for that exact ownership shape. Do not silently fall back across conflicting owner records.
- **If project lookup or scope differs between middleware and service:** compare the authenticated tenant, management context tenant, scoped DbContext, effective database target, and loaded API build for the same trace. Fix the proven configuration/context divergence; do not add another PublicId conversion or widen scope.
- **If a non-null `organizationId` is actually sent:** fix the caller to omit stale Organization context or send the owner's verified Organization ID; retain the server-side mismatch rejection.
- Preserve the external PublicId contract. Do not accept `Project.Id` as a fallback external identifier, disable authorization, or grant broader tenant/Organization scope.

## Focused verification after an approved fix

1. Add/adjust service tests that isolate each 404 predicate and verify that an unresolved/cross-tenant/deleted owner remains not found.
2. Keep an HTTP pipeline test that sends the external PublicId and asserts the controller/service receives the internal Project.Id; verify invalid, internal-ID, cross-tenant, out-of-scope, and Organization-mismatch requests remain rejected.
3. Add owner-lookup cases for Program → Portfolio → Organization, direct Project Portfolio → Organization, and Department → Business Unit → Organization, including missing/deleted/cross-tenant/inactive terminal ownership as applicable.
4. Run focused tests/build only after implementation is authorized. For runtime UAT, verify the API's exact Development target first and account for `EnsurePreInitiationPhaseAsync` before issuing the authenticated GET; do not classify that GET as read-only until its side effect is prevented or contained.

## Final disposition

**PARTIALLY IDENTIFIED.** Current source makes PublicId conversion failure an unsupported explanation and points most strongly to a null Organization resolution. The exact runtime branch and the project's ownership data remain unverified. No code or database changes were made; this report is the only file intentionally updated for the requested deliverable.
