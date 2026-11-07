# Login SQL Cancellation Diagnosis

Date: 2026-11-03  
Scope: `POST /api/auth/login` cancellation reported at `DemoAuthService.GetLoginUserByEmailAsync`  
Environment: Development API, SQL Server `Shady` / `NexusPmo`

## Finding

The exception is consistent with cancellation of an in-flight SQL command, not a login permission/session check, SQL command timeout, or a database error at the query itself. ASP.NET Core binds the `AuthController.Login` cancellation token to the HTTP request-aborted token; `DemoAuthService` passes it to EF Core's initial user lookup. EF Core passes that token to the SQL provider.

There is no API source mapping for HTTP 499, no configured ASP.NET request timeout, and no API-side `CancelAfter` for login. The React login call uses the unauthenticated API-client path, which adds neither an `AbortController` nor an application timeout. No SQL command timeout override is configured; Microsoft.Data.SqlClient's command timeout default is 30 seconds, so a 7.6-second abort does not match the default command timeout. EF Core passes async query cancellation tokens to its provider ([EF Core cancellation guidance](https://learn.microsoft.com/en-us/ef/core/querying/async), [SqlClient command timeout](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlcommand.commandtimeout?view=sqlclient-dotnet-core-6.1)). This means the observed 499 is being reported by a caller or intermediary outside the current API source. Which external component initiated the abort cannot be established from the available trace alone.

## Query and database evidence

`GetLoginUserByEmailAsync` first selects a bounded user projection from `Users`, filtered by exact email, explicit tenant, and `IsDeleted = 0`, with `AsNoTracking()` and `FirstOrDefaultAsync(cancellationToken)`. It does not load the role graph in this first query. Role and effective-permission reads follow only after the user row is found.

The actual Development target was verified read-only as `SERVERPROPERTY('ServerName') = Shady`, `DB_NAME() = NexusPmo`, database ID `5`. A parameterized execution of the matching first user lookup returned the existing Admin row in **141 ms**. A point-in-time `sys.dm_exec_requests` sample showed **0 running and 0 blocked requests**. These observations do not prove that no transient block occurred during the originally reported request, but they provide no evidence of a persistent slow lookup or blocking chain.

A separate read-only provider probe against this same target ran `WAITFOR` and canceled it with a `CancellationToken` after 250 ms. The API's `Microsoft.Data.SqlClient` version (6.1.1) produced `SqlException.Number = 0` with two errors, each number `0`, severity `11`, state `0`, matching the reported “severe error / Operation cancelled by user” shape. SqlClient's message is localized and has no dedicated cancellation error number ([SqlClient issue #2424](https://github.com/dotnet/SqlClient/issues/2424)), so the API identifies this cancellation by the provider error shape and the canceled request token rather than matching message text.

One live Development API probe with a randomly generated invalid credential returned the expected `401` in **1.389 seconds** and created no session. It exercised the live login path without using the Admin password. It is not evidence of a successful Admin login.

## Change

`ApiExceptionHandler` now handles `OperationCanceledException` and the narrowly identified SqlClient cancellation shape only when `RequestAborted` is canceled. It does not emit an unexpected API/database error for that expected abort or attempt to write to the disconnected response. A different `SqlException` is still logged as an error, even if the client disconnects at the same time; the handler simply skips writing an undeliverable response. No timeout was increased, no retries were added, and authentication policies were not changed.

## Development Admin constraint

The read-only account check found exactly one `admin@nexus.com` row in the demo tenant. It is active and local, has an existing password hash, and is marked `MustChangePassword = 1` with `TemporaryPasswordExpiresAt = NULL`. `DevelopmentAdminTemporaryPasswordBypassEnabled` is false. Under the current authentication rules, a correct password still cannot complete login in this state; enabling the Development-only bypass or changing account/password state would be required. Neither was done because this task forbids weakening authentication or changing user/password data.

Therefore a successful Development Admin login and authenticated `/api/auth/me` call could not be verified without violating the stated restrictions. No password, user, role, permission, database row, schema, migration history, or seed data was modified.

## Regression coverage

`AuthenticationPipelineTests` verifies that request-token cancellation and the reproduced SqlClient cancellation shape are not logged as unexpected failures, while a real SQL error remains error-logged after a client disconnect. Existing coverage still verifies a database exception produces a safe 500 response and invalid protected sessions are rejected.

Focused result: `AuthenticationPipelineTests` **8 passed, 0 failed**. The isolated build emitted existing nullable/analyzer warnings in unrelated tests and `ProgramService`; there were no build errors.
