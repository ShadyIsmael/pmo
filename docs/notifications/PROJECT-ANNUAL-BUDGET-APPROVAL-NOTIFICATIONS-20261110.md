# Annual Budget Approval Notifications

**Date:** 2026-11-10  
**Status:** Source implementation and focused regressions added; one API test/build attempt failed before tests ran. The missing import was corrected afterward but not recompiled under the one-attempt limit. Live database inspection and authenticated browser UAT remain outstanding.

## Finding

The Annual Budget review and revision submission paths persisted approval requests and approver assignments but did not publish an event to the existing Notifications service. No `ProjectAnnualBudgetApproval` notification type was registered, so the Notification Center had no record to load for the assigned reviewer. This was a missing producer on top of an existing notification persistence, preference, delivery, and center infrastructure gap; no duplicate notification subsystem was needed.

## Implementation

- Added `ProjectAnnualBudgetApprovalNotificationPublisher` to use the shared `INotificationService.SendIdempotentAsync` pipeline.
- Initial review and approved-budget revision submissions queue the first actionable approval step. Each review decision checks the new actionable state before committing; sequential workflows notify the newly active reviewer, while parallel workflows target each still-pending, unacted assignment.
- Before publishing, the workflow recomputes current eligible reviewers from active same-Tenant Project Team members, Project Managers, or Sponsors with effective `ProjectAnnualBudget.View` and `ProjectAnnualBudget.Approve`; Active Deny therefore excludes the reviewer. Role category does not independently exclude an otherwise eligible reviewer. The publisher resolves the actual assigned System User ID, requires an active same-Tenant account, and excludes the approval requester. A Resource profile is not required for Sponsor/Admin recipients.
- Notification IDs are deterministic from tenant, approval request, assignment, and assigned System User IDs. Existing `SendIdempotentAsync` checks for an identical persisted event and handles a concurrent unique-key insert by reloading the row, avoiding duplicate center records on retries.
- Notifications are queued through the same scoped `AppDbContext` and caller-owned approval transaction. A rollback removes the approval transition and notification intent together.
- The new type is included in the supported preference catalog. The existing service persists the canonical Notification row and applies in-app, push, and email preference settings; no channel is forced on when disabled. The payload uses bilingual title/body content and `ReferenceType=Project`. The Notifications API resolves the project internal ID to its PublicId before returning it, so project navigation uses the public route identifier.
- The current Notification Center refresh path remains in use: its existing list query polls when open and refetches on window focus. No real-time architecture was added.

## Regression coverage

Added `ProjectAnnualBudgetApprovalNotificationPublisherTests` for sequential next-step delivery, parallel fan-out, eligibility filtering, retry idempotency, System User recipients without Resource records, and preservation of disabled in-app preferences while retaining the canonical notification record.

Updated existing workflow test factories for the notification publisher dependency and updated eligible-reviewer fixtures to include both View and Approve permissions.

## Verification status

| Check | Result |
|---|---|
| Source review | Call sites added for review submission, revision submission, and review decisions; no realtime changes or schema migration required. |
| Focused API regression tests | Added and included in one filtered `dotnet test` attempt; API compilation failed first, so test bodies did not run. |
| API build | The single attempt failed with `CS0103` in `ProjectAnnualBudgetApprovalNotificationPublisher.cs`: missing import for `PaymentRequestStatuses`. The import and category-independent reviewer eligibility adjustment were made after the attempt; neither was recompiled, and no retry was run. |
| Development `Shady / NexusPmo` record inspection | SQL connection attempt failed because the execution environment denied TCP socket access. Target identity and records remain unverified. No database writes were made. |
| Notification persistence/delivery and recipient records | **Not verified against the live Development database** in this workstream. |
| Authenticated browser UAT | **Not run**; Product Owner browser checks remain pending after API build/test verification. |

