# NEXUS authentication 5xx diagnosis

Date: 2026-08-31  
Scope: `POST /api/auth/login` only  
Maximum load used: 10 VUs  
Application code changed: none

## Executive conclusion

The live API 500s are caused by an unhandled Entity Framework optimistic-concurrency conflict on the shared `Users` row, not by refresh-token rotation, duplicate session inserts, SQL deadlocks, connection-pool exhaustion, or invalid credentials.

`DemoAuthService.LoginAsync` loads the user, sets `user.LastLoginAt`, and calls `SaveChangesAsync`. The `Users.RowVersion` property is configured as a SQL Server rowversion concurrency token. When multiple VUs log in as the same account, they read the same original rowversion; the first update succeeds and the remaining updates affect zero rows. EF then raises `DbUpdateConcurrencyException`, which is not handled by the login controller and becomes HTTP 500.

This is classified as an **application bug** in the authentication path, with a **same-account session/data-model amplification** in the load shape. It is a real defect for concurrent logins to the same account, although it does not mean that independent users necessarily collide.

## Exact evidence

Failed request example captured from k6:

```text
POST http://localhost:5000/api/auth/login
mode=shared, VU=8, status=500, error_code=1500
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
The database operation was expected to affect 1 row(s), but actually affected 0 row(s);
data may have been modified or deleted since entities were loaded.
```

The stack trace identifies:

```text
Microsoft.EntityFrameworkCore.Update.AffectedCountModificationCommandBatch.ThrowAggregateUpdateConcurrencyExceptionAsync(...)
Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(...)
NEXUS.Modules.Identity.Infrastructure.DemoAuthService.LoginAsync(...): line 43
NEXUS.Modules.Identity.Api.AuthController.Login(...): line 22
```

The relevant source flow is:

```text
DemoAuthService.LoginAsync:30  GetUserWithRolesByEmailAsync
DemoAuthService.LoginAsync:42  user.LastLoginAt = DateTime.UtcNow
DemoAuthService.LoginAsync:43  await _context.SaveChangesAsync(...)
DemoAuthService.LoginAsync:45  IssueSessionAsync(...)       [not reached by failed requests]
```

The failed database operation is therefore the tracked `Users` update equivalent to:

```sql
UPDATE Users
SET LastLoginAt = @newLastLoginAt, UpdatedAt = @updatedAt, ...
WHERE Id = @userId AND RowVersion = @originalRowVersion;
```

The affected-row count is zero for stale concurrent copies of the user. This is the expected EF Core symptom of a rowversion conflict.

## Controlled reproduction

The auth-only scenario is `tests/load/auth-only.js`. It performs one login per VU, does not call refresh, and does not call any project/risk endpoint.

| Scenario | VUs | Duration | Login requests | HTTP 2xx | HTTP 5xx | Error % | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Same account (`admin@nexus.com`) | 10 | 5s | 10 | 1 | 9 | 90% | Reproduced rowversion exception |
| Same account (`admin@nexus.com`) | 10 | 15s | 1 per VU | 2 | 8 | 80% | Reproduced rowversion exception |
| Ten distinct seeded accounts | 10 | 5s | 10 | 10 | 0 | 0% | No login failures |
| Ten distinct seeded accounts | 10 | 15s | 10 | 9 | 1 | 10% | One transient 500 in this short run; no shared-account row collision observed |

The most diagnostic artifacts are:

- `tests/load/results/auth-diagnosis-live-shared-stack-20260831-102743.json`
- `tests/load/results/auth-diagnosis-live-distinct-stack-20260831-102753.json`
- `tests/load/results/auth-diagnosis-live-shared-detailed-20260831-102716.json`
- `tests/load/results/server-auth-shared-diagnosis.csv`

The prior two-minute 10-VU full-flow runs also produced two login 5xx responses per run, while normal API p95 stayed below 500 ms. The full-flow script used independent per-VU tokens and did not refresh them, so the observed login failure is not a shared mutable JWT or refresh-token conflict.

## Authentication/database flow review

### User lookup

`GetUserWithRolesByEmailAsync` uses a tracked query with:

```text
User -> UserRoles -> Role -> Permissions
```

This produces EF warning 20504 about multiple collection includes and can make login heavier, but it is not the cause of the live HTTP 500 after the logging/configuration issue was isolated. The failed live stack terminates at the first `SaveChangesAsync`, not at query materialization.

### User update

`UserConfiguration` marks `User.RowVersion` as `IsRowVersion()`. Every concurrent successful read of the same user gets the same original rowversion. The login path writes `LastLoginAt` on every successful password check and does not catch or retry `DbUpdateConcurrencyException`.

### Session insert

`IssueSessionAsync` creates a new `UserSession` with `Guid.NewGuid()` and inserts it after the `LastLoginAt` save. `UserSessions` has a primary key on `Id` and a non-unique index on `UserId`; the migration shows no unique constraint that would reject multiple sessions for one user. The failing requests do not reach this insert, so the evidence does not support duplicate session rows or refresh-token rotation as the cause.

### DbContext lifetime

`AppDbContext` is registered scoped, `DemoAuthService` is scoped, and the controller receives the request-scoped service. There is no evidence of one `DbContext` instance being shared between k6 VUs. The conflict is between independent contexts updating the same database row, which is precisely what the rowversion is designed to detect.

## Same-account versus distinct-account interpretation

The k6 VUs use independent credentials/tokens. In shared mode they intentionally use the same identity, not the same mutable token or refresh session. Refresh is disabled in this reproduction, so refresh-token rotation cannot be involved.

The sharp difference between shared and distinct modes distinguishes:

- **Not merely artificial token sharing:** each VU owns its request and would own its returned token.
- **A real same-identity concurrency defect:** simultaneous login attempts for one account race on `Users.RowVersion`.
- **A workload amplification:** a normal population of independent users is represented better by distinct mode and does not hit the same row for `LastLoginAt`.

## Pressure and stability observations

No timeout counter incremented in the successful-configuration live runs. The shared-account failures were HTTP 500 responses with the exception body, not network timeouts or 4xx validation responses.

The lightweight host collector recorded no meaningful SQL CPU increase and no available SQL user-connection value. Its SQL Server performance-counter path returned zero/unavailable in this environment, so it cannot be treated as pool telemetry. The collector also saw an environment-level process enumeration anomaly (140 `dotnet` processes in the short capture), making its aggregate dotnet CPU/memory values unsuitable for attributing pressure to the API process.

The earlier full-flow host samples were low pressure: aggregate dotnet CPU peaked at approximately 4.8%, SQL Server CPU at 0%, and no sustained timeout signal was observed. There is no evidence in the captured runs of SQL blocking, deadlocks, request-queue saturation, or connection-pool exhaustion. Direct SQL DMV inspection was blocked by the execution environment’s Windows authentication/SSPI context (`Cannot generate SSPI context`), so SQL-side query-duration and blocking data were not available.

## Configuration caveat discovered during diagnostics

Starting the existing Release DLL in an isolated process exposed two environment issues unrelated to the live 500 root cause:

1. Release `appsettings.json` has a trusted SQL connection string without `Encrypt=False`; the local SQL client reported that encryption was required but unsupported.
2. The Windows Event Log logging provider attempted to write the EF multiple-collection-include warning and failed with `Access is denied` for source `.NET Runtime`.

Those issues caused isolated-process failures/connection aborts before the application could reach the concurrency test. The live API process on port 5000 was used for the definitive reproduction, and its response body exposed the distinct `DbUpdateConcurrencyException` at `DemoAuthService.LoginAsync` line 43.

## Root-cause classification

| Finding | Classification | Likely source | Priority |
| --- | --- | --- | --- |
| Concurrent same-account login updates stale `Users.RowVersion` and returns HTTP 500 | Application bug | Auth/user update | P0 for affected concurrent-login requests; P1 capacity concern if common in production |
| Same-account VUs amplify one shared user-row conflict | Load-test data/workload issue | Test identity distribution | P2 test-model caveat; not the exception itself |
| Multi-collection include increases login query cost | Optimization opportunity | Auth database query / planning | P2 |
| Release isolated process cannot authenticate to local SQL without runtime connection override | Environment/configuration issue | SQL encryption/Windows SSPI configuration | P1 for that launch mode; not the live API 500 cause |
| Event Log provider lacks permission | Environment/configuration issue | Logging sink permissions | P1 observability/runtime reliability issue |

The P0 label is limited to the observed behavior: an ordinary login request becomes an unhandled 500 under concurrent same-account use. No environment crash or sustained API/DB instability occurred.

## Recommended fix (not implemented)

The login path should not allow the best-effort `LastLoginAt` write to turn an otherwise valid authentication into an unhandled 500. Candidate approaches for the implementation task are:

- make the last-login update concurrency-safe and retry/reload on a rowversion conflict;
- make the timestamp update non-blocking/best-effort relative to issuing the session; or
- otherwise separate authentication/session issuance from the shared mutable user-profile update.

The chosen approach must preserve the intended audit semantics and should be validated with both concurrent same-account and independent-account login scenarios. The multiple-collection include and runtime logging/SQL configuration should be handled as separate follow-up work; no fix was applied here.

Expected effect of the auth fix: concurrent valid logins should return 2xx and create independent sessions instead of surfacing `DbUpdateConcurrencyException` as HTTP 500. It should not require refresh-token changes for this reproduction.

## Commands to reproduce

From `C:\Projects\pmo`:

```powershell
& 'C:\Program Files\k6\k6.exe' run --vus 10 --duration 5s `
  -e BASE_URL=http://localhost:5000 `
  -e AUTH_MODE=shared `
  -e AUTH_ONCE_PER_VU=true `
  --summary-export .\tests\load\results\auth-shared-10vus.json `
  .\tests\load\auth-only.js

& 'C:\Program Files\k6\k6.exe' run --vus 10 --duration 5s `
  -e BASE_URL=http://localhost:5000 `
  -e AUTH_MODE=distinct `
  -e AUTH_ONCE_PER_VU=true `
  --summary-export .\tests\load\results\auth-distinct-10vus.json `
  .\tests\load\auth-only.js
```

Do not advance to 25, 50, or 100 VUs until the login 500 is addressed or explicitly accepted as a known same-account limitation.
