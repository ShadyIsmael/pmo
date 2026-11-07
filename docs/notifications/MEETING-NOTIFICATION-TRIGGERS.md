# Meeting Notification Triggers

**Date:** 2026-11-06  
**Status:** Source implemented; pending migration application and runtime verification.  
**Infrastructure:** Existing `NotificationService`, `Notification`/`NotificationDelivery` persistence, notification preferences, dispatch processors, and Notification Center. No parallel notification subsystem or new permission keys were introduced.

## Trigger and recipient matrix

| ID / type | Trigger | Recipient eligibility | Stable event identity | Link |
|---|---|---|---|---|
| MN-01 `MeetingInvitation` | Newly persisted System User invitation on meeting creation or attendee addition. Unchanged attendees are not re-emitted by edits. Each recurrence occurrence has its own Meeting/participant row. | The newly invited active same-tenant user only; excludes the actor and inactive, removed, declined, or external-only invitees. | `MeetingParticipant.Id` plus tenant, event type, and recipient in the deterministic notification ID. A removed invitee re-added through the existing workflow receives a new participant row/event identity. | Meeting Details via Meeting PublicId. Body includes title, scheduled UTC date/time, and organizer. |
| MN-02 `MeetingReminder` | Organizer-selected lead time before a Scheduled meeting. `ReminderDueAt = Meeting.StartTimeUtc - ReminderOffsetMinutes`. | Current active invited System Users; removed, inactive, declined, and unsupported response-state invitees are excluded. The attendee list and eligibility are re-read at dispatch. | Persisted reminder dispatch is unique per Meeting occurrence and scheduled instant; notification identity also includes event type and recipient. | Meeting Details via Meeting PublicId. |
| MN-03 `MeetingStarted` | Successful explicit Start Meeting operation, after `InProgress` and `ActualStartTime` are persisted. Scheduled time alone does not trigger it. | Active invited System Users, excluding the actor who started the Meeting. | `started:{Meeting.Id}` plus tenant, event type, and recipient in the deterministic notification ID. | Active Meeting Details via Meeting PublicId. |
| MN-04 `MeetingMinutesPublished` | A completed Meeting's Minutes are persisted as `Final`. Draft initialization, draft saves, completion alone, and failed publication do not emit this event. | Active invited attendees who currently have effective `Meeting.View` and valid project management scope. Excludes the publishing actor. | `minutes-published:{MeetingMinutes.Id}` plus tenant, event type, and recipient. | `/meetings/{MeetingPublicId}?tab=minutes`. |
| MN-05 `MeetingMinutesComment` | New top-level comment persisted on published Final Minutes. | Every active invited attendee currently authorized to view the published Minutes; excludes the comment author. | `comment:{MeetingMinutesComment.Id}` plus tenant, event type, and recipient. | `/meetings/{MeetingPublicId}?tab=minutes&comment={CommentPublicId}`. |
| MN-06 `MeetingMinutesCommentReply` | Reply persisted under an existing comment on published Final Minutes. | Every active invited attendee currently authorized to view the published Minutes, including the original comment author when eligible; excludes the reply author. | `comment:{Reply.Id}` plus tenant, event type, and recipient. | `/meetings/{MeetingPublicId}?tab=minutes&reply={ReplyPublicId}`. |

The notification payload for comment and reply events contains the Meeting title and a generic event description; it does not include comment text. Recipient eligibility is checked at event time against the current invitation state, active System User account, effective View permission, tenant, and applicable project scope. Following every link reloads the Meeting through the normal authorization path; a notification does not grant access.

## Reminder scheduling and recurrence

The existing Meeting Create/Edit experience exposes No reminder, 15 minutes, 30 minutes, 1 hour, and 1 day. The selected value is saved with the Meeting. Stored Meeting time is treated as an absolute UTC instant; reminder due time is derived from that start instant, independent of organization working hours.

The existing background scheduler polls at one-minute intervals and reconciles persisted `MeetingReminderDispatch` rows per Meeting occurrence. It deactivates stale pending dispatches when start time or lead time changes, the Meeting is cancelled or starts, and creates the revised pending dispatch when appropriate. At delivery it rechecks current Meeting status, due instant, invitations, and active same-tenant users inside the dispatch transaction. Declines, removal, cancellation, or an already-started Meeting suppress delivery. Recurring occurrences are separate Meeting records and therefore have separate schedules and idempotency identities.

## Transactionality, retries, and idempotency

Meeting events are queued through `NotificationService.SendIdempotentAsync` on the caller's shared `AppDbContext`; the publisher does not open or commit a separate transaction. A notification intent is saved in the same transaction as the invitation, Start, publication, comment, or reply business change, so a rollback cannot leave a delivered business notification. The shared delivery workers retain pending deliveries and retry them under existing policy.

Notification IDs are deterministic from tenant, type, stable event/occurrence key, and recipient. The notification service verifies an existing row matches the retried event. Reminder dispatch rows also use the existing unique Meeting/`ScheduledAtUtc` key and row-version concurrency. Recipient collections are deduplicated by System User ID. Repeated calls, worker retries, and repeated lifecycle requests do not create duplicate notification rows.

## Authorization, localization, and Notification Center

- Sending-operation permission and recipient eligibility are separate checks. Existing Meeting operation authorization remains in the Meeting service.
- Published Minutes, comments, and replies fan out only to currently invited, active, authorized attendee accounts. Effective permission checks and project management scope are applied; no role-name check or Admin bypass is used.
- Invitation replies remain separate from actual attendance. Reminder/start recipients are selected from active invitations, not from attendance state.
- Six event types carry English and Arabic titles and bodies. The existing Notification Center selects the requested locale and preserves RTL layout.
- Links use public Meeting and comment/reply IDs. Invitation, reminder, and start links open the Meeting; publication opens Minutes; comment/reply links target the specific public comment or reply.
- Read/unread state, counters, preferences, and delivery retry remain in existing Notification infrastructure.

## Persistence and migration status

The existing `MeetingReminderDispatches` persistence supports the scheduler. A generated migration named `20261105214717_AddMeetingNotificationPublicIdentifiers` adds `Meetings.PublicId`, `MeetingMinutesComments.PublicId`, and nullable `Notifications.ReferencePublicId`, with unique indexes for the Meeting and comment public IDs. Its idempotent SQL artifact is generated and was reviewed at source level. No migration was applied, and no database reset, reseed, direct SQL write, or schema patch was performed. The offline EF model check passed; target Development migration history and physical schema were not inspected as part of this notification implementation.

Classify the feature as **Source Implemented — Pending Migration / Runtime Verification** until the authorized deployment process applies the migration and verifies the target schema. Do not report the notification triggers as deployed to the Development database.

## Changed source and test areas

- `apps/api/src/NEXUS.Modules.Meetings/Application/MeetingService.cs` - lifecycle, invitation, publication, and comment event hooks.
- `apps/api/src/NEXUS.Persistence/MeetingNotificationPublisher.cs` and `MeetingReminderScheduler.cs` - recipient validation, durable event identity, reminder reconciliation, and persisted-start checks.
- `apps/api/src/NEXUS.Modules.Notifications/Domain/NotificationTypes.cs` and `apps/api/src/NEXUS.Api/Program.cs` - canonical event types and existing scheduler registration.
- `apps/web-react/src/features/notifications/NotificationsPage.tsx`, `src/lib/notification-navigation.ts`, and `src/lib/notification-content.ts` - localized content and public detail/Minutes/comment/reply links.
- `apps/api/tests/NEXUS.Api.Tests/Integration/MeetingNotificationPublisherTests.cs`, `MeetingReminderSchedulerTests.cs`, and `MeetingNotificationLifecycleAndRescheduleTests.cs` - publisher authorization/idempotency and lifecycle/scheduling regressions.
- `apps/web-react/src/test/notification-content.test.ts`, `notification-navigation.test.ts`, and notification center/realtime tests - locale, navigation, and read/unread behavior.

## Automated verification

| Area | Evidence |
|---|---|
| Earlier consolidated React focused tests | Passed: 8 files, 41 tests in the earlier recorded run. This is historical evidence; the latest focused Vitest execution was not run because automatic review blocked telemetry egress. |
| React typecheck | `npm run typecheck` passed. |
| Focused React lint | ESLint passed for the modified resolver, Notification Center page, and tests. |
| React production build | Latest npm run build passed; existing chunk/import warnings remain. |
| Invitation/start/reminder backend regressions | Added focused lifecycle and rescheduling tests for create/add, unrelated edit, explicit start, repeat start, start/lead-time reschedule, cancellation, and decline. Not executed. |
| Publisher authorization/idempotency regressions | Existing focused publisher tests cover active invitees, effective `Meeting.View`, project scope, draft suppression, event deduplication, safe comment payloads, and caller transaction behavior. Additional tests cover unsupported invitation statuses and persisted actual-start time. Not executed. |
| Minutes service notification hooks | Added focused service-level tests that finalizing a Draft queues the publication hook once, repeated finalization does not resend, and top-level comments/replies on Final Minutes queue the correct persisted comment hierarchy and actor. Not executed. |
| .NET verification | API build passed with 0 errors and 356 warnings; API test project compiled with 0 errors and 370 warnings. Focused tests then failed to connect to testhost at 90 seconds; one test-only retry failed at 180 seconds before any test started. No backend test is reported as passed. |
| Database and browser | Migration not applied; runtime verification and browser UAT not run. Product Owner performs browser UAT. |

A separate Vitest attempt was auto-review blocked because the npm process triggered telemetry egress to an untrusted external host; it was not retried through a workaround and produced no new result. The earlier recorded 8-file/41-test pass remains historical evidence only.

## Remaining limits

The current source, model snapshot, and generated migration are available, but backend tests could not execute in this environment and the Development physical schema was not verified. Runtime correctness, migration deployment, push/email provider behavior, and full multi-instance delivery must be confirmed through the authorized deployment and Product Owner's combined UAT.

## Development execution recheck — 2026-11-06

The Minutes page now uses the public Meeting ID only for the route, resolves Meeting Details, then passes the internal Meeting ID to Minutes APIs that retain that contract. Missing Minutes Draft remains distinct from a missing or inaccessible Meeting. Notification links stay on public Meeting/comment identifiers. The API and React production builds passed, and the API test project compiled. Focused API tests did not start because VSTest could not connect to testhost. The latest Vitest run was not executed after the automatic telemetry review block. The reported /api/meetings HTTP 500 could not be reproduced or ruled out without a live API/Development database connection. No database identity, backup, migration history, physical schema, or runtime notification delivery was verified; no migration or other database write was made.

## Development migration recheck — 2026-11-06

The live Development target is now verified as `Shady/NexusPmo` (database ID 5). `20261105214717_AddMeetingNotificationPublicIdentifiers` is pending as migration 13 of 13; live history ends at `20261102190318_AddStableSystemRoleIdentity`. The target-specific forward SQL adds public IDs to Meetings and Meeting Minutes comments, a nullable Notification reference public ID, and unique indexes; no migration was applied. The host has no independent backup volume or mapped share, and 16 additional current-model physical differences remain on Integration and Program tables. See the [current Development execution record](../development/ONE-PASS-DEV-EXECUTION-20261106.md). The feature remains source implemented and pending migration/runtime verification.
