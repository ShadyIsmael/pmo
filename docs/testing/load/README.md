# NEXUS API baseline load test

This directory contains the read-heavy k6 baseline requested for the NEXUS backend. It exercises backend APIs only; it does not load the web app or measure browser rendering.

## Preconditions

- API is available at `http://localhost:5000`.
- The demo tenant is `11111111-1111-1111-1111-111111111111`.
- The database contains seeded data and at least one project. The script selects project and risk IDs from live list responses.
- Use a Release-built API process and a production-style configuration where possible. Do not use `dotnet watch` for the measured run.
- k6 is installed and available on PATH.

The seeded demo users share one development password, defined in `SeedDataProvider`. Supply it to the test through `K6_PASSWORD`; the scripts refuse to start without it. By default, VUs rotate across the seeded demo/admin identities in `nexus-baseline.js`. Each VU logs in independently and keeps its own access token. The test does not call refresh or logout, avoiding artificial refresh-token rotation conflicts during a short baseline.

## Install k6 on Windows

Preferred package-manager setup:

```powershell
winget install --id Grafana.k6 -e
```

If WinGet is unavailable, use the official k6 Windows installer or Chocolatey package, then open a new terminal and verify:

```powershell
k6 version
```

Do not add k6 to the application build or runtime dependencies.

## Start the API

Start SQL Server/Redis and the API using the project’s normal startup procedure. For a local Release process on port 5000, from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5000'
dotnet run --project .\apps\api\src\NEXUS.Api\NEXUS.Api.csproj --configuration Release --no-launch-profile
```

For an already-seeded database, `ASPNETCORE_ENVIRONMENT=Production` can be used if its connection string and secrets are explicitly configured. Production mode skips the development seed path.

Confirm the API and database before starting a load run:

```powershell
Invoke-WebRequest http://localhost:5000/swagger/index.html -UseBasicParsing
```

## Run one independent baseline

Run each load level as a separate process. Two minutes is the default; use three minutes when the environment can support it.

```powershell
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$result = ".\tests\load\results\10vus-$stamp.json"
k6 run --vus 10 --duration 2m --summary-export $result .\tests\load\nexus-baseline.js
```

Repeat independently for 25, 50, and 100 VUs:

```powershell
k6 run --vus 25 --duration 2m --summary-export .\tests\load\results\25vus-$stamp.json .\tests\load\nexus-baseline.js
k6 run --vus 50 --duration 2m --summary-export .\tests\load\results\50vus-$stamp.json .\tests\load\nexus-baseline.js
k6 run --vus 100 --duration 2m --summary-export .\tests\load\results\100vus-$stamp.json .\tests\load\nexus-baseline.js
```

In a second PowerShell window, collect lightweight host observations during each 2-minute run. The collector records aggregate dotnet process CPU/memory, SQL Server process CPU/memory, and the available SQL Server user-connections counter; it does not change the application or database.

```powershell
powershell -NoProfile -File .\tests\load\capture-server-metrics.ps1 -DurationSeconds 150 -OutputPath .\tests\load\results\server-10vus-$stamp.csv
```

Start the collector immediately before k6 for each level and use a distinct output file per level.

Use `--duration 3m` for a longer stabilization window. Stop the sequence when the failure rate is above 1%, repeated 5xx/timeouts occur, p95 sharply degrades, or API/DB stability worsens.

Optional overrides:

```powershell
$env:BASE_URL = 'http://localhost:5000'
$env:REQUEST_TIMEOUT = '30s'
$env:THINK_MIN_SECONDS = '1'
$env:THINK_MAX_SECONDS = '3'
$env:K6_USER_EMAIL = 'admin@nexus.com'
$env:K6_PASSWORD = '<seeded development password>'
```

Do not set a shared token manually. `K6_USER_EMAIL` is only for choosing one valid identity; tokens are still generated independently per VU.

## Auth-only reproduction

The smallest controlled reproduction uses 10 VUs and one login attempt per VU. It does not call `me`, refresh, projects, risks, or any other endpoint:

```powershell
k6 run --vus 10 --duration 15s --summary-export .\tests\load\results\auth-shared-10vus.json .\tests\load\auth-only.js
k6 run --vus 10 --duration 15s --summary-export .\tests\load\results\auth-distinct-10vus.json -e AUTH_MODE=distinct .\tests\load\auth-only.js
```

`AUTH_MODE=shared` makes all VUs log in as `admin@nexus.com`. `AUTH_MODE=distinct` assigns the ten seeded identities in the script, one per VU. Both modes still create independent access/refresh token pairs inside each VU; the auth-only script does not refresh or share tokens. Keep the duration short because login creates a server-side session and updates last-login state.

## Metrics captured

The script records global k6 request metrics and separate metrics for Login/Auth, Current user, Projects list, Project Details, Planning Bootstrap, Risks list, Risk Details, and Risk Actions. For each endpoint it records request count, failures, and p50/p95/p99 duration in the k6 summary JSON. It also records overall failed request rate, normal API duration excluding login, HTTP 4xx, HTTP 5xx, timeouts, login failures, and dynamic-data selection failures.

Expected validation 4xx responses are not treated as successful traffic: every endpoint is successful only for a 2xx response, and 4xx/5xx/timeout samples increase failure metrics.

## Server-side observations

During every run record, at minimum, the timestamp, API environment/build/configuration, database and approximate row counts, and any available observations for API CPU/memory, SQL CPU, SQL connection/pool pressure, request queueing, query duration, exceptions, and timeouts. The load script does not install or redesign observability infrastructure.

## Current authentication diagnosis

- [Login SQL cancellation diagnosis (2026-11-03)](LOGIN-CANCELLATION-DIAGNOSIS-REPORT.md) records the request-abort propagation, SQL provider cancellation evidence, exception handling change, and the Development Admin verification blocker.
