# Repository agent instructions

## Supported Frontend and Audit Scope

The only supported frontend is apps/web-react (React, Vite, React Router). apps/web was retired on 2026-10-27 under ADR-009. Do not recreate, build, deploy, or include the removed app in future audits. Frontend dependency reviews target apps/web-react/package.json and its lockfile. Next.js mentions in historical ADRs and reports are not active scan targets.



## EF Core and database workflow

For any change to entities, relationships, `AppDbContext`, persistence mappings, migrations, SQL Server schema, persisted models, or missing-object/schema errors:

1. Read the relevant project Markdown documentation before inspecting or changing persistence code.
2. Use the repository's documented EF Core migration and seeding workflow and the installed EF Core reference skills.
3. Identify the actual context, migration assembly, startup project, environment and database target. Never infer database identity from `localhost`, a health endpoint, or a configuration filename.
4. Use the repository's repeatable EF migration tooling. Review generated migration SQL and verify the actual target before any write.
5. Never modify Production. Database drop/reset is prohibited except for a Product Owner-authorized reset of the disposable Development/UAT database on SQL Server `Shady` named `NexusPmo`. For this one reset path, no backup is required. Before destructive work, verify `ASPNETCORE_ENVIRONMENT=Development`, the requested Development environment, the live `SERVERPROPERTY('ServerName')`, `DB_NAME()` and database ID, and that the active API Development connection resolves to the same exact target. Require explicit Product Owner authorization recorded in the active task/conversation and written confirmation of the exact database name. No other database, environment, host, Production target, migration-history reset, migration removal, or rewrite of an applied migration is permitted. Preserve data and concurrent work everywhere else.
6. Before every other database write, require an independently verified backup. Apply only reviewed pending Development migrations when explicitly authorized, then verify migration history and physical schema. The no-backup exception covers only the complete authorized reset workflow, including its immediately following required deterministic seed. A clean system bootstrap must use Minimal Seed; the separate Demo Seed must never be used to satisfy a clean-bootstrap request. The exception does not cover standalone seeding, permission reconciliation, password recovery, ordinary migration apply, or other data changes.
7. Run relevant persistence tests against the intended isolated database and report evidence truthfully.

Do not mark database work complete while required Development model/schema synchronization is pending or unverified. A clean system reset is complete only after all EF migrations and Minimal Seed have succeeded and the post-reset checks confirm exactly one active Admin user, the complete protected Admin permission baseline, and no demo/business records. Every completion report must include: model synchronized; pending migration count and names; Development DB identity verified; backup verified (`Not needed` only for the exact authorized reset workflow); migrations applied; required seed mode and idempotency verified; physical schema verified; persistence tests; and blockers. Use `Not run` or `No` when evidence is unavailable.


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

