# RBAC Build-Only Handover

**Date:** 2026-11-15  
**Repository:** `C:\Projects\pmo`  
**Phase result:** **IMPLEMENTATION COMPLETE — BUILD VERIFIED — TESTS DEFERRED**  
**Database result:** **DATABASE APPLICATION PENDING — HOST ACTION REQUIRED**  
**Runtime/API result:** **NOT VERIFIED**

## Executive status

The approved source implementation was compiled across the API, Persistence, API test project (compilation only), directly affected backend modules, and React/Vite frontend. Every listed build completed with exit code 0 and zero compilation errors. Existing source/build warnings are recorded below. The independent reviewer identified no unresolved critical source-level regression.

No unit/integration/UI tests, test discovery, authenticated API calls, runtime verification, or browser UAT were run in this phase, as directed. The API test project was built only to verify compilation. These build results do not prove behavior at runtime.

The EF migration was reviewed but not applied. The last host preflight is historical evidence from 2026-11-15, not a fresh target check for this execution. No database write, seed, migration, role reclassification, or role inventory query occurred. The Project Manager onboarding 403 and Organization Admin API behavior remain unverified.

## Implementation and architecture delivered

- Canonical role identity is separated from System classification. Source seed defines only Tenant Admin and Organization Admin as System identities; Project Manager and other operational roles remain Non-System. The prior seed source classified eight roles as System; seven ordinary roles (Project Manager, Portfolio Manager, Executive, PMO Admin, Finance Controller, Resource Manager, Team Member) are reclassified in source. The actual database has not been migrated or inventoried.
- Tenant Admin delegation uses explicit assignment/delegation authority and catalog, scope, dependency, restriction, reserved-key, self-escalation, Protected Core, Active Deny, and last-effective-admin safeguards. Operational possession is not required to delegate an eligible ordinary permission.
- Organization Admin checks require canonical identity, active same-Tenant membership, explicit Organization scope grant, and current effective Organization Configuration permissions. Tenant and Platform configuration and unrelated operational/financial permissions remain separate.
- Frontend permission references and Organization configuration/lifecycle controls are aligned to the approved backend scope and inherited Tenant settings remain read-only in the Organization Admin UI.
- Existing source test compatibility edits, including the `ManagementContext` fixture correction, are retained. Tests and assertions were not run or weakened in this phase.

## Build evidence

Builds used .NET SDK `11.0.100` from the system temporary directory because the repository `global.json` pins SDK `10.0.302`, whose prior project-reference resolver failed in this environment. Isolated direct project builds used `--no-restore`, one MSBuild node, `BuildProjectReferences=false`, and a unique temporary `OutDir` to avoid locked or shared repository outputs. The final integrated API test-project compilation enabled project references and used an isolated output directory. No repository SDK configuration was changed.

| Target | Command / method | Exit | Warnings | Errors |
|---|---|---:|---:|---:|
| React/Vite | `npm run build` from `apps/web-react` | 0 | Bundler warnings listed below | 0 |
| API | `dotnet build C:/Projects/pmo/apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false "-p:OutDir=<unique-temp>/"` | 0 | 352 | 0 |
| Persistence | `dotnet build C:/Projects/pmo/apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --no-restore --verbosity quiet -m:1 -p:BuildProjectReferences=false "-p:OutDir=<unique-temp>/" '-clp:ErrorsOnly;Summary'` | 0 | 0 | 0 |
| SharedKernel | `dotnet build C:/Projects/pmo/apps/api/src/NEXUS.SharedKernel/NEXUS.SharedKernel.csproj --no-restore --verbosity quiet -m:1 -p:BuildProjectReferences=false "-p:OutDir=<unique-temp>/" '-clp:ErrorsOnly;Summary'` | 0 | 0 | 0 |
| API test project, compile only | `dotnet build C:/Projects/pmo/apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity quiet -m:1 -p:BuildProjectReferences=false "-p:OutDir=<unique-temp>/" '-clp:ErrorsOnly;Summary'` | 0 | 383 | 0 |
| API test project, integrated compile only | `dotnet build C:/Projects/pmo/apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal -m:1 -p:OutDir=<unique-temp>/` (project references enabled) | 0 | 389 | 0 |
| Identity, Organization, ProjectManagement, CostManagement, ResourceManagement, ProjectPlanning, Portfolio, WorkflowEngine, AIPlanning, Approvals, Audit, Documents, Integrations, IssueManagement, Meetings, Notifications, PluginManagement, Reporting, RiskManagement, WorkItems, ProjectPlanning.MppAdapter | Individual isolated builds using the same no-restore, one-node, no-project-reference, unique-temp-output method | 0 each | 0 except ProjectManagement 4 and CostManagement 2 | 0 each |

The API command used a unique temporary output directory through `-p:OutDir`; the API test compilation used the same isolation. The integrated test-project build compiled SharedKernel, all referenced modules including MppAdapter, Persistence, API, and API.Tests together. Its elapsed time was 5:27.78. The exact invocation shape was:

```powershell
Set-Location $env:TEMP
$out = Join-Path $env:TEMP ('pmo-rbac-integrated-tests-compile-' + [guid]::NewGuid().ToString('N'))
dotnet build 'C:/Projects/pmo/apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj' --no-restore --verbosity minimal -m:1 "-p:OutDir=$out/"
```

The earlier API.Tests isolated compile-only build was also successful (383 warnings, 0 errors); the integrated result is the stronger cross-project confirmation. Neither build started test discovery or execution. The common command template for module builds was:

```powershell
dotnet build C:/Projects/pmo/apps/api/src/<project>/<project>.csproj --no-restore --verbosity quiet -m:1 -p:BuildProjectReferences=false "-p:OutDir=<unique-temp>/" '-clp:ErrorsOnly;Summary'
```

### Representative changed source files

Representative implementation files include `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs`, `apps/api/src/NEXUS.Modules.Identity/Application/RoleAssignmentAuthorizationPolicy.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserAdministrationService.cs`, `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserEffectivePermissionService.cs`, `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs`, `apps/api/src/NEXUS.Persistence/Configurations/RoleConfiguration.cs`, `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs`, the `20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification` migration/designer and model snapshot, Organization configuration access and lifecycle services/controllers, and React permission, user/role administration, Organization settings/lifecycle and EN/AR translation files. The worktree also contains concurrent changes; this list identifies the RBAC implementation surface and is not an attribution of every dirty file. The detailed architecture inventory remains in `RBAC-IMPLEMENTATION-FINAL-REPORT.md`.

### Warning classification

- API: 352 warnings, predominantly IKVM reference/class-format and missing referenced Java-class diagnostics from the MPP dependency path; 0 errors.
- API test-project isolated compilation: 383 warnings; 0 errors. Integrated project-reference build: 389 warnings; 0 errors. Warnings include inherited IKVM diagnostics, nullable analysis, and existing xUnit analyzer advice; no test discovery or execution was started.
- ProjectManagement: 4 nullable warnings (`CS8629`/`CS8602`) in `ProjectLifecycleCatalogService.cs`; 0 errors.
- CostManagement: 2 nullable warnings (`CS8629`) in `BudgetService.cs` and `BudgetRevisionService.cs`; 0 errors.
- Other listed .NET modules, Persistence, and SharedKernel: 0 warnings, 0 errors.
- React/Vite: build succeeded. Non-blocking warnings included npm's unrecognized `http-proxy` config, dropped Rollup PURE annotations, mixed static/dynamic import for `PlannedPaymentsPage.tsx`, and large generated JS/font chunks.

## Database and seed status

Historical host preflight artifact: `artifacts/ef-review/dev-db-preflight-inspect-20261115T094923Z-77e35528.json`. It reported Development SQL Server `Shady`, database `NexusPmo`, DB_ID `5`, Windows Authentication, model synchronized, 27 of 28 migrations applied, and pending migration `20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification`. Its reviewed SQL artifact hash was `29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B`.

The migration source and generated SQL were statically reviewed as transactional/fail-closed and limited to canonical role identity/classification plus EF migration history. The source does not delete/recreate role rows or update memberships, role grants, direct overrides, audit rows, or unrelated business records. This is not proof of live data preservation. The migration has not been applied, seed has not run, and live System Role inventory, membership/grant counts, schema status, and seed idempotency are not verified.

On the authorized Windows Development host, reconfirm the target and pending artifact before any write:

```powershell
Set-Location 'C:\Projects\pmo'
./scripts/dev/db-preflight.ps1
Get-FileHash -Algorithm SHA256 'artifacts/ef-review/dev-pending-20261115T094740Z-eacb7b37.sql'
```

Stop if the environment/identity, migration list, generated SQL, schema checks, or hash differ from the reviewed values. Capture the documented read-only role/membership/grant/override/audit baseline before applying. If all gates still match, use only the repository wrapper:

```powershell
./scripts/dev/db-apply-migrations.ps1 -ReviewedSqlPath 'artifacts/ef-review/dev-pending-20261115T094740Z-eacb7b37.sql' -ApprovedSha256 '29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B' -AuthorizationReference '2026-11-15 FOPX PM P0 RBAC Build-Only Handover' -ConfirmDevelopmentApply
./scripts/dev/db-migrations-status.ps1
```

Then run only the approved idempotent seed/reconciliation workflow and verify the two System identities, ordinary role classifications, stable role IDs, unchanged memberships/grants/overrides/audit counts, Protected Core, and absence of duplicates. Do not execute the SQL artifact directly or patch data manually.

## Deferred verification and unresolved decisions

- Tests: explicitly deferred. No `.NET test`, React test, test discovery, or testhost was invoked; test counts are **not applicable for this phase**, not zero-pass evidence.
- Runtime: no serving API process/assembly or Development connection was verified. No authenticated `POST /api/users`, trace, membership readback, or effective-permission comparison was performed. The historical 403 is not declared resolved.
- Organization Admin runtime: no API allow/deny scenarios were run. Organization Admin appointment/removal/recovery/protection/cardinality/last-admin policy remains a Product Owner decision.
- Organization scope: Organizational Unit/type/membership mutations remain Tenant `Settings.ManageTenantConfiguration` protected; this handover does not claim them as Organization Admin authority. BusinessUnit boundaries and `Users.ManageAccounts` versus `Users.View` compatibility remain explicit policy decisions.
- Browser UAT: reserved for the Product Owner and not performed.

## Independent review

The Independent Critical Reviewer reconciled the source safeguards and the supplied build results and found no unresolved critical source regression. The reviewer confirmed the security boundaries for Tenant Admin delegation, protected Core, Active Deny, anti-escalation, last-admin protection, Organization scope, and fail-closed seed behavior. The reviewer did not run builds, tests, API, browser, or database actions; the assessment is not runtime or data-integrity verification.

## Readiness

**IMPLEMENTATION COMPLETE — BUILD VERIFIED — TESTS DEFERRED.**  
**DATABASE APPLICATION PENDING — HOST ACTION REQUIRED.**  
**RUNTIME/API BEHAVIOR NOT VERIFIED.**

