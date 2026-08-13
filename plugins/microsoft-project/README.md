# Microsoft Project Integration

Import and export Microsoft Project MPP and XML files. Supports project import wizard, task and resource mapping, baseline comparison, and version history.

**Status:** Active

## Features

- Import MPP and XML project files
- Project import wizard with step-by-step mapping
- Task and resource mapping from MS Project to NEXUS fields
- Baseline comparison between imported versions
- Version history tracking for imported projects
- Export NEXUS projects to MS Project format

## Configuration

Configuration is done via the plugin settings in the admin panel or through `configurationSchema`:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `mppFilePath` | string | — | Default directory path for MPP file imports |
| `defaultTaskMapping` | object | — | Default field mappings from MS Project to NEXUS |
| `baselineComparisonEnabled` | boolean | `true` | Enable baseline comparison on import |

## Dependencies

- `nexus.module.project-management`
- `nexus.module.resource-management`

## How to Enable

Enable the `microsoftProjectImport` feature flag in your NEXUS configuration. The plugin will appear under the Integrations section in the sidebar.
