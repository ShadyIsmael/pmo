# Repository agent instructions

## Supported Frontend and Audit Scope

The only supported frontend is apps/web-react (React, Vite, React Router). apps/web was retired on 2026-10-27 under ADR-009. Do not recreate, build, deploy, or include the removed app in future audits. Frontend dependency reviews target apps/web-react/package.json and its lockfile. Next.js mentions in historical ADRs and reports are not active scan targets.



## EF Core and database workflow

For any change to entities, relationships, `AppDbContext`, persistence mappings, migrations, SQL Server schema, persisted models, or missing-object/schema errors:

1. Read the relevant project Markdown documentation before inspecting or changing persistence code.
2. Use the repository's documented EF Core migration and seeding workflow and the installed EF Core reference skills.
3. Identify the actual context, migration assembly, startup project, environment and database target. Never infer database identity from `localhost`, a health endpoint, or a configuration filename.
4. Use the repository's repeatable EF migration tooling. Review generated migration SQL and verify the actual target before any write.
5. Never modify Production. Development and UAT database operations do not require a backup before migrations or other authorized changes. Do not require independent backup storage or `RESTORE VERIFYONLY`, and do not block an operation solely because a backup is absent. Create an optional backup only when explicitly requested. These rules do not relax target/environment identity, migration review, history, model, or physical-schema checks. Production remains outside the permitted write scope and retains its mandatory backup and recovery protections for any separately approved workflow.
6. Destructive operations (including drop, reset, delete, recreate, truncate, or other intentional data loss) still require explicit Product Owner authorization recorded in the active task/conversation and written confirmation of the exact database name. The only permitted reset path remains the repository's narrowly scoped reset of the disposable Development database on SQL Server `Shady` named `NexusPmo`; it must verify the requested Development environment, live `SERVERPROPERTY('ServerName')`, `DB_NAME()` and database ID, and that the active API Development connection resolves to the same exact target before destructive work. Do not infer reset authority from the optional-backup policy. No other database, environment, host, Production target, migration-history reset, migration removal, or rewrite of an applied migration is permitted. Preserve data and concurrent work everywhere else.
7. Apply only reviewed pending Development migrations when explicitly authorized, then verify migration history and physical schema. A clean system reset is complete only after all EF migrations and Minimal Seed have succeeded and the post-reset checks confirm exactly one active Admin user, the complete protected Admin permission baseline, and no demo/business records. The separate Demo Seed must never be used to satisfy a clean-bootstrap request.
8. Run relevant persistence tests against the intended isolated database and report evidence truthfully.

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


<!-- UPI-RULES-START -->
## UPI architecture-first task routing

Follow all existing repository instructions first. For coding, debugging, refactoring, and architecture tasks, read `docs/architecture/INDEX.md` before searching application source. If it does not exist, initialize it with `tools/upi/upi.ps1 init --root .` on Windows or `python tools/upi/upi.py init --root .` elsewhere. Then route the actual task with `tools/upi/upi.ps1 route --root . --task "<task>" --budget 16000` on Windows or the matching Python command elsewhere. Start with the selected files and symbol ranges in the generated task manifest; expand only when evidence or task requirements justify it. Current source is authoritative. After application-code changes, run `refresh` and `validate`. Indexing may read project source but writes only under `docs/architecture` and must never access or modify a database.

When the user's request is in Arabic or another non-English language, route a concise English equivalent that retains its feature and action terms so they match English source paths and symbols.
<!-- UPI-RULES-END -->
