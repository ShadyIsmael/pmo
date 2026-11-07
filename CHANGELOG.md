# Changelog

All notable changes to FOPX PM are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

No unreleased changes recorded.

## [1.0.0] - 2026-10-28

### Summary

Initial product baseline for FOPX PM.

This is the first formal release of FOPX PM, establishing the baseline for the
enterprise project, program, and portfolio management platform.

### Classification

- **MAJOR.MINOR.PATCH**: 1.0.0
- **Classification**: MAJOR (initial release)
- **Status**: Release Candidate (v1.0.0-rc.1)

### Product Scope

FOPX PM is an enterprise project portfolio management platform built on a
modular monolith architecture with 19 backend modules and a React/Vite
frontend.

#### Implemented Modules

| Module | Status |
| --- | --- |
| Identity | Implemented |
| Organization | Implemented |
| Portfolio | Implemented |
| Project Management | Implemented |
| Work Items | Implemented |
| Resource Management | Implemented |
| Cost Management | Implemented |
| Risk Management | Implemented |
| Issue Management | Implemented |
| Meetings | Implemented |
| Workflow Engine | Partial |
| Approvals | Implemented |
| Notifications | Implemented |
| Documents | Implemented |
| Reporting | Implemented |
| Audit | Implemented |
| Integrations | Partial |
| Plugin Management | Implemented |
| AI Planning | Partial |
| Project Planning | Implemented |

#### Plugins (Placeholder)

| Plugin | Status |
| --- | --- |
| Jira Integration | Placeholder |
| Microsoft Project | Placeholder |
| Internal Ticketing | Placeholder |
| Test Management | Placeholder |
| Mobile Workspace | Placeholder |
| Enterprise Identity | Placeholder |
| ERP Integration | Placeholder |

### Release Gates

The following release gates remain open for v1.0.0:

- **API build**: Blocked by CS0234 in `NotificationDeliveryProcessor.cs:3`
  (`NEXUS.Modules.Identity` unresolved).
- **Live browser integration**: Not verified.
- **EN/AR visual layout**: Not verified.
- **Light/Dark visual inspection**: Not verified.
- **Production database verification**: Not performed.

Because these gates remain open, this baseline is recorded as **v1.0.0-rc.1**
(a pre-release candidate) rather than a final production release.

### Migration Notes

No migration is required. This is the initial release.

### Release Notes

See [`docs/releases/1.0.0.md`](./docs/releases/1.0.0.md) for the full
release notes.