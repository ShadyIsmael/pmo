# NEXUS Login Pipeline Optimization Report

Date: 2026-08-31  
Scope: `POST /api/auth/login` only  
Environment: Release-built ASP.NET API, Production-style settings, SQL Server database `NexusPmo`  
Load tool: k6 2.2.0  
Data: approximately 93 active seeded demo users; the prior captured database had approximately 919 `UserSessions` rows.  
Password: existing seeded development credentials supplied via `K6_PASSWORD`; no password policy or hash setting was changed.

## Status

The login pipeline change is implemented and the Release API build succeeds with 0 warnings and 0 errors. The focused post-change runs completed with 0 HTTP errors while the instrumented API process was active.

The post-change files named `distinct93` were initially invoked with `MODE=distinct`, but the script recognized `AUTH_MODE`; those runs were therefore same-account runs. The script now accepts both names. A clean rerun using true distinct accounts could not be completed because every newly launched API process began failing to connect to local SQL Server with `Microsoft.Data.SqlClient.SqlException: The target principal name is incorrect. Cannot generate SSPI context.` This occurs while opening the database connection, before the login query executes. No post-change distinct-account capacity result is claimed below.

## Local SQL SSPI investigation

- `MSSQLSERVER` is running as the default SQL Server instance from `MSSQL17.MSSQLSERVER`, using the virtual service account `NT Service\\MSSQLSERVER`, with TCP enabled on port 1433.
- The configured local connection is `Server=localhost;Database=NexusPmo;Trusted_Connection=True;...`; `localhost` resolves to both `::1` and `127.0.0.1`.
- The current user has no cached Kerberos ticket (`klist` reports no credentials cache). SPN queries could not reach the domain directory (`LDAP 0x51 / server down`), so no positive SPN registration evidence is available.
- Direct ODBC 17/18 `sqlcmd` attempts fail during the same local encrypted/integrated-auth handshake, while the API reports `Cannot generate SSPI context` before `GetLoginUserByEmailAsync` starts. This rules out the optimized login query as the cause.
- A local `MSSQLSERVER` restart was attempted but the agent account cannot open the service (`Cannot open 'MSSQLSERVER' service`). An administrator must restart the local SQL Server service or restore its Windows-auth/SPN state before the remaining validation can run.

### Post-restart continuation

After the SQL Server restart, `MSSQLSERVER` reports `RUNNING`, but direct ODBC 18 integrated-auth connectivity still fails before a query executes:

```text
Microsoft ODBC Driver 18 for SQL Server:
Encryption not supported on the client.
SSL Provider: No credentials are available in the security package.
Client unable to establish connection.
```

The API was not started and no post-restart load was run because the direct SQL health check did not pass. No Windows, SPN, security, connection-string, or application-code changes were made.

## Exact implementation

Changes are limited to `DemoAuthService` login support:

1. `GetLoginUserByEmailAsync` now uses `AsNoTracking` and a scalar user projection containing only the fields required for credential validation, the response, and token claims.
2. Authorization data is loaded through a second stable `AsNoTracking` projection from `UserRoles`. It returns role DTOs and permission strings, then de-duplicates permissions in memory. The login path no longer materializes the full `User -> UserRoles -> Role -> Permissions` tracked graph.
3. The projected data is held in a `LoginUserSnapshot`, which is used by password verification, JWT creation, and the login response. Password verification remains the existing ASP.NET Identity V3 hasher; seeded hashes are HMAC-SHA512 with 100,000 iterations.
4. Session creation remains authoritative: the session is added and one `SaveChangesAsync` commits it before a successful login response is returned.
5. `LastLoginAt` is updated after session commit using a direct `ExecuteUpdateAsync`, without tracking the user or participating in the session save. It is best-effort and catches database update failures, so it cannot turn a valid login into an HTTP 500.
6. Existing indexes were retained. `Users.Email` already has the unique `IX_Users_Email` index; `UserSessions.UserId` already has a non-unique index. No global rowversion change, lock, cache, background queue, or password-security reduction was introduced.

An initial single projection containing two nested collection projections was rejected because EF could not translate that shape at runtime. It was replaced with the two-query projection above.

## Post-change k6 results captured

These results are endpoint-only, one login per VU, 15 seconds, and 0% HTTP error rate. The 10/25/50/100 files in this section were invoked with the wrong distinct-mode variable and are consequently same-account results; they are useful as same-account behavior evidence, not as distinct-account capacity evidence.

| Users | RPS | p50 | p95 | p99 | Error % |
| ----: | --: | --: | --: | --: | ------: |
| 10 cold | 0.646 | 3,467 ms | 3,467 ms | 3,467 ms | 0.00% |
| 10 warm | 0.657 | 195 ms | 206 ms | 206 ms | 0.00% |
| 25 | 1.632 | 235 ms | 298 ms | 306 ms | 0.00% |
| 50 | 3.196 | 485 ms | 563 ms | 585 ms | 0.00% |
| 100 | 6.280 | 1,380 ms | 1,416 ms | 1,482 ms | 0.00% |

The previous diagnosis reported true distinct-account baseline p95 values of approximately 265 ms, 998 ms, 909 ms, and 2,591 ms at 10/25/50/100 VUs, but that fixture had only ten accounts and was not a clean high-load comparison. The corrected fixture contains 93 accounts and must be rerun after the SQL connection issue is recovered.

## Stage measurements

Temporary Release-build probes measured the following post-change request stages. Values are milliseconds; `n` is the number of successfully logged stage records. JWT and refresh-token generation were measured separately and remained negligible.

| Run | Stage | Avg | p50 | p95 | p99 |
|---|---|---:|---:|---:|---:|
| 10 cold | lookup | 2,589 | 2,557 | 2,728 | 2,728 |
| 10 cold | password | 143 | 139 | 168 | 168 |
| 10 cold | session persistence | 350 | 356 | 454 | 454 |
| 10 cold | LastLoginAt | 75 | 68 | 90 | 90 |
| 10 cold | total | 3,157 | 3,150 | 3,173 | 3,173 |
| 10 warm | lookup | 102 | 99 | 115 | 115 |
| 10 warm | password | 76 | 75 | 80 | 80 |
| 10 warm | session persistence | 6 | 4 | 11 | 11 |
| 10 warm | LastLoginAt | 2 | 1 | 6 | 6 |
| 10 warm | total | 185 | 181 | 194 | 194 |
| 25 | lookup | 79 | 83 | 199 | 205 |
| 25 | password | 99 | 97 | 113 | 125 |
| 25 | session persistence | 50 | 38 | 115 | 117 |
| 25 | LastLoginAt | 14 | 7 | 57 | 58 |
| 25 | total | 242 | 226 | 286 | 294 |
| 50 | lookup | 171 | 144 | 413 | 459 |
| 50 | password | 126 | 127 | 168 | 175 |
| 50 | session persistence | 88 | 61 | 265 | 286 |
| 50 | LastLoginAt | 22 | 13 | 41 | 213 |
| 50 | total | 407 | 466 | 544 | 572 |
| 100 | lookup | 596 | 537 | 1,199 | 1,329 |
| 100 | password | 142 | 137 | 207 | 230 |
| 100 | session persistence | 215 | 155 | 681 | 884 |
| 100 | LastLoginAt | 153 | 95 | 437 | 459 |
| 100 | total | 1,105 | 1,366 | 1,399 | 1,466 |

The cold 10-VU result includes first-use EF/SQL compilation. Warm lookup is materially lower, but high-load lookup and session persistence still queue behind shared database/request resources.

## Database and process observations

- Before the change, the active burst exposed `RESOURCE_SEMAPHORE_QUERY_COMPILE` and `WRITELOG`; no deadlocks were observed.
- The optimized warm path reduced graph materialization and uses a stable two-query shape. A clean post-change SQL DMV snapshot could not be taken after the API restart because local SQL integrated authentication began failing with SSPI before any query was issued. The earlier working optimized-process observation showed `WRITELOG`, no deadlock, and only a short blocking sample; it is not treated as a clean post-change DMV comparison.
- In the captured post-change 100-VU process, API CPU averaged approximately 3.5% of the 14-logical-processor host, working set was approximately 225 MB, and the process had 52 threads. These host-wide CPU values do not prove that password verification is free; the Identity V3 verification is synchronous and remains a meaningful per-request CPU stage.
- JWT and refresh-token generation were sub-millisecond in the stage sample. The remaining measured tail is lookup, session commit, and the awaited best-effort metadata update.

## Classification and next actions

No application P0 was observed in the completed post-change runs: all of those requests returned 2xx. The current capacity finding remains P1 because warm 100-VU same-account login p95 was approximately 1.4 seconds and the corrected distinct-account run is still required.

The next measurement should be run after recovering the local SQL Server integrated-auth state:

```powershell
k6 run --vus 10 --duration 15s --summary-export .\tests\load\results\auth-optimized-distinct93-10vus-final.json .\tests\load\auth-only.js -e AUTH_MODE=distinct
k6 run --vus 25 --duration 15s --summary-export .\tests\load\results\auth-optimized-distinct93-25vus-final.json .\tests\load\auth-only.js -e AUTH_MODE=distinct
k6 run --vus 50 --duration 15s --summary-export .\tests\load\results\auth-optimized-distinct93-50vus-final.json .\tests\load\auth-only.js -e AUTH_MODE=distinct
k6 run --vus 100 --duration 15s --summary-export .\tests\load\results\auth-optimized-distinct93-100vus-final.json .\tests\load\auth-only.js -e AUTH_MODE=distinct
```

Do not alter password hashing parameters or claim a distinct-account improvement until those four runs and a short same-account check complete against a working SQL connection.
