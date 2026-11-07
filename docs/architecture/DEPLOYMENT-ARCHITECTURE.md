# Deployment Architecture

---

## Local Development Setup

Two commands to start development (after infrastructure is running):

```bash
# Start API (from apps/api directory)
dotnet run

# Start frontend (from apps/web-react directory)
npm run dev
```

Or from root via npm scripts:
```bash
npm run dev:api    # -> cd apps/api && dotnet run
npm run dev:web    # React/Vite development server on port 4100
```

**Evidence:** `README.md:54-57`, `package.json:7-8`

### Prerequisites
- Node.js 18+
- .NET 10 SDK
- SQL Server 2022 (local install, SQL Express, or Docker)
- Redis 7
- Docker & Docker Compose (optional)

---

## Docker Compose

5 services defined in `docker-compose.yml`:

```yaml
services:
  sqlserver:  # mcr.microsoft.com/mssql/server:2022-latest, port 1433
  redis:      # redis:7-alpine, port 6379
  api:        # ./apps/api/Dockerfile, port 5000
  web:        # ./apps/web-react/Dockerfile, host port 4000
  worker:     # ./apps/api/Dockerfile.worker, internal
volumes:
  sqlserver-data:
  redis-data:
networks:
  default: nexus-network
```

**Evidence:** `docker-compose.yml` (91 lines)

### Service Details

| Service | Image | Port(s) | Healthcheck | Depends On |
|---------|-------|---------|-------------|------------|
| sqlserver | `mcr.microsoft.com/mssql/server:2022-latest` | `127.0.0.1:1433:1433` | `/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"` | - |
| redis | `redis:7-alpine` | `127.0.0.1:6379:6379` | `redis-cli -a "$$REDIS_PASSWORD" ping` when `NEXUS_REDIS_PASSWORD` is set, otherwise `redis-cli ping` | - |
| api | `./apps/api/Dockerfile` | 5000:5000 | - | sqlserver (healthy), redis (healthy) |
| web | `./apps/web-react/Dockerfile` | 4000:80 | - | api |
| worker | `./apps/api/Dockerfile.worker` | - | - | sqlserver (healthy), redis (healthy) |

> SQL Server and Redis are published on the loopback interface only. They are not
> reachable from other hosts.

### Health Checks

| Service | Test | Interval | Timeout | Retries |
|---------|------|----------|---------|---------|
| sqlserver | `CMD-SHELL /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$$MSSQL_SA_PASSWORD" -C -Q "SELECT 1"` | 15s | 5s | 10 |
| redis | `CMD-SHELL` conditional `redis-cli` ping (uses AUTH when configured) | 10s | 5s | 5 |

### Volumes

| Volume | Mount | Purpose |
|--------|-------|---------|
| `sqlserver-data` | `/var/opt/mssql` | Persistent database storage |
| `redis-data` | `/data` | Persistent cache storage |

### Network

All services share `nexus-network` bridge network.

---

## Dockerfiles

### API (`apps/api/Dockerfile`)

| Stage | Base Image | Operations |
|-------|-----------|------------|
| build | `mcr.microsoft.com/dotnet/sdk:10.0` | Copy .csproj files, restore, publish Release to /app |
| runtime | `mcr.microsoft.com/dotnet/aspnet:10.0` | Copy published output, expose 5000, entrypoint `dotnet NEXUS.Api.dll` |

**Resolved (ADR-005):** .NET 10 images with `net10.0` TFM -- consistent.

### Worker (`apps/api/Dockerfile.worker`)

| Stage | Base Image | Operations |
|-------|-----------|------------|
| build | `mcr.microsoft.com/dotnet/sdk:10.0` | Same multi-stage build as API |
| runtime | `mcr.microsoft.com/dotnet/aspnet:10.0` | Entrypoint: `dotnet NEXUS.Api.dll --worker-mode`, port 5001 |

### Web (apps/web-react/Dockerfile)

| Stage | Base Image | Operations |
|-------|-----------|------------|
| deps | node:20-alpine | npm ci using the React app lockfile |
| builder | node:20-alpine | Vite production build |
| runner | nginx:1.27-alpine | Serves the static SPA and proxies API requests |

Docker Compose publishes the React/Vite SPA on host port 4000. Local Vite development uses port 4100.
## Environment Variables

### API (`docker-compose.yml` + `appsettings.json`)

| Variable | Value | Source |
|----------|-------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` by default; `Development` from `docker-compose.override.yml` | docker-compose |
| `ASPNETCORE_URLS` | `http://+:5000` | docker-compose + Dockerfile |
| `ConnectionStrings__DefaultConnection` | `Server=sqlserver;Database=NexusPmo;User Id=sa;Password=${NEXUS_DB_PASSWORD};Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true` | docker-compose (password from environment) |
| `Jwt__Secret` | `${NEXUS_JWT_SECRET}`; required outside Development (the host fails fast when empty) | environment |
| `Redis__ConnectionString` | `redis:6379` | docker-compose |

### Web (docker-compose.yml)

| Variable | Value | Source |
|----------|-------|--------|
| VITE_API_URL | Optional; defaults to http://localhost:5000 | Docker build environment |
| VITE_FIREBASE_* | Public browser configuration where enabled | Docker build environment |

The SPA is served by nginx on container port 80 and published on host port 4000.
Local Vite development uses port 4100 and can read VITE_API_URL from apps/web-react/.env.local.
### appsettings.json (fallback)

| Key | Value |
|-----|-------|
| `ConnectionStrings:DefaultConnection` | `Server=localhost;Database=NexusPmo;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true` |
| `Jwt:Secret` | **Not present in source.** Supply it through `Jwt__Secret` or a secret store; Development generates a process-local key when unset |
| `Jwt:Issuer` | `NEXUS` |
| `Jwt:Audience` | `NEXUS-API` |
| `Jwt:ExpiryHours` | `8` |
| `Redis:ConnectionString` | `localhost:6379` |
| `Redis:InstanceName` | `NEXUS` |

**Evidence:** `docker-compose.yml`, `appsettings.json`

---

## Port Mapping

| Port | Service | Protocol |
|------|---------|----------|
| 1433 | SQL Server | TCP |
| 6379 | Redis | TCP |
| 5000 | API | HTTP |
| 4000 | React/Vite web SPA | HTTP |

---

## Startup Order

1. SQL Server and Redis start in parallel (no dependencies)
2. API waits for both services to be healthy (depends_on with condition: service_healthy)
3. Worker waits for both databases to be healthy
4. Web starts after API (depends_on without health condition)

---

## Deployment Diagram

```mermaid
graph TB
    subgraph "Docker Host"
        subgraph "mcr.microsoft.com/mssql/server:2022-latest"
            PG[("SQL Server 2022<br/>Port 1433")]
            PGV[(Volume: sqlserver-data<br/>/var/opt/mssql)]
        end

        subgraph "redis:7-alpine"
            RD[("Redis 7<br/>Port 6379")]
            RDV[(Volume: redis-data<br/>/data)]
        end

        subgraph "API Server"
            API["NEXUS API<br/>ASP.NET Core 10<br/>Port 5000"]
            APIDL[Dockerfile<br/>Multi-stage build]
        end

        subgraph "Web Server"
            WEB["NEXUS Web<br/>React/Vite SPA<br/>Host port 4000"]
            WEBDL[React/Vite Dockerfile<br/>nginx static hosting]
        end

        subgraph "Background Worker"
            WRK["Worker<br/>--worker-mode<br/>Port 5001"]
            WRKDL[Dockerfile.worker]
        end

        API -->|"EF Core (SqlServer):1433"| PG
        API -->|"StackExchange.Redis:6379"| RD
        WEB -->|"HTTP/REST JSON"| API
        WRK -->|"EF Core:1433"| PG
        WRK -->|"StackExchange.Redis:6379"| RD
    end
```

---

## Production Security Concerns

| Issue | Details |
|-------|---------|
| Database credential | Resolved: `NEXUS_DB_PASSWORD` is required from the environment; no password is committed |
| JWT secret | Resolved: no fallback key in source; `NEXUS_JWT_SECRET` is mandatory outside Development and the host fails fast when it is missing |
| Loopback-only data services | Resolved: SQL Server and Redis publish on `127.0.0.1` |
| No HTTPS enforcement | CORS allows localhost origins only, no TLS termination configured |
| Auth rate limiting | Resolved: fixed-window limiter on `/api/auth/login` and `/api/auth/refresh` |
| No secrets management | No vault/secret store integration; the platform must inject environment secrets |
