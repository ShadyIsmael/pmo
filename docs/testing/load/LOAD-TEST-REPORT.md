# NEXUS API load-test baseline report

Date: 2026-08-31 (Africa/Cairo)  
Scope: authenticated, read-heavy backend/API traffic only  
Tool: k6 v2.2.0  
Target API: `http://localhost:5000`  
Target web: `http://localhost:4000` (not loaded; browser rendering was out of scope)  
Tenant: `11111111-1111-1111-1111-111111111111`

## Executive result

The baseline was stopped at 10 concurrent users. Two independent 2-minute 10-VU runs reproduced the same authentication failure pattern: 2 HTTP 5xx login responses out of 12 login attempts (16.66% login-failure rate per the k6 metric). The non-authenticated API flow had 0 failed requests and no timeout samples, but the repeated login 5xx behavior at the lowest requested load meets the stop condition for repeated 5xx errors.

25, 50, and 100 VU runs were intentionally not started. This report is therefore a partial baseline, not evidence that those levels are stable.

## Load model

Each VU logs in independently using a seeded demo identity, keeps its own access token, and performs this read-heavy sequence:

1. `POST /api/auth/login`
2. `GET /api/auth/me`
3. `GET /api/projects?page=1&pageSize=20`
4. `GET /api/projects/{dynamicProjectId}`
5. `GET /api/projects/{dynamicProjectId}/plans/bootstrap`
6. `GET /api/risks?page=1&pageSize=20`
7. `GET /api/risks/{dynamicRiskId}`
8. `GET /api/risks/{dynamicRiskId}/actions`

Project and risk IDs were selected from live response bodies. The test did not call refresh/logout and did not issue deletes, archives, approvals, status transitions, bulk writes, or test-data creation. Login itself updates the demo user’s last-login value and creates a session, as required by the existing authentication flow.

Think time was randomized between 1 and 3 seconds between user actions. Every endpoint was counted as successful only for a 2xx response; 4xx, 5xx, and timeout responses are failures.

## Environment recorded before the runs

| Item | Recorded value |
| --- | --- |
| API runtime | Already-running ASP.NET API, `Production` hosting environment, listening on `http://127.0.0.1:5000` |
| API execution mode | Release-style running process; no `dotnet watch` was used for the measured traffic |
| Repository revision | `0c2c8a1190907a4946eb889f972f9aa66068d20e` at measurement time; working tree contained pre-existing unrelated modifications |
| API artifact visible on disk | `apps/api/src/NEXUS.Api/bin/Release/net10.0/NEXUS.Api.dll`, timestamp 2026-08-24 13:49 local time; runtime-to-artifact mapping was not exposed by the running process |
| Database | SQL Server, database `NexusPmo`, configured via the local trusted-connection string |
| Redis | Local Redis configuration present; no Redis-specific pressure metric was available |
| Approximate data volume | 10 projects, 3 risks, 3 plans, 36 planning activities, 1 WBS node, 25 plan dependencies; 0 risk actions on the sampled risk |
| Credentials | Seeded demo accounts with the development password supplied via `K6_PASSWORD`; credentials are not stored in result files |

The API and database were reachable at preflight. The web endpoint also returned 200, but it was not included in the load test.

## Summary results

Times are milliseconds. RPS is k6 `http_reqs` rate. Error % is the test-wide `failed_requests` rate, which includes all request samples; login failures are separately called out because they are disproportionately important in this flow.

| Users | RPS | p50 | p95 | p99 | Error % |
| ----: | --: | --: | --: | --: | ------: |
| 10 — initial | 4.86 | 17.22 | 237.52 | 836.28 | 0.31% |
| 10 — confirmation | 4.65 | 11.75 | 242.35 | 687.56 | 0.32% |
| 25 | — | — | — | — | Not run — stop condition |
| 50 | — | — | — | — | Not run — stop condition |
| 100 | — | — | — | — | Not run — stop condition |

Normal API duration excluding login was below the 500 ms p95 target in both runs: 172.87 ms initially and 183.30 ms in confirmation. The global p99 is inflated by slow login samples and should not be interpreted as a normal API p99.

## Endpoint-specific results

### Initial 10-VU run

| Endpoint/group | Requests | p50 | p95 | p99 | Failures |
| --- | ---: | ---: | ---: | ---: | ---: |
| Login/Auth | 12 | 854.62 | 983.11 | 987.36 | 2 |
| Current user | 90 | 9.83 | 76.57 | 87.38 | 0 |
| Projects list | 90 | 10.94 | 39.21 | 53.16 | 0 |
| Project Details | 90 | 20.61 | 178.37 | 208.51 | 0 |
| Planning Bootstrap | 90 | 86.19 | 350.60 | 396.24 | 0 |
| Risks list | 90 | 19.41 | 111.56 | 172.87 | 0 |
| Risk Details | 90 | 8.57 | 36.04 | 77.14 | 0 |
| Risk Actions | 90 | 6.60 | 49.99 | 92.56 | 0 |

Run totals: 642 HTTP requests, 92 iterations, 2 HTTP 5xx, 0 HTTP 4xx, 0 timeouts, 0 non-auth API failures, and 2 login failures out of 12 login attempts (16.66%).

### Confirmation 10-VU run

| Endpoint/group | Requests | p50 | p95 | p99 | Failures |
| --- | ---: | ---: | ---: | ---: | ---: |
| Login/Auth | 12 | 654.20 | 693.16 | 698.47 | 2 |
| Current user | 87 | 5.42 | 45.14 | 154.49 | 0 |
| Projects list | 87 | 8.94 | 36.14 | 207.12 | 0 |
| Project Details | 87 | 15.50 | 137.11 | 350.63 | 0 |
| Planning Bootstrap | 87 | 84.72 | 635.88 | 1,100.00 | 0 |
| Risks list | 87 | 15.80 | 152.33 | 456.94 | 0 |
| Risk Details | 87 | 7.64 | 146.14 | 324.81 | 0 |
| Risk Actions | 87 | 5.02 | 34.18 | 113.90 | 0 |

Run totals: 621 HTTP requests, 89 iterations, 2 HTTP 5xx, 0 HTTP 4xx, 0 timeouts, 0 non-auth API failures, and 2 login failures out of 12 login attempts (16.66%).

The confirmation run also shows Planning Bootstrap as the heaviest normal endpoint: p95 635.88 ms and p99 1.10 s. This is a P2 observation in this run because it did not cause request failures and the test was stopped for the separate auth instability.

## Server observations

The lightweight collector ran alongside both measured runs and wrote CSV snapshots beside the k6 summaries.

| Observation | Initial 10 VU | Confirmation 10 VU | Interpretation |
| --- | ---: | ---: | --- |
| Aggregate dotnet working set maximum | 898.9 MB | 1,019.3 MB | Aggregate of 11 observed dotnet processes; API process could not be isolated with available host permissions |
| Aggregate dotnet CPU maximum | 2.01% | 4.80% | Low host CPU; aggregate process set changed during the second run, so use directionally only |
| SQL Server working set maximum | 247.4 MB | 209.0 MB | Process-level observation |
| SQL Server CPU maximum | 0.00% | 0.00% | Available process counter reported zero; not proof of zero SQL work |
| SQL Server user-connections counter | 0 reported | 0 reported | Counter was unavailable/not populated in this environment; do not interpret as actual connection count |
| Request queueing/query durations/exceptions | Not available | Not available | No additional observability infrastructure was installed or redesigned |

The available host observations do not indicate CPU or memory saturation. They also do not isolate the auth failure to a specific SQL query or connection-pool event. The most likely area is authentication/session persistence and its database interaction, but that is a hypothesis requiring application/database logs or tracing to confirm.

## Findings and classification

| ID | Severity | Finding | Likely source | Evidence |
| --- | --- | --- | --- | --- |
| F-001 | P0 — failures/instability | Repeated login 5xx responses at 10 VUs | Auth, with possible database/session persistence or connection contention | 2 login 5xx responses in each independent 2-minute run; 16.66% login-failure rate; k6 thresholds crossed |
| F-002 | P2 — optimization opportunity | Planning Bootstrap becomes heavy in the confirmation tail | Planning bootstrap and/or database query shape | p95 635.88 ms and p99 1.10 s at 10 VUs; 0 failures; no source-level change made |

No P1 capacity bottleneck was assigned from the measured evidence: the test did not reach a safe higher-concurrency level, normal API failures were zero, and available host CPU/memory observations were low. A P1 assessment should wait until the P0 auth issue is understood and the 25/50/100 runs can be completed safely.

## Stop decision

Higher stages were not run. The same auth/5xx failure reproduced at the lowest requested stage, and the available live logs did not expose the request exception needed to distinguish transient database contention from an auth-path defect. Continuing to 25, 50, or 100 VUs would knowingly increase load on an unstable authentication path and would not produce a clean capacity baseline.

## Saved evidence

- Harness: [`nexus-baseline.js`](../../../tests/load/nexus-baseline.js)
- Rerun instructions: [`README.md`](./README.md)
- Host collector: [`capture-server-metrics.ps1`](../../../tests/load/capture-server-metrics.ps1)
- Initial k6 summary: [`10vus-20260831-100928.json`](../../../tests/load/results/10vus-20260831-100928.json)
- Initial host observations: [`server-10vus-20260831-100928.csv`](../../../tests/load/results/server-10vus-20260831-100928.csv)
- Confirmation k6 summary: [`10vus-repeat-20260831-101249.json`](../../../tests/load/results/10vus-repeat-20260831-101249.json)
- Confirmation host observations: [`server-10vus-repeat-20260831-101249.csv`](../../../tests/load/results/server-10vus-repeat-20260831-101249.csv)

No application business logic, frontend code, or observability infrastructure was changed for this load-test task.
