# Organization-Owned Role Contract

Status (2026-11-16): Role ownership and shared assignment contracts are implemented. The Development migration is applied and verified on `Shady / NexusPmo` (DB_ID 5), with 30/30 migrations and no schema gaps. The two approved Roles are NEXUS Group-owned; their assignments and grants are preserved, and five unassigned operational Roles remain unresolved. Focused login/session and role-isolation tests pass 20/20; a broader filtered suite remains red at 43/91. Successful live login and broader security/persistence regression closure remain open.

## Role identity and ownership

- A normal operational Role has one immutable `OrganizationId` within its `TenantId`.
- `Role.Id` is the assignment identity. Role names are display values and may repeat across Organizations.
- Existing canonical `RoleKey` values remain immutable and are not inferred from names.
- Protected `tenant.admin` remains one tenant-scoped role (`OrganizationId = null`).
- Protected `organization.admin` remains the existing tenant-unique System Role. Its organization authority continues to come from the existing organization-scoped grant policy; it is not cloned into unrestricted tenant roles or exposed as an ordinary assignable Role.
- A Role's Organization cannot be changed after creation. A different owner requires a new Role record and separate grants.

## Role administration API

- Managed Role summary/detail responses include `id`, `organizationId`, `organizationName`, `isOrganizationOwned`, `scope` (`Organization`, `Tenant`, or `Unresolved` for retained ambiguous legacy rows), and the existing system/protection flags.
- Role creation for an operational Role requires exactly one `organizationId`; the server verifies that Organization is active and belongs to the actor's Tenant, checks the existing protected Tenant Admin management authority, then checks `Roles.Create` and permission grants against effective permissions in that Organization. The selector's Organization options and the create endpoint use the same authority boundary.
- Role update never accepts an ownership change. A mismatch returns `ROLE_ORGANIZATION_IMMUTABLE` (HTTP 409); an unknown or cross-tenant Organization returns the existing non-disclosing not-found/forbidden response.
- Same normalized name is allowed across Organizations and rejected within one Organization. Permission IDs must resolve to active, assignable Permissions in the same Tenant and may not include protected or reserved grants.
- Role list/detail, permission editing, and deletion are constrained to Organizations the actor may administer. Tenant Admin follows its existing protected tenant-wide policy.

## User Role assignment contract

- Create/update JSON uses `roleIds: string[]` (Guid values), never Role names. On create, omitted/null or an empty array creates the account without Role assignments. On update, omitted/null preserves current Role assignments; an empty array requests removal of ordinary Role assignments, subject to protected dependency and administrator checks.
- The canonical options endpoint is `GET /api/roles/assignable`, authorized by `Users.AssignRoles`. Each option exposes `id`, `name`, `organizationId`, `organizationName`, `isOrganizationOwned`, `scope`, `roleKey`, system/protection flags, and `isAssignable`. The same Role name in two Organizations remains two choices with different IDs.
- User create/update responses expose assigned Roles as read-only objects containing `id`, `name`, `scope`, and Organization identity. `organizations` is a read-only projection derived from active Role assignments; Create/Edit must not accept `organizationIds`.
- Agent 2 must call `IRoleAssignmentAuthorizationService.ResolveAndAuthorizeAsync(roleIds, actorUserId, tenantId, targetUserId, cancellationToken)` before persisting UserRole changes. It resolves stable IDs and checks tenant, active Role and Organization, `Users.AssignRoles` in each owning Organization, delegation, protected identities, and self-escalation. Its result supplies the Role entities for the atomic assignment write.
- Client-supplied Organization IDs never override Role ownership. Membership is derived from active UserRole assignments to Organization-owned Roles. Removing one Role preserves membership while another active Role remains in that Organization.
- Before removing the last Role in an Organization, Agent 2 must calculate the removed Organization IDs from old/new Role ownership and call the shared `IUserOrganizationMembershipIntegrity` dependency guard. A blocked change returns `ROLE_ASSIGNMENT_DEPENDENCY_CONFLICT` (HTTP 409). Existing Tenant Admin last-effective-admin protection remains required; ordinary assignment cannot add or remove protected administrator identities.
- The current `UserOrganizations` table and any existing rows remain intact until all consumers and persisted data have been evaluated and an explicit reconciliation is applied. It may only serve as a verified projection during a transition, never as an authorization grant.
- Removing one Role preserves membership while another active Role remains in that Organization. Removing the last Role removes the derived membership unless a protected dependency blocks the Role change.

## Legacy data and migration behavior

- Migration `20261116093145_AddOrganizationOwnedRoles` retains existing Role IDs, permission links, UserRole assignments, audit rows, and `UserOrganizations` data. It marks ownerless ordinary Roles as `IsLegacyOrganizationOwnershipUnresolved`; it does not infer an Organization from a name or assign the Role to a default Organization.
- This is a fail-closed transition: unresolved legacy Roles remain visible for reconciliation but are not assignable and do not grant effective permissions. Their grants and assignments remain physically preserved, but users may lose effective access until an evidence-based owner decision is applied through a reviewed follow-up migration or administrator workflow.
- The migration's `Down` path refuses to drop ownership metadata after any owned or unresolved Role state exists. It is intentionally guarded against silent owner data loss and conflicts from same-name Roles in different Organizations.
- No migration has been applied yet. Keep `UserOrganizations` and its rows until consumers, membership dependencies, and existing data have been verified on the approved Development target.

## Effective permission boundary

- Tenant-scoped permissions retain existing Tenant policy behavior.
- Grants from an Organization-owned Role may only authorize operations against resources in that Role's Organization. A global, context-free permission claim must not merge grants from multiple Organizations.
- Active Deny remains authoritative. Organization membership by itself grants no permission.
- Where a request cannot establish a trusted resource Organization, Organization-scoped permission evaluation fails closed.

## Stable validation errors

| Code | Meaning |
| --- | --- |
| `ROLE_ORGANIZATION_REQUIRED` | An operational Role was created without one Organization. |
| `ROLE_ORGANIZATION_IMMUTABLE` | An update attempted to change an existing Role's Organization. |
| `ROLE_ASSIGNMENT_OUT_OF_SCOPE` | A selected Role is outside the actor's authorized scope or Tenant. |
| `ROLE_ASSIGNMENT_ID_INVALID` | Role IDs are duplicated, empty, invalid, or unavailable. |
| `ROLE_ASSIGNMENT_DEPENDENCY_CONFLICT` | Removing the last Role would break a protected Organization dependency. |
| `ROLE_PROTECTED` | A protected System Role was modified or assigned through the ordinary workflow. |

## Integration gate

User Administration remains Agent 2's ownership. Agent 1 owns the shared Role contract, role ownership model, assignment authorization service, permission boundary, migrations, and final integrated verification. The current DTOs use `RoleIds`; responses expose Role IDs and Organization identity, while Organization membership is derived from assigned Role owners. An independent review found that `CreateAsync` authorizes Role IDs before persistence without the tenant mutation guard used by `UpdateAsync`; a concurrent Role/delegation change can invalidate that authorization snapshot. Agent 2 must hold the mutation guard/transaction across create authorization, audit, and save before final integration. Agent 1 has not edited those User Administration files. Final automated testing waits for that fix and Agent 2 completion.
