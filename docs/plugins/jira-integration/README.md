# Jira Integration

Two-way synchronization with Jira. Supports issue type mapping, status mapping, user mapping, JQL filters, webhook handling, and sync conflict resolution.

**Status:** Active

## Features

- Bidirectional sync between NEXUS and Jira
- Issue type mapping (Jira issue types ↔ NEXUS task types)
- Status mapping (Jira statuses ↔ NEXUS statuses)
- User mapping between systems
- JQL-based filtering for selective sync
- Webhook receiver for real-time updates from Jira
- Conflict resolution strategies (NEXUS wins, Jira wins, manual)

## Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `jiraUrl` | string | — | Jira instance URL |
| `apiToken` | string | — | Jira API token or personal access token |
| `issueTypeMappings` | object | — | Mapping of Jira issue types to NEXUS task types |
| `statusMappings` | object | — | Mapping of Jira statuses to NEXUS statuses |
| `jqlFilter` | string | — | Default JQL filter for synced issues |
| `syncDirection` | enum | `bidirectional` | Direction of synchronization |

## Dependencies

- `nexus.module.integrations`
- `nexus.module.work-items`

## How to Enable

Enable the `jiraIntegration` feature flag and configure the Jira connection settings (URL, API token, field mappings) in the admin panel.
