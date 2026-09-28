# NEXUS API load-test report after concurrent-login fix

Date: 2026-08-31 (Africa/Cairo)  
Scope: authenticated, read-heavy backend/API traffic only  
Tool: k6 v2.2.0  
API: `http://localhost:5000`  
Tenant: `11111111-1111-1111-1111-111111111111`  
Database: local SQL Server `NexusPmo`  
Maximum load: 100 VUs

## Code change

Changed only `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DemoAuthService.cs`:

- `IssueSessionAsync` now runs before the `LastLoginAt` update, so session/token persistence is authoritative.
- `LastLoginAt` is updated by a small best-effort helper after the session save.
- Only `DbUpdateConcurrencyException` is caught locally.
- The stale tracked `User` entity is detached after that conflict so it cannot affect later persistence.
- `User.RowVersion` remains enabled; JWT creation, session schema, refresh-token behavior, credential validation, tenant handling, and response shape are unchanged.

No frontend changes, tests, business-rule changes, or refresh/session-rule changes were made.

## Same-account verification

Before the fix, 10 same-account VUs reproduced 9/10 HTTP 500s with `DbUpdateConcurrencyException` on `LoginAsync` line 43. After the fix:

| Scenario | VUs | Login requests | HTTP 2xx | HTTP 5xx | Error rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| Same `admin@nexus.com` account | 10 | 10 | 10 | 0 | 0% |

The counted verification read `UserSessions` before and after the run: 224 -> 234, exactly ten new persisted sessions. No refresh calls were made and no refresh-token/session conflicts were observed.

Artifact: `tests/load/results/auth-after-fix-shared-counted-20260831-104853.json`.

## Baseline results after the fix

Each stage was an independent 2-minute run using the existing dynamic, read-heavy flow. All checks passed, all login attempts succeeded, and no HTTP 4xx/5xx or timeout samples were recorded.

Times are milliseconds. p50 is k6 `med`. Error % is the overall failed-request rate.

| Users | RPS | p50 | p95 | p99 | Error % |
| ----: | --: | --: | --: | --: | ------: |
| 10 | 4.68 | 29.91 | 163.23 | 664.47 | 0% |
| 25 | 11.98 | 24.97 | 154.58 | 774.29 | 0% |
| 50 | 22.79 | 24.59 | 210.05 | 1,921.34 | 0% |
| 100 | 45.51 | 4.04 | 38.21 | 4,940.26 | 0% |

Normal API p95 excluding login remained below the 500 ms target at every level:

| Users | Normal API p95 | Normal API p99 | Login failures | HTTP 5xx | Timeouts |
| ----: | --------------: | --------------: | -------------: | --------: | -------: |
| 10 | 151.86 ms | 207.51 ms | 0/10 | 0 | 0 |
| 25 | 144.44 ms | 173.69 ms | 0/25 | 0 | 0 |
| 50 | 189.42 ms | 298.42 ms | 0/50 | 0 | 0 |
| 100 | 32.70 ms | 60.65 ms | 0/100 | 0 | 0 |

The global p99 at 50 and 100 VUs is dominated by login latency, not normal API traffic.

## Endpoint-specific results

All endpoint groups had zero failures at all stages.

### 10 VUs

| Endpoint/group | Requests | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: |
| Login/Auth | 10 | 668.52 | 714.45 | 723.52 |
| Current user | 88 | 28.90 | 40.57 | 121.77 |
| Projects list | 88 | 17.58 | 24.22 | 62.71 |
| Project Details | 88 | 69.71 | 98.46 | 304.95 |
| Planning Bootstrap | 88 | 142.72 | 175.99 | 282.03 |
| Risks list | 88 | 47.22 | 62.84 | 96.27 |
| Risk Details | 88 | 15.91 | 22.94 | 44.93 |
| Risk Actions | 88 | 23.38 | 32.00 | 53.59 |

### 25 VUs

| Endpoint/group | Requests | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: |
| Login/Auth | 25 | 782.10 | 807.07 | 807.33 |
| Current user | 220 | 25.91 | 40.02 | 65.29 |
| Projects list | 220 | 16.00 | 27.41 | 49.69 |
| Project Details | 220 | 62.31 | 97.86 | 151.70 |
| Planning Bootstrap | 220 | 132.34 | 182.88 | 217.86 |
| Risks list | 220 | 43.58 | 66.81 | 93.65 |
| Risk Details | 220 | 12.18 | 22.12 | 33.57 |
| Risk Actions | 220 | 18.02 | 32.08 | 50.00 |

### 50 VUs

| Endpoint/group | Requests | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: |
| Login/Auth | 50 | 1,941.52 | 2,014.35 | 2,023.96 |
| Current user | 438 | 26.47 | 73.73 | 171.36 |
| Projects list | 438 | 16.04 | 31.81 | 46.87 |
| Project Details | 438 | 28.02 | 143.84 | 215.61 |
| Planning Bootstrap | 438 | 135.90 | 299.48 | 394.26 |
| Risks list | 438 | 51.31 | 115.40 | 263.29 |
| Risk Details | 438 | 13.74 | 32.37 | 127.30 |
| Risk Actions | 438 | 16.01 | 45.47 | 93.89 |

### 100 VUs

| Endpoint/group | Requests | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: |
| Login/Auth | 100 | 5,074.14 | 5,257.25 | 5,278.67 |
| Current user | 867 | 3.03 | 13.31 | 34.94 |
| Projects list | 867 | 3.58 | 12.21 | 30.30 |
| Project Details | 867 | 6.32 | 22.84 | 44.87 |
| Planning Bootstrap | 867 | 28.32 | 65.55 | 138.59 |
| Risks list | 867 | 5.32 | 15.33 | 29.15 |
| Risk Details | 867 | 2.52 | 6.12 | 14.99 |
| Risk Actions | 867 | 2.03 | 5.49 | 14.20 |

## Bottleneck and severity assessment

### P0: resolved authentication failure

The previous P0 failure mode—valid concurrent same-account logins returning HTTP 500—was not observed after the change. The fix preserves optimistic concurrency protection and prevents a non-critical audit write from blocking a committed session.

### P1: login latency at 50 and 100 VUs

Login is the clear remaining heavy endpoint:

- 10 VUs: p95 714 ms
- 25 VUs: p95 807 ms
- 50 VUs: p95 2,014 ms
- 100 VUs: p95 5,257 ms

This exceeds the normal-API target and drives the global p99 at higher loads. It is classified as a P1 capacity bottleneck in the authentication path. The likely source is the auth database flow: the login query loads multiple collection navigations (`UserRoles -> Role -> Permissions`), password verification is performed per request, and each login still performs a metadata save after the session commit. The measured evidence does not isolate the relative contribution of database query shape, password hashing, or connection wait time.

No new HTTP failure instability, sustained 5xx, timeout, or normal-API p95 breach was observed.

### P2: multi-collection auth query

EF Core continues to warn that the login query loads multiple collection navigations as a single query. This was not changed in this task. It is a separate optimization opportunity and should be profiled before any change.

## Host observations

The lightweight CSV collector ran beside every stage:

- `server-after-fix-10vus.csv`
- `server-after-fix-25vus.csv`
- `server-after-fix-50vus.csv`
- `server-after-fix-100vus.csv`

The collector’s SQL user-connection counter reported `0`/unavailable, and the host process enumeration included hundreds of `dotnet` processes in this execution environment. Therefore aggregate dotnet memory/CPU values are not reliable API-process measurements. SQL DMV-level blocking/query-duration/pool data was not available from the permitted execution context. No application or database observability infrastructure was added.

## Raw result artifacts

- `tests/load/results/after-fix-10vus-20260831-104853.json`
- `tests/load/results/after-fix-25vus-20260831-105135.json`
- `tests/load/results/after-fix-50vus-20260831-105413.json`
- `tests/load/results/after-fix-100vus-20260831-105650.json`
- `tests/load/results/auth-after-fix-shared-counted-20260831-104853.json`

All requested stages were reached. No further load beyond 100 VUs was run.
