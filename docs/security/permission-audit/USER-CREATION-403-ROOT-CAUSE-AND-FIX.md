# User Creation 403: Evidence Status and Source-Level Correction

**Updated:** 2026-11-14  
**Status:** Source correction implemented; historical incident root cause and current runtime recovery are **not verified**.

## Evidence boundary

The Product Owner reported that `POST /api/users` returned HTTP 403 while a Protected Tenant Admin created a user and selected Project Manager. No sanitized request capture, trace artifact, deployed assembly identity, diagnostic rejection reason, or database snapshot for that request is available in this review. The exact rejected role/permission and rejection branch therefore remain unknown. Do not treat this report as confirmation of the historical runtime root cause.

The previous version of this document incorrectly labeled a source-level explanation as the confirmed production root cause. That conclusion is withdrawn. It also contained malformed control characters in paths and identifiers; those have been removed.

## Source-level hypothesis

The relevant former decision point was `UserAdministrationService.CanAssignRolesAsync`, reached by both `CreateAsync` and role changes in `UpdateAsync`. The reported denial text is consistent with role-assignment policy rejecting an unheld permission. The former code also treated seeded operational roles as System Roles. Together these facts make the System classification/operational-possession interaction a plausible explanation, but they do not identify what happened in the reported request.

The current source now:

- Gives the protected Tenant Admin an explicit delegation path for eligible ordinary roles and operational permissions without adding those permissions to the Admin's own effective permission set.
- Preserves role and permission tenant identity, catalog activity, assignability, dependencies, resource scope, reserved boundaries, no-self-escalation, protected-role restrictions, and Active Deny behavior.
- Uses canonical RoleKey independently from `IsSystem`; ordinary seeded roles, including Project Manager, are Non-System in the current seed definition.
- Revalidates authorization within the user mutation guard and records sanitized rejection diagnostics.

These are source observations only. They do not prove the deployed database was migrated, the seed ran, or a request now succeeds.

## Required runtime proof before claiming recovery

Against the verified Development API and database, capture a sanitized test proving all of the following:

1. The authenticated actor is the protected Tenant Admin in the same Tenant.
2. The selected Project Manager role is eligible and contains at least one active operational permission absent from the Admin's effective permissions.
3. `POST /api/users` returns success and persists exactly one role membership.
4. The Admin does not gain the delegated operational permission; the new user receives only the selected role's effective permissions after dependency and deny evaluation.
5. A non-authorized actor and an out-of-scope/cross-Tenant attempt are denied.
6. Audit records identify the actor, target, role change, and outcome without exposing credentials.

Do not use browser UAT as evidence in this execution; it is reserved for the Product Owner.

## Current disposition

**Implemented in source; not runtime verified.** The historical 403 remains unresolved as an incident-level root cause until the required request evidence is captured.
