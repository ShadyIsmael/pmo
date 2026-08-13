# Mobile Workspace

Mobile-optimized workspace for task management, approvals, notifications, and quick updates on the go.

**Status:** Active

## Features

- Mobile-optimized task list and detail views
- Quick task creation and status updates
- Approval workflows with swipe-to-approve gesture
- Push notifications for task assignments and updates
- Offline mode with local caching
- Touch-optimized UI

## Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `pushNotificationsEnabled` | boolean | `true` | Enable push notifications for mobile devices |
| `offlineModeEnabled` | boolean | `true` | Enable offline mode with local caching |
| `approvalActionRequired` | enum | `swipe` | Gesture required for approval actions |

## Dependencies

- `nexus.module.work-items`
- `nexus.module.notifications`
- `nexus.module.approvals`

## How to Enable

Enable the `mobileWorkspace` feature flag. The mobile workspace is accessible via the NEXUS mobile app. Configure push notification settings from the admin panel.
