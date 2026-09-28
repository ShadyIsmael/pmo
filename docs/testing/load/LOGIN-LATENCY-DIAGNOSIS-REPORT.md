# NEXUS Login Latency Diagnosis

Date: 2026-08-31  
Scope: `POST /api/auth/login` only  
Environment: local Production-style ASP.NET API, Release binaries, SQL Server `NexusPmo`, 93 users and 919 persisted user sessions at capture time.  
Load tool: k6 2.2.0  
Safety: one login per VU, no refresh calls, no writes other than the login session and existing best-effort `LastLoginAt` update.

## Executive conclusion

The high login tail is a capacity/queuing problem in the login pipeline, not a JWT or refresh-token generation problem and not a deterministic same-user row-contention problem.

The dominant contributors observed at 50–100 VUs were:

1. The login user query loads the user, roles, and permissions through multiple collection `Include`s in one EF query. On a cold/concurrent start, SQL Server showed `RESOURCE_SEMAPHORE_QUERY_COMPILE`; the measured lookup stage reached 2.6 seconds before the query was warm.
2. Password verification is synchronous PBKDF2 work. The seeded hashes are ASP.NET Identity V3 HMAC-SHA512 hashes with a 100,000-iteration work factor. At 100 VUs, password verification reached 937 ms p95 in the distinct-account sample.
3. Session persistence is a synchronous database insert/commit, followed by a separate best-effort `LastLoginAt` save. The session-save stage reached 1,368 ms p95 in the distinct-account 100-VU sample. SQL Server exposed `WRITELOG` during the active burst.

There were no HTTP failures in these post-fix auth-only runs, no blocked requests in the captured SQL DMV sample, and no evidence that the same account was slower because of the `Users.RowVersion` conflict after the earlier fix. The current degradation is therefore classified as **P1 — major capacity bottleneck**, with no P0 instability observed.

## Login path inspected

`DemoAuthService.LoginAsync` performs these operations:

| Stage | Implementation evidence | Measurement boundary |
|---|---|---|
| User lookup | `GetUserWithRolesByEmailAsync` | `Users` lookup plus eager role/permission graph materialization |
| Role/permission lookup | Same EF query via `UserRoles -> Role -> Permissions` | Not a separate query in the current login path |
| Password verification | `IPasswordHasher<User>.VerifyHashedPassword` | Synchronous hash verification |
| JWT generation | `GenerateJwtToken` | Token construction and signing |
| Refresh-token generation | `GenerateRefreshToken` and `HashToken` | Random token and SHA-256 hash |
| Session creation | `UserSession` add plus `SaveChangesAsync` | Insert/commit of the session row |
| LastLoginAt | `TryUpdateLastLoginAsync` | Separate best-effort `SaveChangesAsync` |

The temporary probes used for this diagnosis were removed before the final clean build. No application optimization was implemented in this task.

## Stage timings

Values are milliseconds from temporary Release-build probes. Each run had one login per VU; p50/p95/p99 are calculated over the login samples. The first cold 10-VU process-start sample is shown separately because it includes EF/SQL query warm-up.

### Same account

| VUs | Stage | Avg | p50 | p95 | p99 |
|---:|---|---:|---:|---:|---:|
| 10 warm | lookup + roles/perms | 145.94 | 146.97 | 150.40 | 150.40 |
| 10 warm | password | 129.18 | 132.11 | 149.76 | 149.76 |
| 10 warm | session | 10.25 | 9.28 | 18.51 | 18.51 |
| 10 warm | LastLoginAt | 7.48 | 7.03 | 14.52 | 14.52 |
| 10 warm | total | 292.85 | 292.89 | 303.00 | 303.00 |
| 25 | lookup + roles/perms | 267.79 | 104.28 | 600.47 | 614.51 |
| 25 | password | 303.33 | 270.94 | 472.37 | 476.78 |
| 25 | session | 114.86 | 132.04 | 245.58 | 332.41 |
| 25 | LastLoginAt | 15.92 | 11.90 | 48.68 | 53.22 |
| 25 | total | 701.90 | 700.33 | 801.29 | 803.18 |
| 50 | lookup + roles/perms | 387.07 | 265.19 | 759.72 | 792.79 |
| 50 | password | 303.29 | 299.91 | 397.39 | 416.77 |
| 50 | session | 138.79 | 86.86 | 380.35 | 412.92 |
| 50 | LastLoginAt | 55.57 | 30.07 | 168.07 | 201.67 |
| 50 | total | 884.71 | 916.35 | 978.56 | 993.86 |
| 100 | lookup + roles/perms | 393.75 | 285.85 | 921.31 | 967.41 |
| 100 | password | 262.15 | 212.13 | 470.18 | 493.45 |
| 100 | session | 215.82 | 134.26 | 602.71 | 647.96 |
| 100 | LastLoginAt | 78.09 | 37.16 | 272.90 | 733.80 |
| 100 | total | 949.82 | 987.00 | 1,295.81 | 1,300.95 |

The first cold 10-VU burst measured lookup p95 2,614.65 ms and total p95 2,986.60 ms. This is consistent with the concurrent query-compilation observation below, and is excluded from the warm 10-VU row.

### Distinct accounts

| VUs | Stage | Avg | p50 | p95 | p99 |
|---:|---|---:|---:|---:|---:|
| 10 | lookup + roles/perms | 5.06 | 3.37 | 13.01 | 13.01 |
| 10 | password | 219.17 | 220.37 | 259.01 | 259.01 |
| 10 | session | 13.51 | 8.68 | 31.01 | 31.01 |
| 10 | LastLoginAt | 7.09 | 2.91 | 19.29 | 19.29 |
| 10 | total | 244.83 | 238.64 | 267.89 | 267.89 |
| 25 | lookup + roles/perms | 485.91 | 482.93 | 868.69 | 911.38 |
| 25 | password | 85.46 | 84.66 | 92.47 | 93.56 |
| 25 | session | 364.26 | 365.80 | 719.12 | 760.60 |
| 25 | LastLoginAt | 35.36 | 38.36 | 47.53 | 48.33 |
| 25 | total | 970.99 | 971.50 | 995.88 | 1,009.79 |
| 50 | lookup + roles/perms | 438.78 | 470.64 | 756.52 | 762.98 |
| 50 | password | 186.37 | 183.48 | 239.79 | 255.25 |
| 50 | session | 112.41 | 115.54 | 237.60 | 390.54 |
| 50 | LastLoginAt | 48.60 | 11.57 | 203.79 | 221.42 |
| 50 | total | 786.16 | 790.67 | 898.13 | 905.78 |
| 100 | lookup + roles/perms | 573.52 | 626.85 | 1,384.00 | 1,510.89 |
| 100 | password | 536.16 | 489.71 | 937.25 | 986.43 |
| 100 | session | 586.27 | 506.31 | 1,367.72 | 1,470.59 |
| 100 | LastLoginAt | 136.32 | 99.84 | 532.61 | 553.85 |
| 100 | total | 1,832.27 | 1,694.44 | 2,519.15 | 2,555.31 |

The distinct-account samples are not monotonically identical across runs because each is a short one-shot burst and the local environment is shared. The important comparison is that distinct accounts are also slow at 100 VUs, while repeat 100-VU observations changed direction: one showed distinct p95 1,622 ms versus shared p95 1,978 ms. That rules out a deterministic same-user `Users` row as the explanation for the high tail.

## k6 endpoint metrics and validation

The apparent 100-VU inconsistency is a metric-population issue, not a bad threshold or tag:

| Full-flow run | Global `http_req_duration` p50 | Global p95 | Global p99 | Login-specific p50 | Login-specific p95 | Login-specific p99 |
|---|---:|---:|---:|---:|---:|---:|
| 50 VUs | 24.59 ms | 210.05 ms | 1,921.34 ms | 1,941.52 ms | 2,014.35 ms | 2,023.96 ms |
| 100 VUs | 4.04 ms | 38.21 ms | 4,940.26 ms | 5,074.14 ms | 5,257.25 ms | 5,278.67 ms |

At 100 VUs the full-flow run contained 100 login samples and 6,069 normal API samples. The global p95 is therefore dominated by the much faster normal API requests; the login-specific Trend contains only login requests and correctly reports the tail. The global p99 begins to include the login tail. `normal_api_duration` and `login_duration` are explicitly separate metrics in the load script.

In a 100-VU auth-only observation, k6 `http_req_waiting` averaged 1,412.39 ms against 1,413.20 ms total duration; blocked and connect time averaged only 2.21 ms and 1.37 ms. The delay is therefore server-side request waiting, not k6 connection setup.

## Database evidence

Read-only SQL Server checks found:

- `Users.Email` has the unique `IX_Users_Email` index, so the login lookup field is indexed.
- `UserSessions` has its primary-key index on `Id` and a non-unique index on `UserId`; there is no per-user uniqueness constraint that would reject concurrent sessions.
- All seeded login hashes use the `AQAAAAIAAYag...` ASP.NET Identity V3 format. Decoding the header identifies HMAC-SHA512 and 100,000 iterations.
- During one synchronized 100-VU burst, the API’s EF Core sessions showed 19 active requests at the sampled peak, maximum request elapsed time 2,121 ms, maximum wait 1,290 ms, aggregate request CPU 1,818 ms, and **0 blocked requests**. Wait types included `RESOURCE_SEMAPHORE_QUERY_COMPILE` and `WRITELOG`.
- No deadlock, lock-blocking chain, or SQL timeout was observed. The active requests drained after the burst.

The `RESOURCE_SEMAPHORE_QUERY_COMPILE` event aligns with the EF warning emitted for the login query: it loads more than one collection navigation (`UserRoles` and nested `Permissions`) using the default single-query behavior. This is the strongest evidence for the cold-start lookup spike. `WRITELOG` aligns with session insert/commit latency under the burst.

Connection-pool saturation was not directly observable because the runtime did not expose a pool counter in this environment. SQL Server showed pooled EF connections remaining idle after the run, but no pool timeout occurred; the evidence is insufficient to label the pool itself as exhausted.

## API process observations

During a synchronized 100-VU auth-only burst against the exact API PID:

- API process CPU sample average: 3.8% of the 14-logical-processor host; peak sample: 44.8% of host capacity.
- Peak API working set: approximately 272.6 MB.
- API OS thread count: 47–68.
- No direct CLR thread-pool-starvation or GC counter was available (`dotnet-counters` was not installed). The process thread count and k6 server-waiting time indicate scheduling/queueing, but do not prove thread-pool starvation.
- SQL Server OS CPU and the existing aggregate process monitor were not reliable enough in this environment to support a stronger database-CPU conclusion. SQL DMV request CPU/wait data above is the authoritative captured database evidence.

The non-saturated host-wide CPU reading does not make password verification free: `VerifyHashedPassword` is synchronous per request, and the measured password stage grows substantially in the high-load samples. It competes with request processing and contributes to queueing even without pegging total host CPU.

## Same-account versus distinct-account interpretation

k6 VUs use independent HTTP requests and do not share a mutable JWT or refresh token. The auth-only scenario performs one login per VU and makes no refresh request, so refresh-token rotation conflicts are not part of these results.

The earlier same-account failure was a real application defect: concurrent `LastLoginAt` updates collided on `Users.RowVersion` before session insertion. That defect is fixed. The current latency diagnosis is different:

- Same-account runs: 0% errors and high tail latency at 50–100 VUs.
- Distinct-account runs: 0% errors and comparable or higher high-load tail latency.
- Repeat 100-VU runs did not preserve a shared-account slowdown direction.

Conclusion: the current high tail is **not** a reproducible user-row concurrency limitation. It is a shared login-pipeline capacity issue involving query compilation/materialization, synchronous password verification, and database persistence/commit work.

## Degradation classification

| Finding | Classification | Likely source | Evidence |
|---|---|---|---|
| No failures, deadlocks, or instability | No P0 | — | 0% auth errors in all post-fix auth-only samples |
| Login p95 reaches ~2.0 s at 50 VUs and ~5.26 s in the original 100-VU full-flow run | P1 | Login pipeline / database / synchronous CPU | Login-specific k6 Trend, not global average |
| Cold concurrent login query compilation reaches ~2.6 s | P1 | Database query compilation and EF multi-collection graph loading | `RESOURCE_SEMAPHORE_QUERY_COMPILE`, lookup probe |
| Session commit reaches 1.37 s p95 at 100 distinct VUs | P1 | Database transaction log/persistence | `WRITELOG`, session-save probe |
| Password verification reaches 937 ms p95 at 100 distinct VUs | P1 | Authentication CPU work factor and request scheduling | Synchronous Identity password stage |
| JWT and refresh generation | P2/no material bottleneck | API CPU | Generally sub-millisecond to a few milliseconds |
| LastLoginAt best-effort save | P2 contributor | Database write path | Separate save, p95 up to 533 ms in distinct 100-VU sample |

## Recommended fixes, ranked by expected impact

These are recommendations only; none was implemented in this diagnosis.

1. **Highest impact: remove or reduce cold login-query compilation/materialization cost.** Measure a projection/compiled-query or equivalent query-shape change against the same role/permission response contract. Preserve authorization data while avoiding an unnecessarily expensive multi-collection single-query shape.
2. **High impact: profile and capacity-plan password verification.** Confirm the permitted Identity work factor against the production CPU budget, and isolate or limit concurrent hash work if needed. Any work-factor change requires security approval and must preserve password policy/business rules.
3. **High impact: profile the two login persistence saves.** Measure SQL duration and transaction-log latency for the `UserSessions` insert and best-effort `LastLoginAt` update separately, then address the confirmed database write bottleneck. Do not weaken `Users.RowVersion` protection.
4. **Follow-up: add runtime counters in the deployed environment.** Capture CLR thread-pool queue length, GC pauses, SQL connection-pool wait time, SQL CPU, and query duration during a repeatable warm and cold login burst before selecting a final optimization.

## Artifacts and raw results

- Auth-only script: `tests/load/auth-only.js`
- Full baseline script: `tests/load/nexus-baseline.js`
- Full-flow results: `tests/load/results/after-fix-50vus-20260831-105413.json`, `tests/load/results/after-fix-100vus-20260831-105650.json`
- Auth-only comparison results: `tests/load/results/auth-latency-shared-*.json`, `tests/load/results/auth-latency-distinct-*.json`
- Synchronized observations: `tests/load/results/auth-observed-distinct-100vus.json`, `tests/load/results/auth-observed-shared-100vus.json`, and `tests/load/results/auth-observed-dmv-distinct-100vus.json`

