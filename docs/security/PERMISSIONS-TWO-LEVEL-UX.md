# Roles & Permissions Two-Level UX Specification

**Status: Proposed; not implemented.** This specification assumes the PO approves the key and dependency decisions in the decision register.

## Information architecture

1. **Module group** — expandable category such as Financial Management, Project Delivery, Administration, Capacity, Reporting, or Portfolio & Program Management.
2. **Screen/workspace** — one canonical destination such as Annual Budgets, Payment Requests, Fiscal Year Closing, Reports, or Roles and Permissions.
3. **Level 1: View** — a separate permission row at the top of the screen group.
4. **Level 2: Actions** — operation permissions grouped under that screen: create, edit, submit, approve, archive, etc.

Each key appears once in the catalog tree. Platform Settings and Platform Tenant Registry use platform policies and never appear in tenant role grants.

## Role editor layout

- Role header shows role name, tenant/category, system-role badge where applicable, and a count summary: selected View grants, selected Action grants, and pending/deprecated compatibility keys.
- Search field searches English label, Arabic label, description, module, screen, and permission key. Results retain their canonical module/screen context.
- Expandable module groups show an indeterminate state if some descendant screens/actions are selected.
- Each screen has a distinct heading and short description. Its View row is visually separated from its Actions.
- Action rows use human-readable labels and concise business descriptions. The technical key is available in secondary text or a detail popover; it is not the primary label.
- Sensitive actions such as Approve/Reject, Override, Assign Roles, Direct User Permission Overrides, Settle, Reverse, Close, Carry Forward, and cross-Portfolio creation receive a consistent “Sensitive” marker, a plain-language explanation, and a separate action count.
- Search results do not clone permissions into multiple categories. A result links to its single canonical tree position.
- Deprecated compatibility keys are hidden from ordinary selection after cutover; during transition they appear in a clearly separated “Legacy compatibility” section with an expiry/decision note. They are never presented as new business capabilities.

## View and Action dependency behavior

Recommended product rule:

- Selecting any Action automatically selects its screen View in the unsaved draft and displays: “This action requires View access to this screen.”
- The selected View is an explicit grant saved with the selected Action; it is not a silent runtime implication.
- Clearing View clears dependent Actions in the draft. Before clearing, show the number and names of affected Actions and require confirmation.
- Saving through API rejects an Action grant whose required View is missing. The UI is an aid, not the enforcement boundary.
- Bulk selection at Screen level has separate controls: “Select View” and “Select all actions.” Selecting all actions also selects View and displays a confirmation listing any sensitive actions.
- Module “Select all” is an explicit bulk action, not a parent permission. It selects only currently assignable active permissions and requires confirmation of the resulting count and sensitive grants.
- Read-only/system-managed permission catalog entries that the actor cannot delegate are visible as unavailable with an explanation, not silently omitted or toggled.
- Existing persisted roles with Action but no View are flagged during migration. They are not silently rewritten because adding View may expose additional records. The owner reviews each affected role and approves a mapping.

## Assigned approver experience

Approval-only users need to open a review workspace without receiving unrelated financial write rights or broad project lists.

- Grant the relevant screen View plus the specific approval Action. Do not grant general financial edit merely to make the approval screen load.
- API list/detail paths identify the reviewer-only access mode and filter to records assigned to that user, while still checking active tenant, organization, project scope, active team membership, current workflow assignment, and separation of duties.
- An assigned reviewer opening a direct URL for an unassigned record receives the same denial as a direct API call.
- Approval history is read under the screen View and scope filter; it does not require an edit permission.
- If an existing API cannot safely filter assigned records without changing its business semantics, keep the permission proposal pending until a dedicated scoped read path is designed. Do not widen the list as a UI workaround.

## Counts, save, and reload

- Counts are derived from distinct permission keys, never from visible buttons or duplicate grouping rows.
- Show totals separately for View and Action; show legacy compatibility and proposed-but-unapproved entries separately.
- Saving displays the exact additions/removals and any dependent View selection. It does not auto-grant proposed permissions or seed roles.
- Save response is authoritative. Reload the role from the server after save and reconcile the selected key IDs; do not rely only on optimistic local state.
- If the catalog or role version changed, show a concurrency conflict and require reload/review before retry.
- Audit record captures actor, tenant, role, before/after permission key IDs, timestamp, and reason where required.

## Arabic and responsive behavior

- Full RTL layout: right-aligned hierarchy, search, labels, badges, counts, help text, and focus order.
- Arabic labels are natural translations of the operation, not generic إدارة labels where the action is known.
- Technical keys use LTR direction in a secondary field; they must not reverse the Arabic sentence or break layout.
- Desktop uses a readable tree/table with sticky screen headings. Tablet and mobile collapse to module cards and screen accordions; View and Action rows remain independently selectable and keyboard accessible.
- Use the same accessible labels and descriptions in screen readers in EN and AR. Color is not the only signal for selection, dependencies, sensitivity, or deprecated state.

## Catalog data required by the UI

Each permission record needs stable key, module ID/label, screen ID/label, level, operation label, bilingual description, sensitivity flag, lifecycle state (active/deprecated), dependencies, and optional workflow/scope note. Module/screen metadata is presentation and configuration; backend policies continue to enforce the stable key and independently resolved scope.

## Current-to-target behavior

The current RolesPage groups by raw Resource and has independent checkboxes. It should not be assumed to already implement this proposal. The target UX must preserve the backend delegation cap: an administrator may assign only permissions in their own effective grant set. The actor’s role name never substitutes for a capability or scope check.

Project lifetime ceiling changes are requested through Change Requests and take effect only after approval. Display ProjectFinancialCeiling.ApproveChange as a sensitive approval action under Change Requests; do not expose a direct Project.Edit control for changing the approved ceiling. A general ChangeRequest.Approve grant is insufficient for a request containing a BudgetChangeAmount unless the specific financial approval action is also granted.
