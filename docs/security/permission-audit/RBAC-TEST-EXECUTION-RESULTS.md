# RBAC Test Execution Results

**Date:** 2026-11-15  
**Scope:** RBAC-focused source/tests and requested build gates. No browser UAT and no Development database/runtime/API test was run.

## Executed commands

| Command | Exit | Result |
|---|---:|---|
| `npm test -- src/test/organization-configuration-access.test.tsx src/test/project-lifecycle-catalog-org-admin.test.tsx` | 0 | **2 files passed; 12 tests passed.** Covers scoped Organization Admin route/navigation, server-authorized organization choices, view/manage controls, and unknown project counts rendered as N/A. The first attempt exposed a test-only route alias and missing `ConfirmProvider` mock export; both test harness issues were fixed without weakening assertions, then this command passed. |
| `npm test -- src/test/project-lifecycle-catalog-org-admin.test.tsx` | 0 | **1 file passed; 6 tests passed** after adding row-level scope protection. Covers no mutation controls for inherited Tenant rows, editable controls for the selected Organization row, and preserved Tenant permission controls. |
| `npm run build` in `apps/web-react` | Earlier exit 1; latest exit 0 | The initial TypeScript failure at `ProjectFinanceTab.tsx:95` was corrected to the canonical `Permissions.AssignmentRecovery.Manage` key (`Governance.AssignmentRecovery.Manage`). Latest `tsc` + Vite production build passed in 32.09 seconds. Existing Rollup annotation, mixed-import, and large-chunk warnings remain. |
| `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --verbosity minimal -m:1` | 1 | The full build reached output-copy stage but could not overwrite locked `apps/api/src/NEXUS.Api/bin/Debug/net10.0/*.dll` files used by another process. MSB3027/MSB3021 after retry; 26 errors are file-copy lock failures, not C# diagnostics. The process was not stopped. |
| `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:OutDir=<isolated-temp-output>` | 0 | **Build succeeded, 0 errors, 352 IKVM warnings.** This compiled the API project to an isolated temporary output and did not overwrite the locked runtime output. Project references were not rebuilt in this invocation; affected module/persistence projects had already compiled during the preceding serial dependency build before its output-copy failure. |
| `dotnet build apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:OutDir=<isolated-temp-output>` | 0 | **Build succeeded, 0 warnings, 0 errors, 39.74 seconds.** This ran after the fail-closed Organization Admin seed guard was added and compiled updated Persistence source to a unique temporary output; it did not rebuild project references or touch the Development database. |

## Attempts without a result

- `npm test -- src/test/access-security-pages.test.tsx` produced no result output after 60 seconds; the command was interrupted and exited 1. No test totals were emitted. A broader React selection also produced no completion output and was interrupted; do not count either as pass or fail.
- An initial combined React run failed because the new test harness used an unconfigured `@routes` alias and its `ConfirmProvider` mock omitted the provider export. The harness was corrected and the two Organization Admin test files above then passed.
- A focused .NET/API test attempt using the default output path exited 1 without test discovery or totals. The isolated attempt initially exposed a test-only compilation mismatch: `Integration/EmailNotificationIntegrationTests.cs(505,65)`, `CS0535`, where `TestOrganizationAccess` lacked `IEmailNotificationOrganizationScopeAccess.HasScopedOrganizationAdminAuthorityAsync(Guid, bool, CancellationToken)`. The fixture was corrected with a forwarding implementation that preserves its existing `HasAccessAsync` behavior and assertions.
- After that fixture correction, the isolated OutDir retry was stopped at the 60-second bound during IKVM reference generation. It emitted repeated `IKVM0100`, `IKVM0101`, and `IKVM0109` warnings but no C# diagnostics, test discovery, totals, or test case execution. Its shell exit code was 1 due to the interruption. This is **not** a test pass or test failure; the selected API tests, including the `ProtectedTenantAdminIdentityTests` SQLite null CHECK case, remain unexecuted.
- `SeedDataProviderTests` in `NEXUS.Modules.ResourceManagement.Tests` was attempted in its separate focused test project. Compilation was repaired and a bounded isolated build later passed, but VSTest could not complete testhost connection; no test-case totals or pass/fail results were produced.

## Not run / unavailable

- Development database preflight stopped before live target identity/migration history because the execution context received `TCP Provider: Access is denied`.
- No `dotnet ef` target-specific SQL was generated/reviewed, and no migration/seed/persistence test was applied to Development.
- No authenticated `POST /api/users`, Organization configuration endpoint request, trace ID, or persisted membership/grant assertion was run.
- No API server was stopped or restarted. The normal build reported locked DLLs in the API output directory, but executor process/listener inspection found no visible API process or listener on ports 5002/5000/4100/5001; CIM process inspection was denied. The actual serving assembly remains unknown.
- No browser UAT was performed; Product Owner owns that activity.

## Initial focused .NET/API command

This initial bounded command used an isolated output directory and a one-node build. `$testOut` was a unique path under the executor's temporary directory.

```powershell
$testOut = Join-Path $env:TEMP ("pmo-rbac-tests-" + [guid]::NewGuid().ToString("N"))
dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false "-p:OutDir=$testOut\" --filter "FullyQualifiedName~AdminDelegationAuthorityRecoveryTests|FullyQualifiedName~IdentityApiAuthorizationScopeTests|FullyQualifiedName~OrganizationAdminScopedConfigurationAuthorizationTests|FullyQualifiedName~OrganizationAdminEmailSettingsAuthorizationTests|FullyQualifiedName~OrganizationAdminProjectLifecycleConfigurationTests|FullyQualifiedName~ProtectedTenantAdminIdentityTests|FullyQualifiedName~PermissionCatalogArchitectureTests|FullyQualifiedName~FinancialApprovalDelegationBoundaryTests|FullyQualifiedName~UserPermissionAdministrationTests|FullyQualifiedName~SeedDataProviderTests"
```

**Exit:** 1 after interruption at the 60-second limit. **Tests discovered:** 0. **Passed/failed tests:** 0/0. The command did not reach test execution. `SeedDataProviderTests` was included in the API-project filter but is defined in a different project, so it would require a separate command; that project was not run.

## Additional .NET recovery attempts (2026-11-15)

### Focused Identity permission tests

```powershell
$testOut = Join-Path $env:TEMP ('pmo-rbac-focused-user-' + [guid]::NewGuid().ToString('N'))
dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:FocusedUserPermissions=true "-p:OutDir=$testOut\" --filter "FullyQualifiedName~UserPermissionAdministrationTests"
```

**Exit:** 1 after interruption during repeated IKVM Maven reference warnings. **Test cases discovered/executed:** no totals; no test case ran. Although `FocusedUserPermissions=true` removes API/MPP project references during evaluation, the existing `project.assets.json` still contains the full restored Maven/MPP graph; the no-restore attempt therefore continued IKVM reference generation. I did not restore or rewrite shared assets.

### ResourceManagement seed test attempts

Initial focused seed test command:

```powershell
$testOut = Join-Path $env:TEMP ('pmo-rbac-seed-' + [guid]::NewGuid().ToString('N'))
dotnet test apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:FocusedMinimalSeed=true "-p:OutDir=$testOut\" --filter "FullyQualifiedName~SeedDataProviderTests"
```

**Exit:** 1. The test project first exposed `SeedDataProviderTests.cs(213,74) CS8198`: an `out var` declaration inside the FluentAssertions expression-tree predicate. The test-only assertion was rewritten as explicit per-permission existence/scope/assignability assertions, preserving the original guarantee.

Retry of the full focused seed class:

```powershell
$testOut = Join-Path $env:TEMP ('pmo-rbac-seed-' + [guid]::NewGuid().ToString('N'))
dotnet test apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:FocusedMinimalSeed=true "-p:OutDir=$testOut\" --filter "FullyQualifiedName~SeedDataProviderTests"
```

**Exit:** 1; VSTest reported it could not connect to testhost after 90 seconds and aborted. One test DLL matched, but no test-case totals were reported; the testhost process was reported as still running (PID 24604). No test result is counted.

Bounded retry of two role-seed scenarios, with the repository's testhost parent-query workaround:

```powershell
$env:VSTEST_CONNECTION_TIMEOUT='180'
$testOut = Join-Path $env:TEMP ('pmo-rbac-seed-focused-' + [guid]::NewGuid().ToString('N'))
dotnet test apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:FocusedMinimalSeed=true -p:_MSTestEnableParentProcessQuery=false "-p:OutDir=$testOut\" --filter "FullyQualifiedName~SeedIdentityOnlyAsync_Rerun_SeedsMissingCatalogRowsAndPreservesOtherRoles|FullyQualifiedName~SeedMinimalAsync_Twice_SeedsSystemBaselineAndLeavesBusinessDataEmpty"
```

**Exit:** 1; coordinator-directed interruption at 120 seconds while awaiting testhost connection. One test DLL matched, but no test-case totals or outcomes were emitted.

Test-source compile check, without starting VSTest:

```powershell
$buildOut = Join-Path $env:TEMP ('pmo-rbac-seed-build-' + [guid]::NewGuid().ToString('N'))
dotnet build apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/NEXUS.Modules.ResourceManagement.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false -p:FocusedMinimalSeed=true "-p:OutDir=$buildOut\"
```

**Exit:** 0; 0 warnings, 0 errors, 5.82 seconds. This compiled `SeedDataProviderTests.cs` against existing project-reference binaries; it is not test execution. The test file now contains two additional unexecuted seed regressions: an unexpected active Organization Admin grant causes seed reconciliation to fail without removing grants/memberships; missing approved `OrganizationConfiguration.View/Manage` grants are restored idempotently without appointing an Organization Admin.

The test-only files changed in this recovery slice are `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/SeedDataProviderTests.cs` (assertion compatibility fix and two new regression tests). The earlier `EmailNotificationIntegrationTests.cs` interface forwarding method was already present from the preceding execution and was not changed in this recovery slice.

## Test interpretation

The passing React tests and isolated API build establish limited source/test compatibility only. The focused API tests and ResourceManagement seed tests remain **Not Run**. They do not verify the historical user-creation 403, persisted role classification, actual Organization Admin grant scope, migration data preservation, cross-tenant runtime isolation, or deployed UI/API parity. The final report remains blocked until the missing test and Development evidence is attached.


## Final recovery attempt — 2026-11-15

The test-only fixture correction in Security/FinanceMutationActorBindingTests.cs now passes a direct ManagementContext to FinancePermissionGuard.RequireWritableActorContext, retaining all existing actor, tenant, perspective, view-mode, scope and status assertions. It removes only the obsolete NEXUS.Api.Management using. The requested test-project build never reached C# compilation, so this change is implemented but not compiler-verified.

Additional commands and outcomes:

| Command | Exit | Result |
|---|---:|---|
| dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity quiet | 1 | MSBuild failed in _GetProjectReferenceTargetFrameworkProperties; 0 warnings/errors surfaced; C# compilation did not run. |
| dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity normal | 1 | Same pre-compilation reference resolution failure. |
| dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal --maxcpucount:1 | 1 (interrupted) | Reached NEXUS.Modules.ProjectPlanning.MppAdapter IKVM generation; IKVM0100/0101/0109 warnings; no test compile. |
| Focused dotnet test for delegation, Identity/API scope, Organization Admin, Protected Admin, catalog, finance delegation and user-permission tests, with --no-restore -m:1 -p:BuildProjectReferences=false and isolated OutDir | 1 (interrupted after ~80s) | IKVM still ran in NEXUS.Api.Tests.csproj; no compilation summary, discovery or test totals. Discovered 0; passed 0; failed 0; skipped 0. |
| dotnet restore ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --verbosity diagnostic --disable-parallel -p:FocusedUserPermissions=true | 1 | Restore graph traversal failed. MSB4276 reports missing Microsoft.NET.SDK.WorkloadAutoImportPropsLocator and Microsoft.NET.SDK.WorkloadManifestTargetsLocator in the installed .NET SDK 10.0.302 tree. No tests built. Five generated NuGet files' hashes match their pre-attempt values after restoration. |

The test gate is BLOCKED by MSBuild/SDK/IKVM setup before test discovery. No test assertion failed or passed; no browser UAT, database write or API request was performed in this recovery.

## Build-only phase reconciliation - 2026-11-15

The Product Owner explicitly deferred test execution for this phase. No .NET tests, React tests, test discovery, testhost, API functional tests, or browser UAT were invoked. The `NEXUS.Api.Tests` project was compiled only:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$out = Join-Path $env:TEMP ('pmo-rbac-tests-compile-' + [guid]::NewGuid().ToString('N'))
dotnet build 'C:/Projects/pmo/apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj' --no-restore --verbosity quiet -m:1 -p:BuildProjectReferences=false "-p:OutDir=$out/" '-clp:ErrorsOnly;Summary'
```

**Exit 0; 0 errors; 383 warnings.** This confirms C# project compilation only; it provides no discovered/passed/failed/skipped test counts. For this phase those counts are **not applicable because execution was explicitly deferred**, not evidence of passing tests. The earlier IKVM/testhost attempts remain historical records and were not repeated. Full build-only results are in [RBAC build-only handover](RBAC-BUILD-ONLY-HANDOVER-2026-11-15.md).

An integrated compile-only build was then run with project references enabled:

```powershell
Set-Location $env:TEMP
$out = Join-Path $env:TEMP ('pmo-rbac-integrated-tests-compile-' + [guid]::NewGuid().ToString('N'))
dotnet build 'C:/Projects/pmo/apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj' --no-restore --verbosity minimal -m:1 "-p:OutDir=$out/"
```

Using SDK `11.0.100`, it built SharedKernel, the referenced modules (including MppAdapter), Persistence, API and the API test project in one project-reference graph. **Exit 0; 389 warnings; 0 errors; 5:27.78.** No test discovery or execution occurred.
