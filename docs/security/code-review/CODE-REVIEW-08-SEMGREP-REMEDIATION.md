# P0-08 — Semgrep Security Hardening and Remediation

**Date:** 2026-11-08  
**Repository:** `ShadyIsmael/pmo` (`C:\Projects\pmo`)  
**Status:** **Implementation integrated; tests, builds, and fresh scans intentionally not run. No full security verification claim is made.**

## 1. Scope, evidence, and count reconciliation

The supplied source exports were inspected from `C:\Users\shady\Downloads`:

- `Semgrep_Code_Findings_2026_11_07.csv`: 40 rows.
- `Semgrep_Code_Combined_Findings_2026_11_07.csv`: 3 rows.
- `Semgrep_Supply_Chain_Findings_2026_11_07 (1).csv`: 12 rows.
- `Semgrep_Supply_Chain_Findings_2026_11_07.csv`: 6 rows.

Together these files contain **61 rows and 61 unique IDs**: 43 Code/combined-Code and 18 Supply Chain. This does not match the task summary of 55 (40 Code + 15 Supply Chain); the exports are not silently truncated or conflated to force that count. The matrices below reconcile all IDs found in the four supplied CSVs. The 40-row Code export and 12-row POC Supply Chain export refer to source commit `2f407fc6a4eceb8b89f2f2998154b670f1a49520`; the 3-row combined Code export and 6-row retired-app Supply Chain export refer to `a698e861fa4066e3405eef48ccde6a1b6294b97c`. Neither source snapshot is a fresh scan of the current worktree. Platform rows remain Open; no Semgrep platform status was changed.

The current worktree already contained unrelated, uncommitted P0 permission/planning/security changes. They were preserved and are not attributed to this remediation. No database reset, data write, migration, seed, Production action, browser UAT, or Next.js restoration occurred.

## 2. Finding-by-finding reconciliation

**Status terms:** FIXED means an implementation change or verified prior source change is present, but not necessarily runtime-tested. VERIFIED FALSE POSITIVE means the flagged source pattern has a source-evidenced safe explanation. VERIFIED NOT APPLICABLE means the finding targets a retired component or behavior incompatible with an essential stateful service. BLOCKED means required evidence or a safe resolution is unavailable.

### Code and combined Code findings (43 IDs)

| ID | Exported file:line | Disposition | Evidence / change |
|---|---|---|---|
| 1066434842 | `.agents/skills/codeql/scripts/test_shell_blocks.py:702` | VERIFIED FALSE POSITIVE | `compile(body, ..., "exec")` parses embedded Python but does not execute it. The test only checks syntax and reports `SyntaxError`. |
| 1066434841 | `apps/api/tools/NEXUS.EFDatabaseGate/Program.cs:1224` | VERIFIED FALSE POSITIVE | `ReadCatalogSetAsync` executes the supplied SQL string, but callers use fixed catalog-query literals. Other dynamic identifiers are quoted and values are parameterized. No attacker-controlled SQL source was found. Gate authorization/reset policy was unchanged. |
| 1066434840 | `.github/workflows/codeql.yml:25` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434839 | `.github/workflows/codeql.yml:28` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434838 | `.github/workflows/codeql.yml:36` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434837 | `.github/workflows/codeql.yml:44` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434836 | `.github/workflows/codeql.yml:55` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434835 | `.github/workflows/codeql.yml:71` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434834 | `.github/workflows/codeql.yml:74` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434833 | `.github/workflows/codeql.yml:81` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434832 | `.github/workflows/codeql.yml:92` | FIXED | GitHub Action references are pinned to immutable commit SHAs; see Section 4. |
| 1066434831 | `.github/workflows/pr-validation.yml:18` | FIXED | Checkout is pinned to a verified immutable SHA. |
| 1066434830 | `.github/workflows/pr-validation.yml:28` | FIXED | Setup Node is pinned to a verified immutable SHA. |
| 1066434829 | `.github/workflows/pr-validation.yml:31` | FIXED | Checkout is pinned to a verified immutable SHA. |
| 1066434828 | `.github/workflows/pr-validation.yml:71` | FIXED | Checkout is pinned to a verified immutable SHA. |
| 1066434827 | `.github/workflows/pr-validation.yml:74` | FIXED | Setup .NET is pinned to a verified immutable SHA. |
| 1066434826 | `.github/dependabot.yml:3` | FIXED | Seven-day default cooldown added for the root npm update group. |
| 1066434825 | `.github/dependabot.yml:9` | FIXED | Seven-day default cooldown added for the React app npm update group. |
| 1066434824 | `.github/dependabot.yml:15` | FIXED | Seven-day default cooldown added for NuGet update groups. |
| 1066434823 | `.github/dependabot.yml:23` | FIXED | Seven-day default cooldown added for GitHub Actions updates. |
| 1066434822 | `.agents/skills/security-audit/validate-findings.cjs:321` | FIXED | Untrusted schema regexes are no longer compiled; only the fixed fingerprint pattern is accepted. |
| 1066434821 | `.agents/skills/security-audit/validate-findings.cjs:321` | FIXED | Same fixed-pattern allowlist as ID 1066434822; rejected patterns are reported as schema errors. |
| 1066434820 | `.agents/skills/security-audit/validate-findings.cjs:321` | FIXED | Same fixed-pattern allowlist as ID 1066434822; pathological patterns are covered by an added regression test. |
| 1066434819 | `.agents/skills/security-audit/validate-findings.cjs:451` | FIXED | Validation now tests a predeclared literal fingerprint regex rather than constructing one from schema input. |
| 1066434818 | `.agents/skills/security-audit/validate-findings.cjs:451` | FIXED | Same fixed literal-regex validation as ID 1066434819. |
| 1066434817 | `.agents/skills/security-audit/validate-findings.cjs:451` | FIXED | Same fixed literal-regex validation as ID 1066434819. |
| 1066434816 | `.agents/skills/security-audit/validate-findings.cjs:451` | FIXED | Same fixed literal-regex validation as ID 1066434819. |
| 1066434815 | `apps/web-react/src/lib/export/project-status-report-export.ts:57` | VERIFIED FALSE POSITIVE | The flagged loop traverses trusted localization catalogs to read a label; it does not assign attacker-supplied keys or mutate prototypes. The separately flagged configuration selection now stores API-provided plan IDs in a `Map`, with regression coverage for `__proto__` and `constructor`. |
| 1066434814 | `.agents/skills/codeql/scripts/test_build_mode_claims.py:48` | FIXED | Escapes interpolated language values with `re.escape` before building the test regex. |
| 1066434813 | `.agents/skills/codeql/scripts/test_script_flags.py:160` | VERIFIED FALSE POSITIVE | The scanner builds its combined script-name pattern using `re.escape` for every accepted script name; it does not interpolate raw names as regex syntax. |
| 1066434812 | `.agents/skills/codeql/scripts/test_shell_blocks.py:476` | FIXED | Escapes interpolated array names with `re.escape`. |
| 1066434811 | `.agents/skills/codeql/scripts/test_shell_blocks.py:489` | FIXED | Escapes interpolated array names with `re.escape`. |
| 1062160470 | `apps/web-react/Dockerfile:32` | FIXED | Nginx runs as the `nginx` user; its PID and cache paths remain writable under `/var/cache/nginx`. |
| 1062160469 | `apps/api/Dockerfile:18` | FIXED | API runs as `$APP_UID`; the worker image uses the same non-root identity. Required document-storage and Development isolated Data Protection paths are created and assigned to that UID. |
| 1062160465 | `apps/web-react/src/components/ui/rich-text-editor.tsx:134` | VERIFIED FALSE POSITIVE | The HTML sink receives only `sanitizeRichText(value)`, which parses and reconstructs allowed tags, drops dangerous elements/attributes, and escapes text. Added adversarial link/iframe/event-attribute regression coverage; the sink remains intentionally guarded. |
| 1062160463 | `docker-compose.yml:6` | FIXED | `no-new-privileges:true` added to SQL Server. |
| 1062160462 | `docker-compose.yml:26` | FIXED | `no-new-privileges:true` added to Redis. |
| 1062160461 | `docker-compose.yml:6` | VERIFIED NOT APPLICABLE | SQL Server requires writes to its named `/var/opt/mssql` data volume. A read-only root filesystem was not imposed; no `read_only` scanner suppression was added. |
| 1062160460 | `docker-compose.yml:26` | VERIFIED NOT APPLICABLE | Redis requires writes to its named `/data` volume. A read-only root filesystem was not imposed; no `read_only` scanner suppression was added. |
| 1062160459 | `apps/api/src/NEXUS.Modules.Documents/Infrastructure/LocalStorageProvider.cs:24` | FIXED | Storage containment now compares the normalized root prefix ordinally instead of case-insensitively, preventing a case-variant sibling-prefix bypass. Focused traversal tests were added. |
| 1062160468 | `apps/web-react/Dockerfile:1` | FIXED | Both React Docker build Node stages use `node:22.23.4-alpine`, the fixed release recorded in P0-07C. |
| 1062160467 | `apps/web-react/Dockerfile:6` | FIXED | Same fixed Node baseline as ID 1062160468. |
| 1062160466 | `apps/web/Dockerfile:1` | VERIFIED NOT APPLICABLE | `apps/web` is the retired frontend and remains absent. It was not restored or included as an active scan target. |

### Supply Chain findings (18 IDs)

| ID | Exported package/version and file:line | Disposition | Evidence / change |
|---|---|---|---|
| 1062160599 | `tinypool@1.1.1`, `apps/web/package-lock.json:11465` | VERIFIED NOT APPLICABLE | The path belongs to retired `apps/web`, absent from the repository. No Next.js app was restored. |
| 1062160598 | `tinypool@1.1.1`, `apps/web-react/package-lock.json:8162` | FIXED | The active lock was updated during P0-07B; the P0-07C source reconciliation reports this old entry absent after the Vitest update. A new audit was not run. |
| 1062160568 | `zod@3.25.76`, `apps/web/.../administration/users/page.tsx:64` | VERIFIED NOT APPLICABLE | This source belongs to retired `apps/web`, absent from the repository. |
| 1062160567 | `zod@3.25.76`, `apps/web/src/components/meetings/MeetingFormSheet.tsx:68` | VERIFIED NOT APPLICABLE | This source belongs to retired `apps/web`, absent from the repository. |
| 1062160565 | `next@14.2.35`, `apps/web/package-lock.json:9024` | VERIFIED NOT APPLICABLE | Retired Next.js dependency tree; the app remains removed. |
| 1062160564 | `next@14.2.35`, `apps/web/package-lock.json:9024` | VERIFIED NOT APPLICABLE | Retired Next.js dependency tree; the app remains removed. |
| 1062160554 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | The obsolete POC and lockfile were removed; no supported React/API consumer depended on it. |
| 1062160544 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160541 | `esbuild@0.21.5`, `poc-vite/package-lock.json:1658` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160535 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160522 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160520 | `react-router@6.30.6`, `poc-vite/package-lock.json:2348` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160514 | `react-router@6.30.6`, `poc-vite/package-lock.json:2348` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160499 | `postcss-selector-parser@6.1.4`, `poc-vite/package-lock.json:2271` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160495 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160491 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160487 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |
| 1062160471 | `vite@5.4.21`, `poc-vite/package-lock.json:2759` | FIXED | Same POC and dependency-tree removal as ID 1062160554. |

## 3. Implemented changes

- Removed the unused `poc-vite/` application, its package manifest/lockfile, source, configuration, and the obsolete benchmark report/catalog entry. No supported React/API or CI functionality depended on it.
- Retained the React Router v7.18.5, Vite 6.4.5, Vitest 4.1.12, Zod 4.6.7 and Node 22.23.4 fixes recorded in P0-07B/07C. The report does not represent their old CSV links as a fresh audit.
- Tightened local storage root containment and added API path-traversal regression tests.
- Kept the guarded rich-text sink and added malicious-markup regression coverage.
- Changed project reporting configuration plan selections from a plain object to `Map` to make prototype-like API IDs ordinary keys; added focused regression coverage. The separate localization-catalog lookup flagged by Semgrep is read-only and did not demonstrate prototype mutation.
- Switched API and worker containers to `$APP_UID`; provisioned the needed document storage and Development isolated Data Protection directories. Switched Nginx to its non-root user and a writable cache/PID path.
- Added `no-new-privileges` to stateful and application Compose services, dropped unnecessary capabilities from application containers, and retained only `NET_BIND_SERVICE` for Nginx. SQL Server and Redis named data volumes remain writable; published ports were preserved.
- Pinned GitHub Actions to immutable SHAs; added a check against the base revision’s pin-check script, with full history and fail-closed shell behavior.
- Added seven-day Dependabot default cooldowns for npm, NuGet, and GitHub Actions.
- Removed dynamic `eval` from the Semgrep scan runner’s array-loading helper using an explicit destination allowlist.
- Constrained schema regex validation to a fixed literal fingerprint pattern and escaped interpolated regex fragments in CodeQL skill test scripts.
- Tightened the CodeQL SARIF gate to block severity >= 4.0 and fail closed on missing/malformed/unscored SARIF. Added dedicated CodeQL gate and schema-pattern regression tests and wired these into PR Validation.

## 4. Prevention controls and immutable action references

Action SHAs in `.github/workflows/` were pinned and checked against public GitHub commit records during implementation:

- `actions/checkout`: `11d5960a326750d5838078e36cf38b85af677262`
- `actions/setup-node`: `49933ea5288caeca8642d1e84afbd3f7d6820020`
- `actions/setup-dotnet`: `67a3573c9a986a3f9c594539f4ab511d57bb3ce9`
- `github/codeql-action` (init/analyze): `33d7b85da1a04171e211befe2c739303b9a4c066`
- `actions/upload-artifact`: `ea165f8d65b6e75b540449e92b4886f43607fa02`

The pin-check runs the version committed in the PR base, not only the PR’s candidate copy. It requires the base SHA, fetches full history, and uses `set -euo pipefail` so an unavailable baseline script cannot pass through an empty pipeline. Existing Semgrep, npm audit, NuGet audit, and CodeQL workflows remain present. CodeQL remains conditional for private repositories without the required license variable; branch-protection requirements are owned by repository settings and were not inspected.

## 5. Remaining risks and evidence limits

- All new and existing targeted tests, builds, audits, and Semgrep/CodeQL scans are **not run** in this pass, as explicitly required by the execution policy. The ID statuses marked FIXED describe implementation/source evidence, not fresh scanner or runtime verification.
- The Semgrep platform rows remain Open until a fresh scan and authorized platform triage. The delivered CSVs are from the prior source snapshot.
- The exported Code/combined/Supply Chain row count is inconsistent with the task’s stated 55. All 61 distinct exported IDs are listed above; no additional, unprovided IDs can be reconciled.
- The API’s production Data Protection key-ring path is an absolute deployment-provided path, and the deployment mount/ownership was not established from repository source. `$APP_UID` access to that production mount remains a deployment validation item. No production config or mount was changed.
- Compose files were not rendered or run. Runtime compatibility of capability restrictions, Nginx’s non-root start, and API writable directories awaits container validation.
- The project-report export localization lookup was judged a false positive by source inspection; no browser test was run.
- No assertion is made that the repository has zero remaining vulnerabilities. Export rows marked not applicable refer to retired paths or required writable state, not scanner suppression.

## 6. Verification completed versus pending

| Check | Result |
|---|---|
| Read `AGENTS.md`, P0-07B/P0-07C reports, and available security guidance | Completed |
| Supplied CSV row counts and unique IDs | Completed: 61 rows, 61 unique IDs across four files |
| Source reconciliation against the changed/current paths | Completed to source-review level; dispositions are in Section 2 |
| GitHub Action commit existence checks | Completed for the listed SHAs via GitHub commit lookup |
| Worktree diff review and whitespace check | Completed; `git diff --check` reported no whitespace errors |
| React, API, Python/Node security regression tests | Not run |
| API/React builds, container builds, Compose rendering/runtime checks | Not run |
| npm/NuGet audit, Semgrep, CodeQL | Not run |
| Database reset/write, migration, seed, browser UAT, Production operation | Not run |

## 7. Changed files and compatibility considerations

P0-08 implementation paths:

- `.agents/skills/codeql/scripts/test_build_mode_claims.py`
- `.agents/skills/codeql/scripts/test_shell_blocks.py`
- `.agents/skills/security-audit/validate-findings.cjs`
- `.agents/skills/security-audit/validate-findings.test.cjs`
- `.agents/skills/semgrep/scripts/run-scans.sh`
- `.github/dependabot.yml`
- `.github/scripts/check-action-pins.py`
- `.github/scripts/enforce-codeql-severity.py`
- `.github/scripts/test_enforce_codeql_severity.py`
- `.github/workflows/codeql.yml`
- `.github/workflows/pr-validation.yml`
- `apps/api/Dockerfile`
- `apps/api/Dockerfile.worker`
- `apps/api/src/NEXUS.Modules.Documents/Infrastructure/LocalStorageProvider.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/LocalStorageProviderPathTraversalTests.cs`
- `apps/web-react/Dockerfile`
- `apps/web-react/src/components/ui/rich-text-editor.test.tsx`
- `apps/web-react/src/features/projects/ProjectReportingConfigurationPage.tsx`
- `apps/web-react/src/features/projects/lib/project-reporting-configuration.ts`
- `apps/web-react/src/test/container-runtime-security.test.ts`
- `apps/web-react/src/test/project-reporting-configuration-safety.test.ts`
- `docker-compose.yml`
- `docs/reports/README.md`
- `docs/security/code-review/CODE-REVIEW-07C-DEPENDENCY-VULNERABILITY-CLOSURE.md`
- `docs/reports/poc-vite/BENCHMARK-REPORT.md` (deleted)
- `poc-vite/` (removed)

Compatibility was preserved for the existing API interfaces and report exports; project plan IDs remain sourced from the API and are serialized from `Map` selections into the same save DTO shape. Existing sanitizer behavior, service ports, SQL Server/Redis persistent volumes, and protected database gate behavior remain. Non-root containers require the documented provisioned writable directories and deployment-owned Data Protection storage.

## 8. Skills and agent contributions

### Skills/guidance applied

- Repository `AGENTS.md` and the security remediation reports.
- `security-audit` guidance, including client-side, container/deployment, and supply-chain boundaries.
- `semgrep` workflow and existing Semgrep CI configuration; no scan was run.
- `codeql` guidance for SARIF quality/severity handling; no CodeQL database or scan was run.
- `frontend-data-integrity` for React changes.
- ASP.NET Core/.NET API context was reviewed; no endpoint, EF model, migration, or database change was made.
- No dedicated Docker, React implementation, .NET persistence, or CI-specific skill was present in the repository’s installed skill catalog. The relevant repository instructions and source patterns were used instead.

### Mandatory subagent contributions

- **A — Legacy cleanup & Supply Chain:** Established that `poc-vite` had no supported app/API or CI consumers; removed its source, lockfile, dependencies and report reference; reconciled the 12 POC Supply Chain IDs.
- **B — Backend & storage:** Fixed normalized path-prefix comparison and added path traversal regression tests. Reviewed the EF database gate and classified the SQL injection candidate as a false positive because executed SQL call sites are fixed literals and dynamic values are parameterized/identifiers quoted.
- **C — React & containers:** Preserved the rich-text sanitizer and added malicious-markup tests; moved plan-ID selection to `Map`; changed API/worker/Nginx runtime users and writable paths; added Compose `no-new-privileges` and capability restrictions while retaining stateful volumes.
- **D — CI, GitHub Actions & prevention:** Pinned actions, added Dependabot cooldowns and pin enforcement, removed dynamic `eval`, constrained regex usage, strengthened the CodeQL gate, and added/wired security gate tests. Its initially proposed CodeQL SHA was independently checked and replaced with an existing official commit before integration.
- **Main — coordination:** Reconciled all 61 IDs from the supplied exports, reviewed integration diffs and compatibility, preserved concurrent changes, and recorded verification limits without running tests/builds/scans.
