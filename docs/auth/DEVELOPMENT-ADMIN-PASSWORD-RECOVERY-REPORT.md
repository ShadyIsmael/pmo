# Development Admin Password Recovery — Completion Report

Date: 2026-11-24
Status: LATEST P0 RUNTIME PACKAGE FIXED AND OFFLINE-VERIFIED; Product Owner local Development recovery remains pending.

## Diagnosis

The recovery console relied on Console.ReadKey for hidden password entry. That reads a console keyboard handle and does not provide a reliable PowerShell/CMD hidden-input path, especially when input is redirected. The recovery flow now explicitly rejects direct keyboard entry and is launched by a PowerShell 7 procedure using Read-Host -AsSecureString.

The PowerShell procedure transfers four UTF-8/Base64 lines through the child process standard input only after the API emits a readiness marker. The marker is emitted only after Development, SQL Server/database identity, EF model, and pending-migration preflight checks pass. Base64 is used as a transport encoding; no password is written to process arguments, environment variables, command history, logs, or files.

## Implemented safeguards

- Development-only command and service.
- Exact expected SQL Server Shady and database NexusPmo. Runtime checks SQL Server provider, pending model changes, pending migrations, SERVERPROPERTY('ServerName'), DB_NAME(), and DB_ID() before asking for passwords.
- Canonical seeded Admin UserId, TenantId, and exact email validation; inactive, deleted, or non-local accounts are rejected.
- ASP.NET Identity PasswordHasher remains the hasher.
- Temporary and permanent passwords are independently confirmed, bounded by configured minimum/maximum length, and required to differ. Permanent password change still uses the existing initial-password operation and its policy checks.
- Strict UTF-8 transport, quote/special-character support, fixed-time confirmation comparisons, rejection of direct input, malformed/canceled/truncated/extra input, and no password echo in output or errors.
- Recovery audit data contains state/expiry only. Existing sessions are revoked. The command verifies temporary login, forced first-login password change, and permanent login, then closes its verification session.
- No role, permission, tenant, membership, or authentication bypass changes.

## Verification evidence

| Check | Result |
|---|---|
| UPI Task Routing | PASS. Read AGENTS.md, docs/architecture/INDEX.md, and the installed Universal Project Intelligence skill. Routing selected Identity; the recovery console, service, Program entry point, and tests were included as explicit task-required scope. The router did not identify the console as a primary file, so that limited scope expansion is recorded here. |
| UPI refresh and validate | PASS after source changes; zero index validation errors/warnings. Snapshot: 1,916 files, 15,111 symbols, 75,793 dependency edges. |
| API build | PASS, 0 errors. 353 warnings, primarily existing IKVM conversion warnings and nullable warnings. The MppAdapter output was locked; the build used a temporary no-copy reference to the already-built adapter, without changing repository build configuration or stopping an unverified process. |
| Focused recovery checks | PASS, 16 passed / 0 failed: 11 console-input cases and 5 recovery lifecycle cases. The cases cover quotes/special characters, Unicode, hidden/non-echo output, confirmation for each password, min/max lengths, temporary/permanent difference, cancellation and malformed/truncated/extra input, canonical Admin ID/email, Development-only guard, hashed temporary password, expiry, session revocation, audit secrecy, and permanent-password length/reuse rejection. |
| Test runner limitation | VSTest could not connect to its testhost over localhost in this environment. The 16 test methods and their assertions were executed successfully through an isolated temporary harness; this is not reported as a successful VSTest session. |
| PowerShell script syntax | PASS using the PowerShell parser. |
| SQL Server execution | Not run by the Agent, as required. |

## Product Owner procedure

Run from Windows PowerShell 7 in the checked-out repository:

    & 'C:\Projects\pmo\scripts\development\Recover-DevelopmentAdminPassword.ps1'

The script sets the child process environment to Development and supplies the authorized Shady/NexusPmo Windows Integrated Security connection. It requests the four password entries with hidden input only after target preflight. If the target or migration preflight fails, it stops before requesting or transmitting any password. On success, it confirms the temporary-to-permanent flow and displays no password.

## Database gate evidence and remaining execution

- Model synchronized: Not verified by the Agent; recovery runtime refuses pending EF model changes.
- Pending migration count/names: Not queried by the Agent; recovery runtime refuses any pending migration and reports its names before password collection.
- Target environment/database identity: Not live-verified by the Agent; the Product Owner's procedure sets Development and runtime verifies the actual Shady/NexusPmo target and database ID.
- Backup status: Not requested; none created by the Agent.
- Migrations applied: No.
- Required seed mode/idempotency: Not run; recovery does not invoke seed operations.
- Physical schema: Not independently inspected by the Agent; runtime preflight checks EF model/migration state and live database identity.
- Persistence tests: Focused recovery lifecycle assertions ran against isolated in-memory SQLite; no SQL Server persistence test was run.
- Blocker: Product Owner must run the provided procedure locally. A failed identity or migration preflight stops before password collection. No migration, seeding, reset, or direct SQL modification was performed by the Agent.

## P0 follow-up — preflight subprocess failure

Date: 2026-11-24
Status: FIXED AND MOCK-VERIFIED; Product Owner's local Development recovery remains pending.

### Confirmed cause and diagnostic limit

The failed script launched the child with the equivalent of:

    dotnet run --project <NEXUS.Api.csproj> --no-build -- --recover-development-admin-password --admin-recovery-expected-sql-server Shady --admin-recovery-expected-sql-database NexusPmo

The wrapper redirected stdout but not stderr. When stdout closed before the readiness marker, it waited for the child and threw a generic message without reading the child's exit code or stderr. With CreateNoWindow enabled, the actual safe application diagnostic was not reliably visible. This is the confirmed reason the Product Owner saw only the generic wrapper error.

The supplied failed run did not record the child exit code, so its historical numeric code cannot be reconstructed. No evidence from that output proves that SQL identity or migration validation was the failure. The inner preflight exception also cannot be established without a live preflight, which the Agent did not run.

The NEXUS.Api launch profile was inspected with values handled safely. It sets ASPNETCORE_ENVIRONMENT=Development and local application URLs only; it does not override the authorized database connection string or select a different database. The wrapper had configured both DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT as Development, and the profile repeated Development for ASPNETCORE_ENVIRONMENT. The launch-profile message was incidental to dotnet run, not evidence of an environment conflict.

The existing API Debug output was present. The revised script verifies the expected assembly exists before starting it.

### Targeted fix

The PowerShell procedure now launches the existing NEXUS.Api.dll directly, with its working directory set to the API project directory. It no longer invokes dotnet run or loads launchSettings.json. The script sets both Development environment variables and the authorized Windows Integrated Security connection on the child process only.

The wrapper now redirects and drains both stdout and stderr, reports the child exit code, and shows safe child diagnostics on failure. Connection strings and credential fields are redacted. It still waits for the readiness marker emitted after target/model/migration preflight and does not call Read-Host before that marker. The API's exact Development target validation and the secure stdin password flow are unchanged.

### Follow-up verification

| Check | Result |
|---|---|
| PowerShell recovery tests | PASS, 25 passed / 0 failed. |
| Absolute path and different caller directory | PASS. The complete script was invoked by absolute path while the caller working directory was C:\Windows\System32. |
| Mocked failure | PASS. A database-free mock child returned exit code 37 and a synthetic diagnostic containing connection/credential fields. The script displayed the exit code and redacted diagnostic; the password reader was called zero times. This mock exit code is not the historical Product Owner process exit code. |
| Mocked success | PASS. A database-free mock child emitted the readiness marker, received four Base64 stdin lines from four mocked SecureString prompts, and returned success. No plaintext mock value appeared in output. |
| Environment and launch-profile handling | PASS. The mock confirmed both environment variables were Development, target settings were present, the API DLL was the first child argument, and the child working directory was the API project. No launch profile was loaded. |
| PowerShell parsing | PASS. |
| API build | PASS, 0 errors / 352 warnings, built to an isolated temporary output with project-reference builds disabled to avoid DLL locking. The C# API source was not changed in this follow-up. Warnings were primarily existing IKVM Java-class conversion warnings. |
| Live database / actual recovery | NOT RUN. No SQL Server connection, recovery operation, migration, seeding, or reset was performed by the Agent. |

UPI routing was run after reading AGENTS.md, the architecture index, and the installed UPI skill. The router matched Program.cs to the unrelated Programs module; the explicitly requested recovery files were then inspected as the bounded task scope. No unrelated financial or application modules were opened. Final UPI refresh and validate passed with zero errors/warnings: 1,916 indexed files, 15,111 symbols, and 75,793 dependency edges. The generated snapshot was unchanged because the configured index excludes these PowerShell files.

### Files changed in this follow-up

- scripts/development/Recover-DevelopmentAdminPassword.ps1
- scripts/development/tests/Test-DevelopmentAdminPasswordRecovery.ps1
- docs/auth/DEVELOPMENT-ADMIN-PASSWORD-RECOVERY-REPORT.md

### Product Owner action and database status

Run the same command from Windows PowerShell 7, including when the current directory is C:\Windows\System32:

    & 'C:\Projects\pmo\scripts\development\Recover-DevelopmentAdminPassword.ps1'

If preflight fails, the revised procedure now prints the child exit code and redacted diagnostic before stopping without asking for a password. If it passes, enter a fresh temporary password and a different policy-compliant permanent password at the hidden prompts.

- Model synchronized: Not verified by the Agent; runtime recovery checks for pending EF model changes.
- Pending migration count/names: Not queried by the Agent; runtime recovery refuses pending migrations and names them before password collection.
- Target environment and database identity: Not live-verified by the Agent; runtime verifies Development, SQL Server provider, SERVERPROPERTY('ServerName'), DB_NAME(), and DB_ID() against Shady/NexusPmo.
- Backup: Not requested; none created by the Agent.
- Migrations applied: No.
- Seed mode/idempotency: Not run; recovery does not invoke seed operations.
- Physical schema: Not independently inspected by the Agent; runtime checks EF model/migration state and target identity.
- Persistence tests: No SQL Server persistence tests run. The recovery service's prior isolated in-memory SQLite tests remain documented above.
- Remaining blocker: Product Owner must execute the final local recovery. If preflight still fails, the new safe diagnostic and exit code will identify the actual inner failure.

## P0 follow-up - missing runtime assembly

Date: 2026-11-24  
Status: FIXED AND OFFLINE-VERIFIED; Product Owner local Development recovery remains pending.

### Confirmed root cause

The launcher selected `apps/api/src/NEXUS.Api/bin/Debug/net10.0/NEXUS.Api.dll` and launched it directly. That directory contained the API assembly and its `.deps.json`, but did not contain `NEXUS.Modules.ProjectPlanning.MppAdapter.dll`; the selected `.deps.json` also had no `NEXUS.Modules.ProjectPlanning.MppAdapter/1.0.0` runtime entry. The API project does have a direct `ProjectReference` to MppAdapter, and the API assets graph listed it. MppAdapter's own build output was present. Its project references ProjectPlanning and includes MPXJ.Net and System.Drawing.Common dependencies. This confirms an incomplete/inconsistent API runtime output, rather than a missing project reference. The API's transitive MPXJ/IKVM dependency set therefore could not be trusted from that selected output either.

### Permanent fix

The recovery launcher now creates a unique temporary runtime directory and runs a fresh serialized `dotnet publish` there. A temporary conditional MSBuild props file redirects only the NEXUS.Api build output into that isolated directory. Project-reference builds are disabled so the recovery command does not rebuild MppAdapter or overwrite DLLs that a running API may have loaded; the existing project-reference outputs are used for the publish. The temporary props file and intermediate API output are removed after publish.

Before starting the child process or constructing the recovery connection string, the launcher parses the published API `.deps.json` and checks the published API DLL, `.deps.json`, runtimeconfig, and each declared runtime/native/resource asset. It also requires the MppAdapter, ProjectPlanning, MPXJ.Net, mpxj, IKVM.ByteCode, and System.Drawing.Common entries and files. Only then does it launch the API DLL from the package directory as its working directory. The existing Development environment, Shady/NexusPmo target checks, readiness-before-password behavior, secure input confirmation, and safe diagnostic handling remain in force.

### Build and runtime dependency verification

| Check | Result |
|---|---|
| Actual isolated API publish | PASS, `dotnet publish` exit code 0. The publish produced a self-consistent API runtime package without rebuilding project references. |
| Shared output protection | PASS. `NEXUS.Api.dll` and `NEXUS.Modules.ProjectPlanning.MppAdapter.dll` timestamps were unchanged by the final publish. The temporary MSBuild props file and intermediate build output were removed. |
| `.deps.json` and physical runtime assets | PASS. The final package's selected `.NETCoreApp,Version=v10.0` target contained 125 runtime/native/resource assets, and the launcher's validator found every declared asset. `NEXUS.Api.dll`, `NEXUS.Api.deps.json`, `NEXUS.Api.runtimeconfig.json`, and MppAdapter were present together in that package. |
| Runtime dependency resolution | PASS. A .NET `AssemblyDependencyResolver` probe using the final published API path loaded MppAdapter 1.0.0.0, ProjectPlanning 1.0.0.0, MPXJ.Net 16.10.0.0, mpxj 16.10.0.0, IKVM.ByteCode 9.3.4.0, and System.Drawing.Common 10.0.0.0 from the package. This covers the reported missing assembly and its relevant ProjectPlanning/MPXJ/IKVM transitive chain. |
| API recovery entry point and Development-only safeguard | PASS. From `C:\Windows\System32`, the final published API entry point ran with both environment variables set to Production and an empty recovery connection-string variable. It rejected the recovery option at the Development-only guard before database access. Exit code `-532462766` is the expected unhandled guard exception for this smoke check; output contained no `FileNotFoundException` or assembly-load error. |
| Focused PowerShell launcher suite | PASS, 31 passed / 0 failed. It includes an absolute-path invocation from `C:\Windows\System32`, isolated publish/output checks, mocked preflight success and failure, password-read prevention on failed preflight, secure collection only after mocked readiness, and safe diagnostic redaction. |
| PowerShell syntax | PASS, checked by the focused suite's parser step. |
| UPI route, refresh, and validation | PASS. Route manifest: `docs/architecture/task-scopes/20261124T130758-fix-development-admin-recovery-runtime-packaging-p-e3f01.md`. Refresh snapshot `20261124T135155-0022972`; validation returned 0 errors and 0 warnings. No application C# files were changed in this follow-up. |
| Live recovery, database, and password | NOT RUN. No SQL Server connection, live password prompt, recovery operation, migration, seed, or reset was performed. |

### Changed files in this follow-up

- `scripts/development/Recover-DevelopmentAdminPassword.ps1`
- `scripts/development/tests/Test-DevelopmentAdminPasswordRecovery.ps1`
- `docs/auth/DEVELOPMENT-ADMIN-PASSWORD-RECOVERY-REPORT.md`

### Product Owner command and remaining database boundary

Run from Windows PowerShell 7, including when the caller's current directory is `C:\Windows\System32`:

    & 'C:\Projects\pmo\scripts\development\Recover-DevelopmentAdminPassword.ps1'

The launcher publishes and validates the isolated runtime before it prompts for any password. The real recovery remains pending because its preflight must verify the live Development database identity and migration/model state. No live password was requested by the Agent.

- Model synchronized: Not verified by the Agent; recovery preflight checks for pending EF model changes.
- Pending migration count/names: Not queried by the Agent; recovery preflight checks and reports pending migrations before password collection.
- Target environment and database identity: Not live-verified by the Agent; recovery preflight verifies Development and the actual Shady/NexusPmo server/database identity.
- Backup status: Not requested; none created.
- Migrations applied: No.
- Required seed mode/idempotency: Not run; recovery does not invoke seed operations.
- Physical schema: Not independently inspected by the Agent; no database connection was made.
- Persistence tests: Not run against SQL Server; runtime packaging and mocked launcher checks are offline.
- Blocker: Product Owner must execute the command above to complete the guarded live preflight and recovery.

