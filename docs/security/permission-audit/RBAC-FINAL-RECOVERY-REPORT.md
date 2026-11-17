# RBAC Final Recovery Report

> **Current build-only phase status (2026-11-15): IMPLEMENTATION COMPLETE — BUILD VERIFIED — TESTS DEFERRED.** The API, Persistence, API.Tests compilation, all directly affected modules, and React/Vite production build now compile with zero errors. The former BLOCKED test-compilation status below is historical and superseded by this build evidence. No tests/runtime/API/UAT were run. Database migration and seed remain unapplied pending fresh authorized host verification. See [RBAC build-only handover](RBAC-BUILD-ONLY-HANDOVER-2026-11-15.md).

**Date:** 2026-11-15  
**Repository:** C:/Projects/pmo  
**Overall status:** **BLOCKED — NOT VERIFIED**  
**Scope:** Existing RBAC, Tenant Admin, Organization Admin, test recovery, Development migration and actual API verification. No browser UAT was performed.

## Executive disposition

The six test fixture call sites were corrected without changing production authorization code or assertions. The helper now supplies the ManagementContext required by FinancePermissionGuard.RequireWritableActorContext, preserving actor, tenant, perspective, view mode and empty management scope. The requested test build did not reach C# compilation: standard builds stopped during MSBuild project-reference resolution, and a serial attempt entered IKVM reference generation before being interrupted. No tests were discovered or executed.

The supplied host preflight report verified the Development database identity and one pending migration. The migration SQL was independently reviewed and its SHA-256 matches the preflight artifact. No role-row inventory was available, so no migration or seed was applied and preservation counts cannot be confirmed.

No Development API listener or serving assembly was visible from this executor, and process inspection was denied. No authenticated API request was made. The Project Manager onboarding 403 and Organization Admin configuration journeys therefore remain unverified. The independent reviewer found no newly introduced critical source-level regression, but did not clear the missing test, database or runtime gates.

## Gate status

| Gate | Status | Evidence and limits |
|---|---|---|
| React production build and focused React tests | **VERIFIED** (previous completed gate) | Prior report records a passing production build and focused Organization Admin tests. Not rerun in this recovery. See RBAC-TEST-EXECUTION-RESULTS.md. |
| Six test fixture call sites | **IMPLEMENTED — NOT VERIFIED** | Test-only change in apps/api/tests/NEXUS.Api.Tests/Security/FinanceMutationActorBindingTests.cs. Directly corrects the argument type at the six named calls; compiler did not reach C# compilation. |
| .NET test-project build | **BLOCKED** | Standard builds exited 1 before compilation. Serial build entered IKVM generation in NEXUS.Modules.ProjectPlanning.MppAdapter; it was stopped before test compilation. |
| Relevant .NET test execution | **BLOCKED** | Selected tests discovered: 0; passed: 0; failed: 0; skipped: 0. No test assertion ran. |
| Development target preflight | **VERIFIED** (host report only) | artifacts/ef-review/dev-db-preflight-inspect-20261115T094923Z-77e35528.json: Development, SQL Server Shady, database NexusPmo, DB_ID 5, Windows Authentication, model synchronized, 27/28 migrations applied, one pending. Exit code 2 reflects the pending migration. Last-applied physical schema matched; current model reports the expected missing Roles.RoleKey column and UX_Roles_Tenant_RoleKey index. |
| Migration source and generated SQL review | **VERIFIED** (static review) | Exact artifact SHA-256: 29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B. The SQL changes Roles and EF migration history only. It does not modify role IDs, user-role memberships, role-permission links, direct overrides or audit rows. |
| Development migration and seed execution | **BLOCKED** | No current host re-confirmation or live role inventory was available to this executor. No migration or seed was applied. Actual system-role inventory, pre/post preservation counts and seed idempotency are unknown. |
| Actual serving API identity | **BLOCKED** | No listeners were visible on ports 5000, 5001, 5002, 4100 or 4101. CIM process inspection returned access denied. The historical port 5002 runtime is not proof of the current serving assembly. |
| Tenant Admin to create user to Project Manager | **BLOCKED** | No authenticated POST /api/users request, trace ID, persisted membership readback or effective-permission comparison was made. The historical HTTP 403 root cause is not confirmed by this recovery. |
| Organization Admin configuration boundaries | **BLOCKED** | No authenticated API request was made. A valid existing Organization Admin membership and authoritative Organization scope were not established. Do not create such an actor by direct database patch; appointment and recovery policy remain unresolved. |
| Independent critical review | **VERIFIED** (source/artifact review only) | Reviewer found the migration structurally transactional and fail-closed, and no new critical source regression. Reviewer explicitly did not clear test execution, live data preservation or API gates. |

## Exact .NET verification attempts

1. Command: dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity quiet  
   Exit 1. Build failed with 0 warnings and 0 surfaced errors during _GetProjectReferenceTargetFrameworkProperties; C# compilation did not run.

2. Command: dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity normal  
   Exit 1. Same pre-compilation MSBuild project-reference resolution failure; 0 warnings and 0 surfaced errors.

3. Command: dotnet build ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal --maxcpucount:1  
   Exit 1 after interruption. Serial evaluation progressed into NEXUS.Modules.ProjectPlanning.MppAdapter IKVM reference generation and emitted IKVM0100/0101/0109 warnings, including unsupported Java class-format warnings. It did not reach test compilation.

4. Focused selected-test attempt:

   $testOut = Join-Path $env:TEMP ('pmo-rbac-focused-' + [guid]::NewGuid().ToString('N'))
   dotnet test ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --verbosity minimal -m:1 -p:BuildProjectReferences=false "-p:OutDir=$testOut/" --filter 'FullyQualifiedName~AdminDelegationAuthorityRecoveryTests|FullyQualifiedName~IdentityApiAuthorizationScopeTests|FullyQualifiedName~OrganizationAdminScopedConfigurationAuthorizationTests|FullyQualifiedName~OrganizationAdminEmailSettingsAuthorizationTests|FullyQualifiedName~OrganizationAdminProjectLifecycleConfigurationTests|FullyQualifiedName~ProtectedTenantAdminIdentityTests|FullyQualifiedName~PermissionCatalogArchitectureTests|FullyQualifiedName~FinancialApprovalDelegationBoundaryTests|FullyQualifiedName~UserPermissionAdministrationTests'

   Exit 1 after approximately 80 seconds. IKVM still ran inside NEXUS.Api.Tests.csproj despite BuildProjectReferences=false; no C# build summary, test discovery or test totals appeared. The run was interrupted. Discovered 0; passed 0; failed 0; skipped 0.

5. Focused restore attempt:

   dotnet restore ./apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --verbosity diagnostic --disable-parallel -p:FocusedUserPermissions=true

   Exit 1. Restore graph traversal failed in _GetAllRestoreProjectPathItems / _GenerateRestoreProjectPathWalk. MSBuild reported MSB4276: the installed .NET SDK resolver could not find Microsoft.NET.SDK.WorkloadAutoImportPropsLocator or Microsoft.NET.SDK.WorkloadManifestTargetsLocator beneath C:/Program Files/dotnet/sdk/10.0.302/Sdks/.... No test build or discovery ran. The five generated NuGet files were verified against their pre-attempt SHA-256 values after restoration; ORIGINAL_GENERATED_ASSETS_RESTORED=True.

No assertions failed because no test case executed. Treat these as build/SDK environment blockers, not passing test evidence.

## Migration and data-integrity assessment

The pending migration is 20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification. Its SQL drops and recreates the same-named check constraint inside the generated transaction, adds nullable RoleKey, performs fail-closed uniqueness and identity checks, copies existing SystemRoleKey values into RoleKey, reclassifies unapproved tenant System Roles, and adds the filtered unique index plus replacement check constraint.

The source and SQL do not delete or recreate role rows and do not write membership, grant, override, audit or business tables. This is a structural review only. It does not prove actual Development row state, role-key collision absence beyond runtime migration guards, stable row counts after application, or seed idempotency. Because the requested live role inventory and a fresh host-side target confirmation were not available, the migration remains not applied.

## Runtime and security evidence

- GET /api/roles and role detail endpoints are available in source for authorized role inventory, but no live serving API was reachable here.
- The API process/assembly, active environment and authenticated actor were not established. No bearer credentials were obtained or used.
- No POST /api/users request was made. No request trace, audit row, membership persistence or effective permission delta was observed.
- No Organization Admin allow/deny request was made. Source review confirms the design requires canonical Organization Admin identity, active same-tenant membership, an explicit Organization scope grant and freshly evaluated permissions; role name alone does not create scope.
- The minimal seed previously described creates only Tenant Admin membership. Existing Organization Admin appointment/recovery rules remain unresolved, and ordinary role assignment deliberately fails closed for that appointment.

## Independent review and remaining decisions

The independent reviewer found no new critical source-level weakness in delegation checks, Organization scope resolution, seed reconciliation or the migration. The reviewer did not certify test or runtime behavior.

Product Owner decisions remain required for Organization Admin appointment/removal/recovery, protection, cardinality and last-admin rules; BusinessUnit boundaries; compatibility of Users.ManageAccounts with Users.View; and a concrete Tenant-enforced configuration example for OA-07. These decisions must not be inferred from this recovery.

### Role inventory evidence required

Before applying the pending migration, capture a read-only role/membership/grant/override/audit baseline through an approved host-side mechanism that is compatible with the currently applied schema. The preflight report does not contain these rows. Do not call the current repository API against the pre-migration database unless the loaded assembly is confirmed schema-compatible; its current EF model expects the pending RoleKey column. Do not use manual SQL, direct patches or an unverified old API process.

After the migration and seed gates pass, a verified current API can provide a post-migration role readback through GET /api/roles and GET /api/roles/{id}. These endpoints require Roles.View and expose role identity/classification, assigned-user counts, permission counts and permission details. Compare these post-migration records with the authorized pre-migration baseline. If no approved compatible baseline collection method is available on the host, do not apply the migration; keep the data-preservation gate blocked and return the sanitized host evidence for review.

## Host-side continuation instructions

The executor cannot perform Windows-host database or API actions. On the authorized Development host:

1. Reconfirm target and migration state using:
   Set-Location 'C:/Projects/pmo'
   ./scripts/dev/db-preflight.ps1

   Stop if the target is not Development Shady/NexusPmo, the generated SQL differs, physical-schema drift appears, or any preflight invariant fails. Capture a read-only authorized role inventory and role/user permission baseline before any migration; the preflight report does not include these rows or preservation counts.

2. Recompute the reviewed SQL hash:
   Get-FileHash -Algorithm SHA256 'artifacts/ef-review/dev-pending-20261115T094740Z-eacb7b37.sql'

   Expected SHA-256: 29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B. If identity, pending migration, path or hash differs, stop and obtain a new review.

3. Only after the exact target and role baseline are reviewed, apply through the documented migration gate (never run the SQL artifact directly):
   ./scripts/dev/db-apply-migrations.ps1 -ReviewedSqlPath 'artifacts/ef-review/dev-pending-20261115T094740Z-eacb7b37.sql' -ApprovedSha256 '29AF00E3847B795A261405258EDC8F8F524B880C89A7B5262AAADF1A60E1F85B' -AuthorizationReference '2026-11-15 FOPX PM P0 Final RBAC Recovery and Runtime Verification' -ConfirmDevelopmentApply

   Then run ./scripts/dev/db-migrations-status.ps1 and record migration history, physical schema, actual System Role inventory, stable IDs and before/after membership/grant/override/audit counts. Verify seed idempotency with the repository's approved seed workflow; do not use Demo Seed or direct SQL patches.

4. On the host, identify any API listener and the exact loaded NEXUS.Api assembly/hash without sharing credentials. Use an existing valid Tenant Admin session for the PM onboarding API scenario. Run the Organization Admin matrix only if an already-authorized Organization Admin actor and scope grant exist; otherwise leave it blocked pending the Product Owner appointment decision. Capture sanitized response status, trace IDs and readback evidence.

## Final recommendation

**BLOCKED — NOT VERIFIED.** The test fixture correction is present but uncompiled. The migration is reviewed but not applied. No .NET tests ran. Neither critical journey was exercised against the actual Development API. Do not claim the repeated 403 is resolved or begin Product Owner browser UAT based on the evidence in this report.
