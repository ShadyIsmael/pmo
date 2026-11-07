# FOPX PM — Versioning Policy

**Document version:** 1.0.0
**Last updated:** 2026-10-28
**Status:** Approved

## 1. Purpose

This document defines how FOPX PM product versions are classified, assigned,
and released. It establishes a single, auditable source of truth for the
product version that is shared by the frontend, backend, build metadata, and
user-facing About/System Information screens.

FOPX PM follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
with explicit product-level release criteria.

## 2. Version Format

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

| Component | Description |
| --- | --- |
| MAJOR | Incremented when you make incompatible API changes. |
| MINOR | Incremented when you add functionality in a backward-compatible manner. |
| PATCH | Incremented when you make backward-compatible bug fixes. |
| PRERELEASE | A hyphen followed by a dot-separated series of identifiers. Used for pre-release verification. |
| BUILD | A plus sign followed by a dot-separated series of identifiers. Used for build metadata only. |

## 3. Release Classification

### 3.1 MAJOR — 2.0.0

Increment MAJOR when:

- Breaking changes are introduced.
- Fundamental workflow or architecture changes occur.
- Strategic product expansions are explicitly approved by the Product Owner.
- Breaking compatibility is introduced, **even if the code change is small**.

The highest-impact change determines the release classification.

### 3.2 MINOR — 1.1.0

Increment MINOR when:

- New backward-compatible capabilities are added.
- New modules are introduced.
- Meaningful enhancements are made to existing functionality.
- Major improvements are delivered to existing functionality.

### 3.3 PATCH — 1.0.1

Increment PATCH when:

- Backward-compatible bug fixes are delivered.
- Security fixes are delivered.
- Minor corrections are made without new functionality.

### 3.4 PRE-RELEASE — 1.0.0-rc.1

A pre-release build used for verification before formal release.

Use a pre-release label when:

- MVP security, verification, or release gates remain open.
- The build is not approved for production release.

**Never tag an incomplete or uncommitted working-tree state as an approved release.**

## 4. Automatic Versioning Prohibitions

Version numbers must **not** increase automatically based on:

- Elapsed time.
- Number of commits.
- Quantity of changed files.

Version classification must be **explicit and reviewable**.

## 5. Centralized Version Source

The authoritative product version is defined in:

```
version.json
```

This file is consumed by:

- React/Vite frontend.
- .NET backend.
- Build/release metadata.
- User-facing About/System Information screen.

### 5.1 What Is NOT a Product Version

Do **not** confuse the FOPX PM product version with:

- Independent dependency/package versions (React, .NET, EF Core, etc.).
- Internal module versions.
- Database migration versions.

## 6. Release Governance

### 6.1 Release Classification Decision Matrix

For every proposed release, record:

| Field | Description |
| --- | --- |
| Current version | The version being released from. |
| Proposed version | The version being released. |
| Classification | MAJOR / MINOR / PATCH / PRE-RELEASE. |
| Business justification | Why this release is being made. |
| New features | Backward-compatible new capabilities. |
| Enhancements | Improvements to existing functionality. |
| Bug / security fixes | Fixes delivered in this release. |
| Breaking changes | Breaking changes and migration requirements. |
| Affected modules | Modules impacted by this release. |
| Required tests | Tests that must pass before release. |
| Release approvals | Approvals required before release. |

### 6.2 Release Notes

Release notes must correspond to actual Git commits and must not fabricate
past releases or claim features are completed without verification.

## 7. Git Integration

### 7.1 Tagging

- Use **annotated version tags**.
- Tags are **immutable release baselines**.
- Never force-push, reset, stash, or rewrite history for a release.

### 7.2 Release Workflow

1. Prepare the working tree.
2. Update `version.json` and `CHANGELOG.md`.
3. Review the release classification decision matrix.
4. Run verification (lint, typecheck, tests).
5. Create an annotated tag: `git tag -a v1.0.0 -m "Release notes"`.
6. Do **not** push tags without explicit authorization.

### 7.3 Rollback

To roll back to a previous release:

1. Check out the desired tag: `git checkout v1.0.0`.
2. Rebuild and redeploy.
3. Never rewrite history; create a new PATCH release instead.

## 8. Verification

- Validate version format and classification logic.
- Verify frontend/backend version consistency.
- Ensure release notes correspond to actual Git commits.
- Verify EN/AR, RTL, Light/Dark for any new UI.
- Test missing build metadata gracefully.
- Ensure existing application functionality remains intact.

## 9. Documentation

- `CHANGELOG.md` — Release history.
- `docs/releases/VERSIONING-POLICY.md` — This document.
- `docs/releases/RELEASE-HISTORY.md` — Release history view.
- `docs/releases/<VERSION>.md` — Version-specific release notes.