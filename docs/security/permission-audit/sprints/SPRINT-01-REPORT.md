# Sprint S1 — Effective Authorization and Resource Scope

**Date:** 2026-11-14  
**Result:** **PARTIAL — SOURCE CHANGES AND REGRESSION TESTS AUTHORED; NOT VERIFIED**  
**Baseline:** Document-Center at cdd1bddf46b851eef38fc915137d4eefe32c1bc4

## Implemented source slice

- Protected Tenant Admin Core is now 63 distinct active Tenant-scoped permissions: Administration-module or Tenant Admin baseline-flag keys, plus the explicitly approved Portfolio, Program, and Handover keys.
- Future active Tenant permissions that enter the Administration module or carry the baseline flag join the Core automatically. The helper rejects Core Action dependencies outside the active Tenant Core.
- Existing direct Deny still overrides ordinary permissions; Protected Core is restored only for the protected tenant Admin role before dependency enforcement.
- Added Identity API route metadata tests for Roles and Users administration policies, scope, and View dependencies.
- Added a two-tenant direct-override regression for attempts to read or update another tenant’s user's overrides.
- Corrected the initial Admin grant assertion found by the final independent review: Program.CreateAcrossPortfolios is excluded from the default grant set.
- Re-extracted exact action- and controller-level authorization attributes for the current 683-route census, separating them from neighboring-action evidence. 617 rows resolve to tenant-catalog keys/expressions, 8 use platform-only policies, and 58 have no direct `Policy =` value. Two newly added Request/Reversal recovery-options GET routes now have source and React-control evidence.
- Completed a focused source audit of all 58 no-direct-policy rows: service/resource scope and React consumers are recorded in the census; 45 rows have a current React consumer and 13 do not. Corrected four stakeholder invitation paths to the current `/api/stakeholder-invitations/*` routes.
- Fixed active device-token cross-owner reassignment and public workflow provision authorization. Provision now checks matching entity create/manage permission, tenant/project/resource scope, read-only state, and actor identity before insertion; regression source and RAS-15/RAS-16 cases were added.
- Further source fixes guard Department/Organization ownership, Project/Portfolio Organization selection, initial Project Manager handover authority, authenticated workflow actor identity/self-approval, Department lookup read permissions, and Project People email privacy. Positive/negative API regression sources were authored but remain unrun.
- Hardened Organization directory reads: `/api/organizations` and its detail route require `Settings.ManageTenantConfiguration`; the list projects only `{id,name}`. BusinessUnit and Department controllers also require the configuration-management policy. The Organization page gets the root name from `/api/tenant/current`; its actual unit lookup remains under `Settings.View`.
- Recorded source-verified service scope for ten census routes: six Time Entry operations, Payment Request detail, Payment Obligation project lookup, Document template-category lookup, and Project Lifecycle options.
- Applied matching actor/Tenant ManagementContext scoping to all 27 ManagerHandover routes: candidate lookups and initiation need writable Program/Portfolio scope; requests/history/snapshots/capabilities need object scope; assigned-manager reads/decisions and requester cancellation remain user-bound; View-As cannot mutate. Census service evidence is updated; focused cross-scope regression source is authored but unrun; no runtime check has run. Runtime/API behavior remains unverified.

## Open work

- The 676-route count is the historical S0 snapshot; the current route census has 683 rows and all source-level service/resource-scope and React consumer fields are mapped. The 58 no-direct-policy rows have source review; ManagerHandover React controls and later controller families were mapped in the 2026-11-14 continuation. Runtime/API verification remains open.
- The Organization/BusinessUnit/Department directory policy gap was source-remediated with existing `Settings.ManageTenantConfiguration`; no cross-Tenant exploit was confirmed. Cross-organization/project and malformed ownership-chain authorization cases remain unaudited.
- Cross-project and malformed ownership-chain authorization cases remain unaudited.
- S3 now rechecks the acting user’s permissions after acquiring the tenant mutation guard for role/user administration changes. The freshness behavior is covered by authored regressions; SQL Server lock/race behavior and last-Admin concurrency remain unverified.
- Branch creation was blocked because repository Git metadata is read-only in this environment. Changes remain uncommitted on Document-Center.

## Owner verification

Regression tests were authored but API tests/build could not run because the .NET SDK fails initialization with Service Control Manager access denied. The focused frontend permission/access-security suites passed 39/39 and the React/Vite build passed. No live API, database, or browser verification was performed. Run the focused API test checklist in SPRINT-01-API-SCOPE-TEST-CHECKLIST.md after resolving the SDK environment blocker.
