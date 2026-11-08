# Local Development Setup

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| Node.js | 18+ (LTS recommended) | Frontend development |
| npm | 10+ | Package management |
| .NET SDK | 10.0+ | Backend development |
| SQL Server | 2022+ | Primary database |
| Redis | 7+ | Caching and pub/sub |
| Docker | Latest | Containerized infrastructure |
| Git | Latest | Version control |
| IDE | VS Code / Rider / Visual Studio | Development |

---

## Step 1: Clone Repository

```bash
git clone <repository-url>
cd pmo
```

---

## Step 2: Install Frontend Dependencies

Install root scripts and the supported React/Vite app dependencies:

npm install
npm --prefix apps/web-react ci

## Step 3: Restore Backend Packages

```bash
cd apps/api
dotnet restore
cd ../..
```

---

## Step 4: Database Setup

### Local SQL Server

The API uses SQL Server. A local SQL Server Developer or Express instance is suitable.
The Development connection string can be supplied through user secrets or the
`ConnectionStrings__DefaultConnection` environment variable; do not commit credentials.
For a local Windows-authenticated instance, the checked-in Development configuration
uses `Server=tcp:localhost,1433;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true`.

In Development, API startup skips database migrations and seeding by default. Apply schema changes through the [EF Core database workflow](ef-core-database-workflow.md) after verifying the target and reviewing the generated SQL. `--seed-only` runs the complete deterministic Development seed and exits before opening the HTTP listener; `--seed-identity` runs only identity seeding. Neither option applies migrations.

---

## Step 5: Environment Configuration

### React/Vite Frontend Environment

The local Vite setup uses a same-origin `/api` proxy over HTTPS. Leave `VITE_API_URL`
unset in `.env.local`; add it only when explicitly configuring another HTTPS API.

Before starting either app, trust the local .NET HTTPS certificate:

```powershell
dotnet dev-certs https --trust
```

The standard `npm run dev:api` and `npm run dev:web` commands also prepare/trust the
certificate. Vite exports it to a temporary password-protected PFX for its HTTPS listener
and removes that temporary file when Vite exits. The password is generated per run and is
not written to the repository.

### API (`apps/api/src/NEXUS.Api/appsettings.Development.json`)

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:4000",
      "http://127.0.0.1:4000",
      "https://localhost:4100"
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:localhost,1433;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Issuer": "NEXUS",
    "Audience": "NEXUS-API",
    "ExpiryHours": 8,
    "RefreshTokenLifetimeDays": 7
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

> The API reads CORS allowed origins from the `Cors:AllowedOrigins` configuration array
> (named policy `FrontendCors` in `Program.cs`). If no origins are configured in
> Development, the API fails at startup with a clear error. Do not add origins for
> production here - configure them via environment/config for production.

### JWT signing secret

No signing key is stored in source control. The API resolves `Jwt:Secret` from
configuration and validates that it is at least 32 bytes of UTF-8 data:

| Environment | Behaviour when `Jwt:Secret` is missing |
|-------------|----------------------------------------|
| Development | A random, process-local key is generated. Tokens are invalid after a restart, which only affects local sessions. |
| Any other   | Startup fails with a clear error. |

Provide the value through a secret store or the `Jwt__Secret` environment variable,
never through a committed `appsettings*.json`. For example, as a .NET user secret:

```bash
cd apps/api/src/NEXUS.Api
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"
```

Use a distinct value per environment, and rotate it (invalidating all issued
access tokens) if it has ever been committed or shared.

### Docker Compose environment

Copy `.env.example` to `.env` before starting the local stack:

```env
NEXUS_DB_PASSWORD=<local SQL Server SA password>
NEXUS_JWT_SECRET=<32+ byte random value; required outside Development>
NEXUS_REDIS_PASSWORD=<optional; enables Redis AUTH>
```

`docker-compose.yml` publishes SQL Server (1433) and Redis (6379) on
`127.0.0.1` only, so they are not reachable from other hosts. The base file
defaults `ASPNETCORE_ENVIRONMENT` to `Production`; `docker-compose.override.yml`
switches the API and worker to `Development` for local work. Never expose a
Development API outside a trusted machine: Swagger is enabled and explicit seed
options can create demo identities. Database migrations run through the delivery
gate, not API startup.


---

## Step 6: Run the Application

### Start API (Terminal 1)

```bash
npm run dev:api
# or directly:
cd apps/api && dotnet run
```

The API starts on https://localhost:5001 with Swagger at
https://localhost:5001/swagger. HTTP requests on port 5000 are redirected to HTTPS.
Development startup skips migrations, seeding,
and workflow provisioning unless an explicit seed option is supplied.

After applying schema through the documented EF Core workflow, seed the identity fixture without
opening an API listener:

```bash
dotnet run --project apps/api/src/NEXUS.Api/NEXUS.Api.csproj -- --seed-identity
```

For the full Development demo dataset instead, use `--seed-only`. Neither option
applies migrations.

### Start Frontend (Terminal 2)

Run `npm run dev:web`, or run `npm run dev` from `apps/web-react`. Vite is available at
https://localhost:4100 and proxies `/api` to `https://localhost:5001`. Use `localhost`
(not `127.0.0.1`) so the trusted development certificate matches the host. The
Docker-hosted static SPA is a separate setup published on host port 4000.

## Step 7: Bootstrap the Development Administrator

Development identity data is seeded only by an explicit seed option and never in
Production. Set `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` through a secure Development secret
source before the first seed. It must contain at least 12 characters. The seeded
`admin@nexus.com` account is marked to change its password at first login; no
administrator password is embedded in the seed source. Re-seeding preserves an
existing ASP.NET Core password hash and its password-change/expiry state, including
missing or elapsed temporary-password expiry.
The temporary password expires after `Security:LocalPasswords:TemporaryPasswordExpiryHours`
(24 hours by default).

For a temporary Development UAT window, set
`Security:LocalPasswords:DevelopmentAdminTemporaryPasswordBypassEnabled` to `true`
in the running API configuration. For a shell-launched API, set
`$env:Security__LocalPasswords__DevelopmentAdminTemporaryPasswordBypassEnabled = 'true'`
before starting it. The setting is `false` by default and only applies to the seeded
Development Admin. The password hash must still verify; the bypass does not rewrite
password state, roles, or grants. Normal login still creates a session and updates
last-login metadata. Development configuration file changes reload for subsequent
authentication/API requests. Set it back to `false` after UAT; if an
environment variable overrides the file setting, remove the override and restart the
API. The application ignores this option outside Development.

### Recover and verify the Development Admin password

Use this only for `Development`. First run the read-only EF gate against the explicit
target and confirm `Shady` / `NexusPmo`. Set
`NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` from the same secure source as the active API
Development connection and set `ASPNETCORE_ENVIRONMENT=Development`; do not print the
connection string. The recovery command rechecks the target and pending migrations.

The backup directory must already exist, be writable by the SQL Server service, and
use a volume or network share independent from every database data/log volume. The
command stops before changing data if the target or backup check fails. Build into a
separate temporary output directory so a currently running API does not need to be
stopped or overwritten:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$recoveryBuild = Join-Path $env:TEMP 'NexusAdminRecoveryBuild\'
dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1 -p:OutDir=$recoveryBuild
Push-Location apps/api/src/NEXUS.Api
dotnet (Join-Path $recoveryBuild 'NEXUS.Api.dll') `
  --recover-development-admin-password `
  --admin-recovery-expected-sql-server Shady `
  --admin-recovery-expected-sql-database NexusPmo `
  --admin-recovery-backup-directory '<absolute SQL Server-writable path on independent storage>'
Pop-Location
```

The interactive console asks twice for a temporary password and twice for the
permanent password to use after the forced change. Characters are not echoed. The
command hashes both passwords through the registered `IPasswordHasher<User>`, backs
up and verifies the Development database before recovery, then verifies temporary
login, the required first-password change, and login with the permanent password.
After it completes, the Admin is using that permanent password and no longer has a
pending first-login change. Do not pass either password in command arguments or
environment variables.

Passwords are hashed with ASP.NET Core `PasswordHasher<User>`. Login returns a JWT access token
(8h) and a refresh token (7d); the frontend refreshes automatically and redirects to
`/{locale}/login` when the session expires.

---

## Temporary tunnels for the React app and API

The normal React/Vite and API development ports are 4100 and 5000. On Windows,
`scripts/start-dev-tunnels.ps1` starts separate instances on ports 4110 and
5100 by default, plus two
Cloudflare Quick Tunnels, prints their HTTPS URLs, and stops all four processes
when interrupted. Install `cloudflared` on PATH or place the official
`cloudflared.exe` in `tmp/tools`. The API instance uses the existing compiled
build (`dotnet run --no-build`); build it beforehand while no other process
holds its output files if you need to pick up source changes. The script sets `VITE_API_URL` to the API
tunnel URL, adds the exact frontend tunnel origin to the API CORS list, and
allows the exact Vite tunnel hostname. No backend secrets belong in `VITE_`
variables.

These are anonymous public URLs. Anyone with either URL can reach the
development service, and this repository's Development mode seeds demo users
with documented credentials. Use this only for a controlled temporary test
with data you are willing to expose. The script requires an explicit
`-AllowPublicAccess` switch. For Google AI Studio browser preview, also pass
the preview page's exact HTTPS origin if it calls the API directly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/start-dev-tunnels.ps1 -AllowPublicAccess -AiStudioOrigin 'https://your-preview-origin.example'
```

Use the printed API URL as the API base URL in the AI Studio app. The origin
must be the browser page's origin (scheme and hostname, without a path), not
the API URL. A new Quick Tunnel run produces new URLs, so update the AI Studio
app after each restart. The API's `/health` endpoint and the React root page
must become healthy before the script prints the URLs. Logs are in
`tmp/dev-tunnels`.
Use `-WebPort` and `-ApiPort` if these ports are occupied; the script refuses
to attach a tunnel to an existing listener.

Cloudflare Quick Tunnels are for development and provide no stable hostname.
For a persistent or access-controlled integration, use a named tunnel and
appropriate access policy instead.

---

## Running with Docker (Full Stack)

```bash
# Start all services
npm run docker:up

# This runs:
# - SQL Server 2022 (port 1433)
# - Redis 7 (port 6379)
# - NEXUS API (.NET 10, port 5000)
# - NEXUS React/Vite SPA (Docker host port 4000)
# - Background Worker (internal)

# View logs
docker compose logs -f

# Stop all services
npm run docker:down
```

---

## Common Development Tasks

### Reset the disposable Development/UAT database

The only no-backup reset exception is the exact Development target SQL Server
`Shady` / database `NexusPmo`. It requires explicit Product Owner authorization
recorded in the active task/conversation, an authorization reference in the gate
command, exact target/environment verification, all EF migrations, and the Minimal
Seed (`--seed-minimal-only`) before completion. The unchanged Demo Seed (`--seed-only`)
is separate and is not used for a clean Admin-bootstrap reset. Follow the [documented EF Core database workflow](ef-core-database-workflow.md).
This exception does not apply to standalone seed, permission reconciliation, Admin
password recovery, ordinary migration apply, or any other database write; those
operations retain their verified-backup requirements.

### Apply Migrations

Use the repository gate to inspect the explicit Development target, generate and
review its SQL, verify a SQL Server-accessible backup, and apply only the approved
hash. See the [EF Core database workflow](ef-core-database-workflow.md). Do not
use `dotnet ef database update` as a substitute for that gate.

### Seed Database

For a standalone seed or reseed, verify the exact Development target and complete
the ordinary verified-backup gate before writing. A seed run required as the
immediate continuation of an explicitly Product Owner-authorized reset of
`Shady/NexusPmo` is covered by that reset's no-backup exception; see the reset
procedure above.

```bash
dotnet run --project apps/api/src/NEXUS.Api/NEXUS.Api.csproj -- --seed-identity
# Minimal system baseline, with no demo/business records:
dotnet run --project apps/api/src/NEXUS.Api/NEXUS.Api.csproj -- --seed-minimal-only
# The unchanged full Development demo dataset:
dotnet run --project apps/api/src/NEXUS.Api/NEXUS.Api.csproj -- --seed-only
```

`--seed-minimal-only` creates only the Tenant, structural Organization, code-defined
settings defaults, canonical permissions and system roles, protected Tenant Admin,
and approved reference catalogs. It refuses to run over existing users or business
records. It does not create projects, portfolios, programs, teams, financial records,
handovers, demo users, vendors, resources, work items, risks, issues, meetings, or
documents. The `--seed-only` Demo Seed remains a separate, unchanged operation and
must not be used for a clean Admin-bootstrap reset. The no-backup reset exception
remains unavailable for execution until the protected EF delivery-gate skill is
updated through the authorized policy-maintenance process.

The focused Minimal Seed regression tests use the EF InMemory provider and do not
connect to SQL Server:

```powershell
dotnet test apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore -p:FocusedMinimalSeed=true --filter "FullyQualifiedName~SeedMinimalAsync"
```

Ordinary identity reseeding leaves existing role grants and direct user Allow/Deny
overrides unchanged. New seeded `admin` roles receive the explicit tenant-scoped
Administration snapshot documented in the [final permission catalog](../security/PERMISSIONS-FINAL-CATALOG.md).
To add missing approved Administration grants to an existing deterministic seeded
Admin role, first verify the exact Development database target and an independent,
verified backup as described in the [EF Core database workflow](ef-core-database-workflow.md),
then run the identity seed with `ASPNETCORE_ENVIRONMENT=Development` and
`NEXUS_RECONCILE_SEEDED_ADMIN_ROLE_PERMISSIONS=true`. This opt-in is add-only,
audited, and proceeds only when the seeded role has exactly its active seeded Admin
user as its sole member. It does not change overrides; direct Deny still wins.

### Run Tests

```bash
# All tests
npm run test

# Frontend tests only
npm run test:web

# Backend tests only
npm run test:api
```

### Lint and Type Check

```bash
# Lint frontend
npm run lint:web

# TypeScript type checking
npm run typecheck
```

### Build for Production

```bash
# Frontend
npm run build:web

# Backend
npm run build:api
```

---

## Troubleshooting

### .NET SDK and API build

For SDK selection, missing workload resolver files, Windows installer repair, or API build failures, follow the [.NET SDK and API Build Troubleshooting guide](dotnet-sdk-troubleshooting.md). Do not copy resolver binaries from another SDK or change the repository's pinned SDK to work around an incomplete installation.

### Database Connection Issues

```bash
# Verify PostgreSQL is running
docker compose ps postgres

# Check connection
psql -h localhost -U nexus -d nexus_pmo
```

### Redis Connection Issues

```bash
# Verify Redis is running
docker compose ps redis

# Ping Redis
docker compose exec redis redis-cli ping
# Should return: PONG
```

### API Won't Start

- Verify PostgreSQL and Redis are running
- Check connection strings in `appsettings.Development.json`
- Ensure PostgreSQL user/password are correct
- Check port 5000 is not in use

### Frontend Can't Connect to API

- Verify the API is running on `https://localhost:5001` and Vite on `https://localhost:4100`.
- Run `dotnet dev-certs https --trust` if the browser or Vite reports a certificate error.
- Leave `VITE_API_URL` unset for the local Vite proxy. A stale `http://localhost:5000`
  override bypasses the proxy and is blocked by browser mixed-content protections.
- For a direct cross-origin API call, ensure the HTTPS frontend origin is in
  `Cors:AllowedOrigins` and the request includes credentials.
- Verify the API startup did not fail with the "CORS is not configured" error
- Test a preflight for the local Vite origin:

```powershell
curl.exe -i -X OPTIONS "https://localhost:5001/api/resources?pageSize=50" `
  -H "Origin: https://localhost:4100" `
  -H "Access-Control-Request-Method: GET" `
  -H "Access-Control-Request-Headers: authorization,content-type"
```

The response must include `Access-Control-Allow-Origin: https://localhost:4100`.

### Port Conflicts

Default ports:
- API: 5001 (HTTPS), 5000 (HTTP redirect)
- Frontend: 4100 (HTTPS Vite development) or 4000 (Docker SPA)
- SQL Server: 1433
- Redis: 6379

Override with environment variables:

```bash
# Change API ports only when updating the HTTPS redirect and Vite proxy target too.
ASPNETCORE_URLS="https://localhost:5001;http://localhost:5000" dotnet run

# Change PostgreSQL port in docker-compose.override.yml
```

### Clean Build and Restart Services

```bash
# Stop Docker services while preserving their volumes
docker compose down

# Clean build artifacts
npm run clean

# Restart services and the API
npm run docker:up
npm run dev:api  # Starts the API; it does not reset, migrate, or seed the Development database.
```

Use the guarded repository EF tooling and documented EF Core workflow for an explicitly authorized database reset, migration, or seed operation.
