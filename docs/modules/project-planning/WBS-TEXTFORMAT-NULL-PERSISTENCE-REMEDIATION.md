# WBS optional text formatting persistence remediation

**Date:** 2026-11-08  
**Status:** Automated WBS persistence acceptance passed; Product Owner Browser UAT remains. Broader existing test-suite limitations are recorded below.

## Root cause

PlanWbsNode.TextFormat and PlanActivity.TextFormat are optional owned values. The old DTO mapper converted an omitted format to a null owned value. EF then attempted to write null into the four required formatting flags, and SQL Server rejected the TextFormatSpecified value with error 515.

The flag is the presence discriminator for optional formatting metadata. An unspecified style is represented by IsSpecified = false, with bold, italic, and underline false and no style tokens. It does not represent visible formatting. A specified style stores IsSpecified = true.

## Schema audit

The live dbo.PlanWbsNodes table matches the current EF mapping. TextFormatSpecified, TextFormatBold, TextFormatItalic, and TextFormatUnderline are bit NOT NULL columns with no SQL defaults. Id is a uniqueidentifier primary key; PlanVersionId, SortOrder, Code, Name, tenant/audit/delete columns, and generated RowVersion match the model. Parent, activity, deliverable, and style-token references remain optional. The PlanVersion cascade FK, self-parent FK, primary/parent/version indexes, and filtered unique tenant/version/parent/code index are present. The table has no identity/computed fields, checks, or triggers that change this insert path.

PlanActivities uses the same required formatting flag mapping, so the same absent-value correction also covers activity creation/import. The already-applied migration 20261101213237_SynchronizePlanningPhysicalSchema established required formatting flags; the current model and WBS physical columns agree. No WBS migration is needed.

The wider database inspection found 16 unrelated physical-schema differences limited to IntegrationConnections, IntegrationWebhooks, and Programs. They were not changed as part of this remediation.

## Corrective change

- Added PlanTextFormat.Unspecified() as a persistence sentinel with IsSpecified = false.
- Initialized new WBS nodes and activities with the sentinel.
- Changed format mapping so absent values persist as the sentinel and unspecified values project back to API TextFormat: null.
- Normalized absent formats in MPP import, plan-version cloning, grid-history replay, and plan comparison. Clearing a style remains absent to callers and does not produce a formatting-only comparison difference.
- Kept the WBS endpoint's existing Planning.Edit authorization. The existing middleware continues to verify tenant, addressed project/plan ownership, and WBS-node/version ownership. No permission key or role grant changed.
- No React change was needed: ordinary WBS creation uses the existing endpoint and omits the optional format.

MPP import behavior for missing formatting remains unchanged. The available import fixture is covered by the passing import regression tests. No MPP export provider was found in the repository.

## Automated verification

- Project Planning module build: passed, 0 errors.
- API build: passed, 0 errors; 9 NU1900 warnings because NuGet vulnerability metadata could not be reached.
- EF model check: synchronized. Repository migration count: 15. Live Development history: 15 applied, 0 pending; pending migration names: none.
- Focused WBS persistence, import, and version-comparison regressions: 15 passed, 0 failed.
- Opt-in SQL Server persistence test: 1 passed, 0 failed against the live Development target. It created an unformatted root and two ordered children, verified all four stored flags were non-null false, saved and cleared explicit bold formatting, reloaded hierarchy/order/null projections, rejected a missing parent, and rolled back all Plan/WBS rows.
- Focused permission/catalog and out-of-scope project checks: 4 passed, 0 failed. The WBS Create action is asserted to require Planning.Edit; the middleware scope test verified out-of-scope plan creation is rejected before reaching the controller.
- Project-plan permission delegation tests: 8 passed, 0 failed, including Active Deny, dependency, and tenant-boundary cases. These test role delegation behavior, not an authenticated WBS HTTP request.

Two exploratory broader runs exposed unrelated existing suite limitations. PlanWbsServiceTests fail during SQLite EnsureCreated with SQLite Error 1: near "max": syntax error, before their service assertions; the target SQL Server test and adapted SQLite persistence tests pass. A broader planning class also had one unrelated Project Registry summary assertion fail. A broad project-routing/permission-class run hit outdated InMemory setup that does not establish tenant context under the current tenant write guard, plus a protected Admin baseline assertion expecting 47 entries while the current catalog returns 51. The affected legacy suite was not modified. The focused out-of-scope test setup now supplies its authenticated tenant context and passes.

The API test build used a temporary MSBuild target to exclude two unrelated dirty-worktree test sources that currently do not compile (LocalStorageProviderPathTraversalTests.cs and ForwardedHeadersTrustTests.cs). Neither file was modified by this remediation.

## Database completion record

| Gate | Evidence |
|---|---|
| EF model synchronized | Yes |
| Pending migrations | 0; none |
| Development identity | Shady/NexusPmo, database ID 5; verified from the live SQL connection |
| Independent backup | Verified with backup header and RESTORE VERIFYONLY; C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-WBS-515-426fcd665ea0469cbbe75a0af66097ae.bak |
| Migrations applied for this change | None |
| Seed mode/idempotency | Not run; not applicable because no reset/bootstrap was requested |
| Physical schema | WBS table matches EF; whole database still has 16 unrelated differences in the three tables listed above |
| SQL Server persistence | Passed in a transaction; test data rolled back and post-rollback assertions confirmed no test rows remain |
| Blockers | Product Owner Browser UAT; broader legacy test-suite limitations described above |

## Product Owner Browser UAT handoff

Using an authorized same-tenant planner on an active Working Plan:

1. Create a root WBS node and two child siblings, then reload and confirm parent links and sibling order.
2. Apply explicit formatting, save, clear formatting, and reload. Confirm the style appears and clears, and no SQL error is shown.
3. Import the available MPP fixture with omitted formatting and reload the WBS/activity hierarchy.
4. Repeat create with a user missing Planning.Edit and with Active Deny; expect denial and no mutation.
5. Attempt the same operation against an out-of-scope project/plan; expect rejection with no data disclosure or mutation.

Automated SQL Server persistence and focused regressions passed. The authenticated browser workflow and live request-level authorization matrix remain for Product Owner UAT.
