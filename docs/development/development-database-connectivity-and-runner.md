# Development database connectivity and EF migration runner

**Scope:** Local Development SQL Server only (`Shady` / `NexusPmo`). This workflow does not support UAT or Production.

## Diagnosis from the Agent executor

The exposed PowerShell executor reports `COMPUTERNAME=SHADY`, but that value does not prove it shares the Windows host's network namespace or loopback. From this executor:

- TCP probes to `localhost:1433` and `Shady:1433` fail.
- The EF gate fails while opening TCP, before SQL Server authentication or database identity queries, with `TCP Provider: Access is denied`.
- Windows CIM and Service Control Manager inspection are denied, so this executor cannot establish whether the SQL Server service is installed or running on the underlying Windows host.
- No host-execution, TCP-forwarding, or managed-environment diagnostic tool is exposed in this session. The available shell is the restricted Agent executor.

This evidence locates the immediate failure before SQL authentication. Together with the Product Owner's report that host-side Windows Authentication succeeds and the repository's prior host-connected PowerShell record, executor isolation or host OS/network policy is more likely than an incorrect SQL connection string. The exact isolation boundary is not observable here: `localhost` could not be proven to refer to the host loopback, and OS/service inspection is blocked. Do not change SQL Server exposure, Windows Firewall, SPNs, or authentication based on this Agent-side failure.

The existing `docs/development/ef-core-database-workflow.md` contains a 2026-11-11 record claiming a successful migration apply to database ID 5. The Project Lifecycle handover in this worktree records no apply and an inaccessible TCP endpoint. Treat those records as conflicting until a fresh host-side preflight confirms current identity and migration history; this runner does not infer live state from a report.

## Tools

The wrappers delegate to the existing `scripts/database/ef-database-gate.ps1` and `apps/api/tools/NEXUS.EFDatabaseGate`. The EF gate owns connection validation, the `AppDbContext` model, migration discovery, schema inspection, target-specific SQL generation, reviewed-hash comparison, `MigrateAsync`, and post-apply verification. The gate itself also pins read, script, and apply operations to Development `Shady/NexusPmo`; changing caller-supplied expected-server/database arguments cannot widen the target. No `sqlcmd`, handwritten SQL, or general command execution is used.

| Script | Effect |
|---|---|
| `scripts/dev/db-preflight.ps1` | Runs the offline model check, then read-only live inspection; writes a target-specific pending SQL file only when migrations are pending, plus sanitized JSON reports. |
| `scripts/dev/db-migrations-status.ps1` | Read-only live inspection of identity, EF history, pending migrations, and physical schema; writes a sanitized JSON report. |
| `scripts/dev/db-apply-migrations.ps1` | Applies only EF migrations after a reviewed SQL file/hash, explicit authorization reference, and confirmation switch; the EF gate regenerates and compares the exact SQL before `MigrateAsync`, then verifies history/schema. |

All tools require PowerShell 7, .NET 10, the repository checkout, and the configured Development API endpoint. The current process environment is set to Development only when unset; any non-Development `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` is rejected. If `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` is not already supplied securely, the wrapper reads the effective connection override or Development appsettings value in memory. The connection string is not added to process arguments, console output, or reports. The EF gate requires Windows Integrated Security, rejects SQL username/password fields, and checks that the supplied endpoint matches the API's effective Development endpoint before opening SQL.

Reports include the operation, expected target, timestamps, exit code, a SHA-256 fingerprint of the authorization reference when applicable, and sanitized EF gate output. The reference text and connection string are never recorded. Pre-gate validation failures also produce a report when the report directory is writable. Reports and pending SQL default to `artifacts/ef-review/`.

## Host-side execution and verification

When the Agent shell cannot reach SQL Server, run this exact read-only preflight command from PowerShell 7 on the authorized Windows host with the repository checkout:

```powershell
Set-Location 'C:\Projects\pmo'; .\scripts\dev\db-preflight.ps1
```

The wrapper obtains the Development connection from the existing API configuration or process-level connection override, sets Development environment variables in its child process, and does not print the connection value.

Review the output and sanitized JSON report. A successful preflight must show all of the following before any apply:

1. `EF Model synchronized: Yes`.
2. `Target Development DB verified: Yes` with live `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID matching `Shady/NexusPmo`.
3. `Windows Authentication: Yes` after a successful SQL connection.
4. Applied and pending migration IDs, history-only IDs, and physical-schema findings.
5. The generated pending SQL path, if migrations are pending.

If no migrations are pending and schema checks pass, stop without applying. The wrapper reports no SQL artifact when EF finds no pending migrations. If migrations are pending, review the exact generated SQL, obtain explicit Product Owner authorization, and calculate its hash:

```powershell
Get-FileHash -Algorithm SHA256 'artifacts/ef-review/<generated-pending-file>.sql'
```

Only the Main Agent may initiate the write, using the reviewed path and hash:

```powershell
.\scripts\dev\db-apply-migrations.ps1 `
  -ReviewedSqlPath 'artifacts/ef-review/<generated-pending-file>.sql' `
  -ApprovedSha256 '<reviewed-64-character-SHA256>' `
  -AuthorizationReference '<active Product Owner task or conversation reference>' `
  -ConfirmDevelopmentApply
```

The authorization reference is an audit pointer, not a substitute for the active explicit approval. The apply wrapper is restricted to `Shady/NexusPmo`; do not use it for an unreviewed migration, an unverified database, or another environment. A Development backup is optional. After apply, exit code zero plus the gate's post-apply output/report must confirm migration history and physical schema. Record persistence test results separately.

If host execution is unavailable to the Agent, do not retry from the isolated shell. Provide the single preflight command above and have the authorized Windows operator return its sanitized report. The Agent must not create a scheduled task, Windows service, tunnel, public SQL endpoint, or unrestricted host command runner to bridge the isolation boundary.

## Safe failure verification and current limits

From an isolated executor, the preflight must exit nonzero before any write when SQL is unreachable. The `apply` wrapper separately refuses a missing confirmation switch, missing authorization reference, nonexistent SQL file, invalid or mismatched SHA-256, non-Development environment, non-Integrated-Security or alternate-authentication connection, endpoint mismatch, unexpected live identity, or any gate rejection. It passes only fixed EF gate arguments; it accepts no raw SQL command.

This Agent session cannot validate live `Shady/NexusPmo` identity, migration history, a pending migration, or host-side apply. A previous Development apply is described in repository documentation, but the conflicting records require fresh host verification. No database write was performed while implementing these tools.
