# Universal Project Intelligence (UPI)

UPI creates a local index of source files, heuristic symbols, module ownership, relationships, and estimated reading cost. It helps coding agents begin with a smaller, relevant set of files.

## First run and task routing

From PowerShell at the repository root:

```powershell
.\tools\upi\upi.ps1 init --root .
.\tools\upi\upi.ps1 validate --root .
.\tools\upi\upi.ps1 route --root . --task "تعديل حالة المشروع" --budget 16000
```

The first run creates `INDEX.md`, a versioned snapshot under `snapshots/`, module and dependency diagrams, and task manifests under `task-scopes/`. Read the generated manifest before opening source files. Expand the suggested scope only when task evidence or project instructions require it.

When a request is in Arabic or another non-English language, route a concise English equivalent with the relevant feature and action terms; source paths and symbols are English, so this gives more precise file ranking.

On other operating systems, run the same commands with `python tools/upi/upi.py` instead of the PowerShell launcher. The launcher uses an installed Python 3.10+ interpreter when available and falls back to the local Codex Python runtime on this device. Set `UPI_PYTHON` to an interpreter path when needed.

## Refresh behavior

`route` checks the source inventory before planning and reparses files whose size or modification time changed, plus new or deleted files. After application-code changes, run:

```powershell
.\tools\upi\upi.ps1 refresh --root .
.\tools\upi\upi.ps1 validate --root .
```

The optional `watch --interval 5` command polls only while it remains running. UPI does not install a background service, and its routing instructions are workflow guidance rather than an operating-system access boundary.

## Project-specific coverage

`tools/upi/upi_config.json` maps the backend modules and supported React feature folders to shared module names. It includes English and Arabic task aliases. Build outputs, dependencies, backups, temporary folders, tool configuration, and UPI's own code are excluded. Untracked source files in the project are included when they match the configured source types.

The scan reads source files but does not save their bodies or snippets. It writes its index and manifests under `docs/architecture`; it does not access application databases or call external services. Git is queried locally for the current commit identifier.

## Execution-flow coverage and token estimates

UPI v1.1 adds execution-flow coverage for Project Status updates and Project Status Report updates. The router checks the frontend save handler, API client, HTTP method and route, backend action, service contract and implementation, persistence call, and focused test files. It reports each required step and relationship in `critical_path_coverage`; a missing required item blocks a completion claim. The manifest distinguishes the project-level status field from an editable status report.

The router puts critical-path files ahead of optional files and calculates selected reading-token estimates only after checking that path. Required files are retained even when they exceed the soft token or file limit. A route-only critical-flow configuration change does not invalidate per-file index metadata. Source edits are still detected and refreshed incrementally, and a relationship-graph version change rebuilds edges from saved file metadata without rereading unchanged source files.

The relationship index records static imports and calls, matching HTTP method plus normalized route, typed interface calls, declared interface implementations, and a persistence call inside the enclosing update method. If a multiline import's named binding is not in the saved metadata, routing can combine the resolved import path, call name, and target symbol; it labels that edge as inferred and unconfirmed. Confidence labels state what source evidence supports each edge. These are source-text heuristics, not Roslyn, TypeScript compiler, or runtime tracing. For example, a class declaration that implements an interface does not prove which implementation dependency injection resolves. Treat the index as a starting map and verify current source before changing code.

If `tiktoken` is available for the configured tokenizer, selected ranges use that tokenizer. Otherwise UPI reports `estimated:chars/3.5`. The manifest estimates initial source-reading cost; it does not predict total model, reasoning, tool, or output usage.

## Agent adapters

- Codex discovers the project skill in `.agents/skills/universal-project-intelligence/` and follows the UPI section in the root `AGENTS.md`.
- Cursor uses `.cursor/rules/upi-architecture.mdc` and the root `AGENTS.md`.
- Kilo Code can load the project skill through its existing `.agents/skills` path and the UPI rule in `.kilocode/rules/`.

Start a new agent session after installing the skill so the tool can discover the new files.

## Latest implementation report

See [UPI v1.1 execution-flow report](UPI-V1.1-EXECUTION-FLOW-REPORT-2026-11-23.md) for the before/after routing measurements, Repo FOPX route results, tests, and explicit uncertainty notes.
