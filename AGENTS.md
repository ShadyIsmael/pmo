# Repository agent instructions

## Supported Frontend and Audit Scope

The only supported frontend is apps/web-react (React, Vite, React Router). apps/web was retired on 2026-10-27 under ADR-009. Do not recreate, build, deploy, or include the removed app in future audits. Frontend dependency reviews target apps/web-react/package.json and its lockfile. Next.js mentions in historical ADRs and reports are not active scan targets.



## EF Core and database workflow

For any change to entities, relationships, `AppDbContext`, persistence mappings, migrations, SQL Server schema, persisted models, or missing-object/schema errors:

1. Read the relevant project Markdown documentation before inspecting or changing persistence code.
2. Use the repository's documented EF Core migration and seeding workflow and the installed EF Core reference skills.
3. Identify the actual context, migration assembly, startup project, environment and database target. Never infer database identity from `localhost`, a health endpoint, or a configuration filename.
   - If a restricted execution context fails before SQL identity/history checks with `TCP Provider: Access is denied`, treat it as an execution-context boundary. Do not retry host aliases, change SQL Server exposure, or use manual SQL. Run the same documented EF gate read-only `inspect -NoBuild` once from an approved Windows host context, using the securely loaded Development API connection and verify `SERVERPROPERTY('ServerName')`, `DB_NAME()`, and database ID from that run. Generate target-specific SQL only after that inspection succeeds; apply only through the documented gate after review and explicit authorization. If host-side execution is unavailable or also fails, stop and report the blocker.
4. Use the repository's repeatable EF migration tooling. Review generated migration SQL and verify the actual target before any write.
5. Never modify Production. Development and UAT database operations do not require a backup before migrations or other authorized changes. Do not require independent backup storage or `RESTORE VERIFYONLY`, and do not block an operation solely because a backup is absent. Create an optional backup only when explicitly requested. These rules do not relax target/environment identity, migration review, history, model, or physical-schema checks. Production remains outside the permitted write scope and retains its mandatory backup and recovery protections for any separately approved workflow.
6. Destructive operations (including drop, reset, delete, recreate, truncate, or other intentional data loss) still require explicit Product Owner authorization recorded in the active task/conversation and written confirmation of the exact database name. The only permitted reset path remains the repository's narrowly scoped reset of the disposable Development database on SQL Server `Shady` named `NexusPmo`; it must verify the requested Development environment, live `SERVERPROPERTY('ServerName')`, `DB_NAME()` and database ID, and that the active API Development connection resolves to the same exact target before destructive work. Do not infer reset authority from the optional-backup policy. No other database, environment, host, Production target, migration-history reset, migration removal, or rewrite of an applied migration is permitted. Preserve data and concurrent work everywhere else.
7. Apply only reviewed pending Development migrations when explicitly authorized, then verify migration history and physical schema. A clean system reset is complete only after all EF migrations and Minimal Seed have succeeded and the post-reset checks confirm exactly one active Admin user, the complete protected Admin permission baseline, and no demo/business records. The separate Demo Seed must never be used to satisfy a clean-bootstrap request.
8. Run relevant persistence tests against the intended isolated database and report evidence truthfully.

### Standard Development SQL Server workflow

Use the host-capable wrappers documented in [Development database connectivity and runner](docs/development/development-database-connectivity-and-runner.md):

1. From PowerShell 7 on the authorized Windows Development host, run `scripts/dev/db-preflight.ps1` to check model synchronization, Windows Authentication connectivity, live SQL Server/database identity, EF history, pending migrations, and physical schema. It writes a target-specific pending SQL artifact when migrations are pending, plus a sanitized execution report.
2. Use `scripts/dev/db-migrations-status.ps1` for a read-only migration/history/schema status check.
3. Only the Main Agent may initiate a database write. After explicit Product Owner authorization, review the exact SQL artifact and its SHA-256, then use `scripts/dev/db-apply-migrations.ps1` with that file/hash, the active authorization reference, and `-ConfirmDevelopmentApply`. The wrapper delegates to the EF gate; it does not execute caller-supplied SQL.
4. The wrappers are restricted to `ASPNETCORE_ENVIRONMENT=Development`, `DOTNET_ENVIRONMENT=Development`, and the expected `Shady/NexusPmo` target. They reject non-Windows-authentication connection strings, mismatched API endpoints, and unexpected identities. Never run them against Production or UAT.
5. If the Agent executor cannot reach the Windows host, do not retry the SQL connection from the isolated executor or create a network tunnel/service. Provide the exact host-side preflight command and verification procedure from the linked document. Treat the host result as evidence only after reviewing its sanitized report.

Do not mark database work complete while required Development model/schema synchronization is pending or unverified. Every completion report must include: model synchronized; pending migration count and names; target environment and database identity verified; backup status (`Not requested` is valid for Development/UAT); migrations applied; required seed mode and idempotency verified; physical schema verified; persistence tests; and blockers. Use `Not run` or `No` when evidence is unavailable.


## Frontend data integrity

All frontend changes must comply with the frontend-data-integrity skill. Operational data must originate from authoritative sources. Hardcoded business values, unapproved currency fallbacks, untranslated user-facing labels, and misleading status placeholders are prohibited.


## Permission-first development

Before implementing any feature, workflow, API, or user action:

1. Identify required permissions and authorization scopes. Reuse existing permission keys when their meaning and scope match; add new keys only when necessary.
2. Register every new permission in the authoritative Permission Catalog and expose it in Roles & Permissions under the correct category with accurate English and Arabic labels.
3. Enforce authorization in backend API endpoints and service operations, and apply matching permission checks to frontend routes and actions.
4. Enforce Tenant, Organization, Project, and resource-level boundaries. Do not authorize by hard-coded role names or implicitly grant administrative privileges.
5. Preserve existing role grants. Do not automatically grant a new sensitive permission to existing roles without an approved authorization policy.
6. Document the permission matrix and add positive, negative, and cross-scope scenarios to the Master UAT Checklist.

Runtime permission testing may be deferred to consolidated final UAT, but permission registration, configuration, and backend/frontend enforcement are Sprint requirements. If authorization requirements are unresolved, report the gap instead of implementing unrestricted access. A feature is not implementation-complete until its authorization model and enforcement are complete.

