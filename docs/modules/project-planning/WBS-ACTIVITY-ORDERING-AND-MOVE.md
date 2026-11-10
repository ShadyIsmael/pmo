# Project Plan WBS activity ordering and move

**Date:** 2026-11-08  
**Status:** Source implementation updated; live SQL Server persistence and Product Owner browser verification remain pending.

## Root cause

Before this fix, the move service looked up a source WBS node only from `PlanActivity.WbsId`. An activity with no `WbsId` followed a separate root-only move path, which rejected a valid drop onto a WBS-backed row with the reported “not linked to a movable WBS node” validation error. Reverse-only legacy links through `PlanWbsNode.ActivityId` were also missed. Separately, the grid ordered WBS nodes and activities in different passes, placing unrepresented root activities after the WBS rows, and Insert Above/Below used `anchor.SortOrder ± 1`, which could collide with a sibling's persisted order.

The Development row for the reported activity ID was not inspected because an explicit Development connection was unavailable to the test process; its exact stored link shape remains unverified. The source-level failure path and the independent UI ordering defects are confirmed.

## Ordering contract

Task and Milestone rows use the same ordering rules. A WBS sibling group is ordered from the WBS nodes and additional activities attached to that parent. At the root, WBS root nodes and activities with no WBS association share one order. A node's primary activity is represented by the node's position; its activity-local `SortOrder` is not rewritten when the node moves.

Sibling ties use `SortOrder`, then ordinal `Code`, then `Id`. A successful structural move reindexes the destination and former sibling groups to unique, contiguous zero-based positions. The WBS adjacency list, activity type, dates, scheduling mode, dependencies, and cycle checks remain in force.

## Activity-to-WBS associations

The normal planning-grid create path creates a WBS node and then an activity whose `WbsId` points to that node. The Activity API also permits a standalone root activity with no `WbsId`. For legacy data, a node may point back to its activity through `PlanWbsNode.ActivityId` while the activity has no `WbsId`; the row remains movable through that existing association without rewriting either link.

Move resolution checks both directions. A missing, deleted, or different-version WBS target, multiple reverse links, or disagreeing links returns a validation error before changing sibling order. An unlinked root activity can be moved before or after a root WBS row. A WBS-backed activity moves its WBS node, while an additional activity attached to a node moves within that node's sibling sequence.

## Grid behavior

Insert Above and Insert Below create the new WBS/activity pair at the end of the destination sibling group, then use the move command to place it relative to the selected row. This avoids assigning the same `SortOrder` as the anchor's neighbor. Drag and drop and single-row Move Up/Down use the same persisted move command. The grid combines persisted node and activity order when it renders rows and keeps malformed or legacy rows visible for correction.

When the server reports an invalid association, the grid displays localized guidance and leaves the row visible. If the response is uncertain after a create or move request, it keeps the row and refreshes authoritative WBS and activity data before the user retries.

`Planning.View` remains required for reads and `Planning.Edit` for writes. Tenant, Project, active Working-version, row-version, and Active Deny checks remain enforced by the existing authorization and service boundaries. No permission key or role grant changed.

MPP import continues to create its existing WBS/activity associations and order. This correction changes no schema or migration, and does not change the `TextFormatSpecified` sentinel mapping.

## Verification status

- The focused React planning-grid suite passed 56/56, including mixed root/child order, insertion after a Milestone, reverse-only and loose-root rows, and localized move feedback. The React typecheck and production Vite build passed.
- The ProjectPlanning module build passed with offline NuGet vulnerability-feed warnings. The full API host build was stopped before completion because its IKVM compatibility scan continued for several minutes; the focused API test runner failed to connect to its test host after 180 seconds.
- The opt-in SQL Server regression covers root and child Task/Milestone moves, insertion after a Milestone, reload ordering and links, reverse-only associations, invalid deleted/cross-version links, cycle rejection, and transaction rollback. It was not run because the read-only Development SQL Server gate returned Access denied. The prior backup path was also inaccessible from this process.
- EF offline model check: synchronized; 16 migration files are present in the repository. Live Development DB identity, applied/pending migration history, and physical schema: not verified. No migration, seed, or database write was performed; backup verification and persistence tests: not run.
- Product Owner browser UAT remains pending. See PWBS-02 in the Master UAT Checklist.
