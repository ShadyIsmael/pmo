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

```bash
# Root level (workspace scripts)
npm install

# Web application
cd apps/web
npm install
cd ../..
```

---

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
uses `Server=localhost;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true`.

The API applies EF Core migrations and seeds demo data when run in Development.

---

## Step 5: Environment Configuration

### Frontend (`apps/web/.env.local`)

Copy `apps/web/.env.example` to `apps/web/.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
```

### API (`apps/api/src/NEXUS.Api/appsettings.Development.json`)

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:4000",
      "http://127.0.0.1:4000"
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Secret": "NEXUS-Demo-Secret-Key-Minimum-16-Chars!",
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

---

## Step 6: Run the Application

### Start API (Terminal 1)

```bash
npm run dev:api
# or directly:
cd apps/api && dotnet run
```

The API will:
- Auto-create database schema (in Development mode)
- Seed demo data (users, projects, resources, etc.)
- Start on http://localhost:5000
- Swagger UI at http://localhost:5000/swagger

To apply migrations and seed without opening an API listener, run from the repository root:

```bash
dotnet run --project apps/api/src/NEXUS.Api/NEXUS.Api.csproj -- --seed-only
```

### Start Frontend (Terminal 2)

```bash
npm run dev:web
# or directly:
cd apps/web && npm run dev
```

Frontend starts on http://localhost:4000

> The frontend calls the API from `http://localhost:4000`. The API's CORS policy
> (`FrontendCors`) must include that origin - it is configured in
> `appsettings.Development.json` under `Cors:AllowedOrigins`.

---

## Step 7: Login with Demo Accounts

Seed data is created automatically in Development mode (skipped in Production). All accounts use password `Demo123!`:

| Role | Email | Password |
|------|-------|----------|
| System Administrator | admin@nexus.com | Demo123! |
| Executive | executive@nexus.com | Demo123! |
| Portfolio Manager | portfolio.manager@nexus.com | Demo123! |
| Project Manager | project.manager@nexus.com | Demo123! |
| Team Member | team.member@nexus.com | Demo123! |
| PMO Administrator | pmo.admin@nexus.com | Demo123! |
| Finance Controller | finance.controller@nexus.com | Demo123! |
| Resource Manager | resource.manager@nexus.com | Demo123! |

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
# - NEXUS Web (Next.js, port 4000)
# - Background Worker (internal)

# View logs
docker compose logs -f

# Stop all services
npm run docker:down
```

---

## Common Development Tasks

### Run Migrations

Migrations are auto-applied in Development mode. To manually manage:

```bash
cd apps/api/src/NEXUS.Persistence
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Seed Database

```bash
npm run seed
# or:
cd apps/api && dotnet run -- seed
```

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

- Verify API is running on port 5000
- Check `NEXT_PUBLIC_API_URL` in `.env.local` (must be `http://localhost:5000`)
- Check CORS configuration in `appsettings.Development.json` under `Cors:AllowedOrigins`
  - Must include `http://localhost:4000` and/or `http://127.0.0.1:4000`
- Verify the API startup did not fail with the "CORS is not configured" error
- Test the preflight manually:

```powershell
curl.exe -i -X OPTIONS "http://localhost:5000/api/resources?pageSize=50" `
  -H "Origin: http://localhost:4000" `
  -H "Access-Control-Request-Method: GET" `
  -H "Access-Control-Request-Headers: authorization,content-type"
```

The response must include `Access-Control-Allow-Origin: http://localhost:4000`.

### Port Conflicts

Default ports:
- API: 5000
- Frontend: 4000
- SQL Server: 1433
- Redis: 6379

Override with environment variables:

```bash
# Change API port
ASPNETCORE_URLS=http://localhost:5001 dotnet run

# Change PostgreSQL port in docker-compose.override.yml
```

### Clear and Reset

```bash
# Stop Docker services and delete volumes
docker compose down -v

# Clean build artifacts
npm run clean

# Fresh start
npm run docker:up
npm run dev:api  # Recreates database and seeds data
```
