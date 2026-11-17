# RBAC Product Owner UAT Handover

**Date:** 2026-11-15  
**Purpose:** Reviewable handover only. Browser UAT remains with the Product Owner.

## Readiness

**BLOCKED - do not treat the RBAC change as deployed or UAT-ready yet.** React production build and focused UI tests pass; the API/Persistence source has isolated build evidence. However, .NET tests did not execute, Development database identity/migration state is unverified, no serving API was identified, and actual user/grant behavior is unknown. See `RBAC-IMPLEMENTATION-FINAL-REPORT.md` and `RBAC-TEST-EXECUTION-RESULTS.md` for exact boundaries.

## Required host-side database gate

From PowerShell 7 on the authorized Development Windows host, run the documented read-only command:

```powershell
Set-Location 'C:\Projects\pmo'; .\scripts\dev\db-preflight.ps1
```

Return the sanitized report showing the live `SERVERPROPERTY('ServerName')`, `DB_NAME()`, database ID, Windows Authentication, model synchronization, migration history/pending IDs, and physical schema. Do not apply or seed if identity/schema/history do not match. Review the generated migration SQL and SHA-256 before any apply through the documented gate.

## UAT journey A — Project Manager onboarding

Use a protected Tenant Admin with `Users.ManageAccounts` and `Users.AssignRoles`. Ensure the canonical Project Manager role has at least one eligible operational permission absent from Admin (the test fixture uses `Project.Edit`).

1. Open user administration and create a user.
2. Assign Project Manager from the assignable-role lookup.
3. Confirm API request returns success and correlated trace ID.
4. Confirm exactly one `UserRole` membership exists for the new user/role.
5. Confirm Project Manager is Non-System, category Management, and retains canonical identity.
6. Confirm target effective permission includes the expected role grant.
7. Confirm Admin effective permission does not gain the unheld operational permission and Admin is not added as an approver by delegation.
8. Confirm audit actor, target user, role, and trace fields.
9. Separately test initial assignment of the user as Project Manager to a Project and the later handover path; role membership alone is not project assignment.
10. Negative: actor missing `Users.AssignRoles`, cross-Tenant role, inactive/nonassignable permission, restricted Admin/Core/reserved permission, self-escalation, and invalid scope all deny with no partial persistence.

The prior `POST /api/users -> 403` is not considered resolved until a successful authenticated Dev API request and persisted evidence are recorded. Capture the previous failure's request trace if available; do not infer its cause from the new source path alone.

## UAT journey B — Organization Admin configuration

Organization Admin appointment and scoped grant setup remain denied until their governance policy is explicitly approved. Do not invent appointment or recovery authority for UAT.

1. View Organization profiles/settings for one authorized Organization; verify no sibling Organization/Tenant/Platform data appears.
2. Edit an approved Organization-overridable setting; confirm only that Organization changes and audit identifies actor/Organization/before/after.
3. Configure Organization Phase Gate and Organization Project Lifecycle catalogs; verify rows are scoped and `ReferencedProjectCount` does not disclose cross-Organization aggregates.
4. Confirm `ProjectLifecycle.OptionsLookup` and actual Project phase/status actions still require their own operational permissions.
5. Negative: Tenant configuration, Platform configuration, another Organization, Protected Tenant Admin mutation, finance approval/payment action without independent authority, and Tenant-enforced/inherit-only override all deny with no partial write.
6. Confirm Active Deny and revoked membership/grant take effect on fresh requests.
7. Separately decide whether BusinessUnit/organizational-unit surfaces are part of Organization Admin configuration before testing them; current code keeps them Tenant-only.

OA-03 grants Organization-level configuration authority only. Organization lifecycle-state transitions and Project phase/status actions remain separate operational decisions and are not granted by this configuration role.

## Decisions required before considering UAT complete

- Who may appoint, remove, protect, recover, and grant Organization Admin scope; cardinality/last-Organization-Admin policy.
- Whether Business Units and other organization-owned management data are within OA-01/02; exact by-ID/list scope checks.
- How least-privilege user management should work when an actor has `Users.ManageAccounts` but not `Users.View`.
- Which concrete active catalog key demonstrates OA-07 Tenant-enforced/inherit-only behavior for production-grade acceptance.

## Handover evidence to attach

- Sanitized Development preflight and before/after role/membership/grant counts.
- Reviewed migration SQL hash, apply-gate output, seed mode/idempotency, and post-apply schema report.
- Test command totals and failures from `RBAC-TEST-EXECUTION-RESULTS.md`.
- Authenticated API status/trace IDs and sanitized audit/persistence evidence for both positive and negative journeys.
- Product Owner decisions linked to each unresolved item above.

