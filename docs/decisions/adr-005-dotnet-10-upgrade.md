# ADR-005: Upgrade to .NET 10 LTS

## Status
Accepted

## Date
2026-07-31

## Context
The NEXUS PMO backend targets .NET 8 with all 22 `.csproj` files using `net8.0`. Dockerfiles already reference `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` images, creating a mismatch between the target framework and container runtime.

.NET 10 LTS provides long-term support, performance improvements, and updated tooling.

## Decision
Upgrade all .NET projects to `net10.0` with SDK `10.0.302`.

### Changes
- **Target Framework:** All 22 projects: `net8.0` → `net10.0`
- **global.json:** Created with `"version": "10.0.302"` and `"rollForward": "latestFeature"`
- **EF Core:** 8.0.0 → 10.0.10 (all packages)
- **Npgsql EF Core:** 8.0.0 → 10.0.10
- **ASP.NET Core JwtBearer:** 8.0.0 → 10.0.10
- **Serilog.AspNetCore:** 8.0.0 → 9.0.0
- **Swashbuckle:** 6.5.0 → 7.0.0
- **FluentValidation:** 11.8.0 → 11.11.0
- **MediatR:** 12.2.0 (unchanged, compatible)
- **BCrypt.Net-Next:** 4.0.3 (unchanged, compatible)
- **Microsoft.AspNetCore.Mvc.Core 2.2.5:** Removed from all module projects; replaced by `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- **Microsoft.AspNetCore.Authentication.JwtBearer:** Removed from Identity module (covered by FrameworkReference)
- **Docker:** No changes needed (already using .NET 10 images)

### Breaking Changes Fixed
1. **`Microsoft.AspNetCore.Mvc.Core` 2.2.5** - This .NET Core 2.x package is incompatible with .NET 10. Replaced with `FrameworkReference` to `Microsoft.AspNetCore.App` in all 18 module projects.
2. **`Authentication.JwtBearer` in Identity module** - Removed explicit NuGet reference; types are available via `Microsoft.AspNetCore.App` framework reference.
3. **Swashbuckle 6.x → 7.x** - Swashbuckle 7 targets .NET 10 with updated OpenAPI support.
4. **EF Core 8 → 10** - Updated entity configurations to use `UseCollation` and `UseIdentityColumn` API changes where applicable. All entity type configurations forward-compatible.
5. **Serilog.AspNetCore 8 → 9** - Updated to support .NET 10 runtime.

### Risks
- **Swashbuckle 7** introduces new OpenAPI internals. If incompatibilities arise, fall back to `Microsoft.AspNetCore.OpenApi` built-in support.
- **EF Core 10** may have model building changes. Existing entity configurations should remain compatible since they use standard Fluent API.
- **No test projects exist** so test-related breaking changes are not applicable.

## Consequences
- All .NET projects now target a single consistent `net10.0` TFM
- Docker builds match the target framework
- Access to .NET 10 LTS features, performance improvements, and 5-year support
- All NuGet packages updated to versions compatible with .NET 10
