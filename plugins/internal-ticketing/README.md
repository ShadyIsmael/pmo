# Internal Ticketing

Built-in ticketing system for organizations without Jira. Supports tickets, queues, SLAs, escalations, and custom ticket types.

**Status:** Active

## Features

- Full ticketing system with configurable ticket types
- Ticket queues for organizing work
- SLA tracking with configurable target times
- Automatic escalation on SLA breach
- Custom workflows via the workflow engine
- Assignment and reassignment of tickets
- Priority-based triage

## Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `defaultTicketType` | string | `task` | Default ticket type for new tickets |
| `slaEnabled` | boolean | `true` | Enable SLA tracking on tickets |
| `escalationEnabled` | boolean | `true` | Enable automatic escalation on breached SLAs |
| `queues` | string[] | — | List of ticket queue names |

## Dependencies

- `nexus.module.work-items`
- `nexus.module.workflow-engine`

## How to Enable

Enable the `internalTicketing` feature flag. The Tickets section will appear in the sidebar. Configure ticket types, queues, and SLAs in the plugin settings.
