# Universal Project Intelligence — Architecture Index

Generated: 2026-11-27T21:21:28Z
Active snapshot: `snapshots/20261127T212126-1a0d3c1/`

**Start here after reading applicable AGENTS.md rules.** All architecture links below refer to the active snapshot. The live source code is authoritative.

## Module registry

| Module | Files | Indexed tokens | Dependencies |
|---|---:|---:|---|
| [administration](snapshots/20261127T212126-1a0d3c1/modules/administration.json) | 24 | 137,947 | api, audit, cost-management, documents, identity, integrations, meetings, notifications, organization, persistence, portfolios, projects, resources, shared, unclassified, vendors |
| [ai-planning](snapshots/20261127T212126-1a0d3c1/modules/ai-planning.json) | 14 | 6,387 | api, cost-management, documents, identity, notifications, organization, persistence, project-planning, projects, resources, shared, unclassified |
| [api](snapshots/20261127T212126-1a0d3c1/modules/api.json) | 41 | 115,659 | administration, ai-planning, approvals, audit, cost-management, documents, governance, home, identity, integrations, issues, management, meetings, notifications, operations, organization, permissions, persistence, plugins, portfolios, programs, project-planning, projects, reports, resources, risks, security, shared, unclassified, work-items, workflow |
| [approvals](snapshots/20261127T212126-1a0d3c1/modules/approvals.json) | 17 | 16,588 | api, audit, cost-management, identity, management, meetings, organization, persistence, programs, projects, reports, resources, shared, unclassified |
| [audit](snapshots/20261127T212126-1a0d3c1/modules/audit.json) | 21 | 19,041 | api, cost-management, documents, identity, issues, meetings, operations, organization, persistence, project-planning, projects, resources, risks, shared, time, unclassified, work-items, workflow |
| [cost-management](snapshots/20261127T212126-1a0d3c1/modules/cost-management.json) | 133 | 433,763 | administration, api, approvals, audit, identity, meetings, notifications, organization, permissions, persistence, portfolios, programs, project-planning, projects, reports, resources, shared, unclassified, work-items, workflow |
| [documents](snapshots/20261127T212126-1a0d3c1/modules/documents.json) | 41 | 111,333 | administration, api, audit, identity, management, notifications, organization, persistence, portfolios, project-planning, projects, resources, risks, shared, unclassified |
| [governance](snapshots/20261127T212126-1a0d3c1/modules/governance.json) | 6 | 37,055 | api, audit, cost-management, home, identity, organization, persistence, portfolios, programs, projects, shared, unclassified |
| [home](snapshots/20261127T212126-1a0d3c1/modules/home.json) | 16 | 34,875 | api, cost-management, identity, meetings, operations, organization, persistence, portfolios, projects, reports, resources, risks, shared, unclassified, work-items |
| [identity](snapshots/20261127T212126-1a0d3c1/modules/identity.json) | 77 | 172,852 | administration, api, approvals, audit, cost-management, documents, home, integrations, issues, management, meetings, notifications, operations, organization, permissions, persistence, plugins, portfolios, project-planning, projects, reports, resources, risks, security, shared, unclassified, work-items |
| [integrations](snapshots/20261127T212126-1a0d3c1/modules/integrations.json) | 33 | 59,037 | administration, api, audit, identity, management, notifications, organization, persistence, projects, resources, shared, unclassified |
| [issues](snapshots/20261127T212126-1a0d3c1/modules/issues.json) | 12 | 5,203 | api, organization, projects, resources, risks, shared, unclassified, work-items |
| [management](snapshots/20261127T212126-1a0d3c1/modules/management.json) | 12 | 39,985 | api, audit, cost-management, documents, governance, home, identity, issues, meetings, notifications, operations, organization, persistence, portfolios, programs, project-planning, projects, resources, risks, shared, tests, work-items |
| [meetings](snapshots/20261127T212126-1a0d3c1/modules/meetings.json) | 61 | 118,887 | administration, api, approvals, audit, cost-management, identity, management, notifications, organization, persistence, programs, projects, reports, resources, risks, shared, unclassified, work-items |
| [notifications](snapshots/20261127T212126-1a0d3c1/modules/notifications.json) | 48 | 72,892 | administration, api, audit, cost-management, documents, identity, meetings, organization, portfolios, project-planning, projects, resources, risks, shared, unclassified |
| [operations](snapshots/20261127T212126-1a0d3c1/modules/operations.json) | 1 | 12,007 | administration, api, portfolios, projects, reports, work-items |
| [organization](snapshots/20261127T212126-1a0d3c1/modules/organization.json) | 77 | 156,742 | administration, api, audit, cost-management, documents, identity, management, operations, persistence, projects, reports, resources, risks, shared, tests, unclassified, work-items, workflow |
| [permissions](snapshots/20261127T212126-1a0d3c1/modules/permissions.json) | 11 | 41,051 | administration, api, approvals, audit, cost-management, documents, governance, identity, integrations, issues, meetings, notifications, organization, persistence, plugins, portfolios, programs, project-planning, projects, resources, risks, shared, work-items, workflow |
| [persistence](snapshots/20261127T212126-1a0d3c1/modules/persistence.json) | 197 | 1,531,805 | administration, ai-planning, api, approvals, audit, cost-management, documents, home, identity, integrations, issues, meetings, notifications, operations, organization, plugins, portfolios, project-planning, projects, reports, resources, risks, shared, time, work-items, workflow |
| [plugins](snapshots/20261127T212126-1a0d3c1/modules/plugins.json) | 20 | 8,374 | api, identity, management, notifications, operations, organization, persistence, projects, reports, resources, shared, unclassified |
| [portfolios](snapshots/20261127T212126-1a0d3c1/modules/portfolios.json) | 41 | 84,732 | api, audit, cost-management, home, identity, issues, management, meetings, notifications, organization, persistence, programs, projects, resources, risks, shared, unclassified, work-items |
| [programs](snapshots/20261127T212126-1a0d3c1/modules/programs.json) | 15 | 61,803 | administration, api, approvals, cost-management, identity, issues, management, meetings, operations, organization, permissions, persistence, portfolios, projects, reports, resources, risks, shared, tests, unclassified, work-items |
| [project-planning](snapshots/20261127T212126-1a0d3c1/modules/project-planning.json) | 124 | 271,349 | administration, api, audit, cost-management, documents, identity, notifications, operations, organization, persistence, projects, reports, resources, risks, shared, tests, unclassified, work-items, workflow |
| [projects](snapshots/20261127T212126-1a0d3c1/modules/projects.json) | 326 | 1,317,019 | administration, api, approvals, audit, cost-management, documents, home, identity, issues, management, meetings, notifications, operations, organization, permissions, persistence, portfolios, programs, project-planning, reports, resources, risks, shared, tests, time, unclassified, work-items, workflow |
| [reports](snapshots/20261127T212126-1a0d3c1/modules/reports.json) | 22 | 22,576 | api, approvals, audit, cost-management, home, identity, issues, meetings, operations, organization, persistence, portfolios, programs, projects, resources, shared, unclassified, work-items |
| [resources](snapshots/20261127T212126-1a0d3c1/modules/resources.json) | 105 | 239,199 | administration, api, approvals, audit, cost-management, home, identity, organization, persistence, project-planning, projects, shared, tests, unclassified, vendors, work-items |
| [risks](snapshots/20261127T212126-1a0d3c1/modules/risks.json) | 35 | 77,483 | api, audit, cost-management, identity, issues, meetings, notifications, organization, persistence, portfolios, project-planning, projects, resources, shared, unclassified, work-items, workflow |
| [security](snapshots/20261127T212126-1a0d3c1/modules/security.json) | 10 | 39,209 | administration, api, approvals, audit, cost-management, documents, identity, organization, persistence, portfolios, project-planning, projects, shared, unclassified |
| [shared](snapshots/20261127T212126-1a0d3c1/modules/shared.json) | 148 | 423,390 | administration, ai-planning, api, approvals, audit, cost-management, documents, home, identity, integrations, issues, management, meetings, notifications, operations, organization, plugins, portfolios, programs, project-planning, projects, reports, resources, risks, stakeholder, time, unclassified, vendors, work-items, workflow |
| [stakeholder](snapshots/20261127T212126-1a0d3c1/modules/stakeholder.json) | 2 | 9,784 | projects, resources, shared, unclassified |
| [tests](snapshots/20261127T212126-1a0d3c1/modules/tests.json) | 60 | 191,232 | administration, ai-planning, api, approvals, audit, cost-management, documents, governance, home, identity, integrations, issues, management, meetings, notifications, organization, permissions, persistence, plugins, portfolios, programs, project-planning, projects, reports, resources, risks, security, shared, unclassified, work-items, workflow |
| [time](snapshots/20261127T212126-1a0d3c1/modules/time.json) | 5 | 7,491 | audit, identity, management, notifications, organization, persistence, projects, resources, shared |
| [unclassified](snapshots/20261127T212126-1a0d3c1/modules/unclassified.json) | 75 | 116,347 | administration, ai-planning, api, approvals, audit, cost-management, documents, identity, integrations, issues, management, meetings, notifications, organization, permissions, plugins, portfolios, project-planning, projects, reports, resources, risks, shared, work-items, workflow |
| [vendors](snapshots/20261127T212126-1a0d3c1/modules/vendors.json) | 4 | 14,950 | administration, approvals, audit, cost-management, documents, organization, persistence, projects, resources, shared, tests |
| [work-items](snapshots/20261127T212126-1a0d3c1/modules/work-items.json) | 46 | 106,471 | administration, api, audit, cost-management, documents, home, identity, issues, management, organization, portfolios, projects, resources, shared, tests, unclassified, workflow |
| [workflow](snapshots/20261127T212126-1a0d3c1/modules/workflow.json) | 54 | 121,108 | api, audit, cost-management, documents, identity, management, notifications, organization, persistence, project-planning, projects, resources, risks, shared, unclassified, work-items |

## Maps and lookup

- [System architecture](snapshots/20261127T212126-1a0d3c1/diagrams/system.mmd)
- [Files index](snapshots/20261127T212126-1a0d3c1/indexes/files.jsonl)
- [Symbols index](snapshots/20261127T212126-1a0d3c1/indexes/symbols.jsonl)
- [Dependency graph](snapshots/20261127T212126-1a0d3c1/indexes/dependencies.jsonl)
- [Token index](snapshots/20261127T212126-1a0d3c1/indexes/tokens.jsonl)

## Task-first workflow

```powershell
python tools/upi/upi.py route --root . --task "describe the task" --budget 16000
```

The router checks index freshness and refreshes changed files before planning. Reading other modules requires a documented dependency or task requirement.

## Integrity

- Indexed files: 1934
- Indexed symbols (heuristic): 15193
- Dependency edges (verified or inferred): 77086
- Changed files parsed this run: 1
- Token counts marked `estimated:chars/3.5` are approximations if tiktoken is unavailable.
- Optional watcher: `python tools/upi/upi.py watch --root . --interval 5` (only runs while this command is active).
