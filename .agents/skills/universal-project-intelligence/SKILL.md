---
name: universal-project-intelligence
description: Use FIRST for any repository coding, architecture, bug-fix or refactor task to consult the persistent module/file/symbol/dependency index, select a minimal justified source scope, and estimate token reading cost BEFORE inspecting application source. Automatically refreshes changed/new/renamed/deleted files, updates architecture diagrams, and records task manifests under docs/architecture. Never modifies application code during indexing.
---

# Universal Project Intelligence (UPI)

Use this workflow for codebase work in a repository containing `tools/upi/upi.py`.

## Mandatory sequence

On this Windows project, invoke the commands below through `.\tools\upi\upi.ps1`; it uses an installed Python 3.10+ interpreter or the local Codex runtime without changing PATH. On other hosts, use the equivalent `python tools/upi/upi.py` commands.

When the user's request is in Arabic or another non-English language, route a concise English equivalent that preserves its feature and action terms so English paths and symbols rank precisely.

1. Follow all applicable `AGENTS.md`, local rules, and product-owner constraints **before** any analysis. UPI does not override those rules.
2. Check `docs/architecture/INDEX.md`. If it is missing, run `python tools/upi/upi.py init --root .`; otherwise the router automatically checks freshness and incrementally refreshes new, modified, and deleted source files.
3. BEFORE general code search, call:
   `python tools/upi/upi.py route --root . --task "<actual user task>" --budget 16000`
   Adjust the planning budget to the real model/context constraints if known.
4. Inspect the emitted Task Context Manifest in `docs/architecture/task-scopes/`; read only its selected files and line ranges first.
5. Treat external module edges as **dependency awareness**, not permission to load or edit every related source file. Expand only for specific evidence or explicit task requirements; document why.
6. If the index is incomplete, wrong, or classifies ownership ambiguously, inspect targeted source to verify and correct `tools/upi/upi_config.json` module rules. Source code remains authoritative.
7. After code changes, run `python tools/upi/upi.py refresh --root .` and `python tools/upi/upi.py validate --root .`. This refresh is read-only regarding application code and database.
8. Explain any scoped-out dependencies, token estimate uncertainty, and any failed refresh/validation in the task report.

## UPI v1.1 execution-flow coverage

- When a route returns `critical_path_coverage.active: true`, inspect the listed required steps and relationships before treating the selected scope as complete. A failed gate or missing step/relationship blocks a completion claim; include the missing evidence in the report.
- The router selects required critical-path files before optional files and calculates selected reading tokens only after checking path coverage. Required files can exceed the soft budget or file cap; never drop a required step to make the estimate fit.
- Read every `unconfirmed_relationships` entry as an explicit limit. Source declarations and static route matches are not runtime traces. In particular, an interface implementation does not prove which implementation dependency injection resolves.
- For project changes, distinguish **Project Status** updates from **Project Status Report** updates. The former routes through `CreateProjectSheet`, `useUpdateProject`, `ProjectController.Update`, `IProjectService` / `ProjectService.UpdateAsync`, and its focused tests. The latter routes through the status-report editor, status-report API endpoint and service, and report persistence tests.
- If the critical-path gate is incomplete, start with the missing basic implementation or focused test evidence. Expand to another module only when a concrete missing relationship requires it; dependency awareness alone does not authorize reading another module.
- The v1.1 relationship index records static imports/calls, full HTTP method and route matches, typed interface calls, declared interface implementations, and same-method persistence calls with confidence labels. These remain source-text evidence, not compiler-resolved semantics.

## Modes

- `init`: first full source scan, then snapshot-backed architecture index.
- `refresh`: stat-based incremental refresh, including new, deleted, renamed and modified source files; rebuild graph from saved metadata.
- `route --task "..." --budget N`: index freshness + module selection + ranked files/symbol ranges + token planning + task manifest.
- `explain --module projects`: module overview and diagram location.
- `validate`: integrity and stale-source checks (nonzero exit for stale or broken).
- `watch --interval 5`: optional continuous polling **only while running**; cannot guarantee instant sync when stopped. Pre-task refresh is the safety net.

## Safety and limits

- During indexing, do NOT edit application source, run EF migrations, access production databases, run UAT, or reset databases.
- The router recommends a scope but does not technically prevent an agent from reading other files. Never treat this as a filesystem security boundary.
- Parsing and C# / TS relationships in v1 are heuristic. Explicit relative imports are verified; type-name and C# namespace edges are inferred. No invented semantic calls.
- If `tiktoken` is absent, token values are **estimates** based on character lengths. Even when available, projected source-reading tokens do not equal total model/tool execution cost.
- The generated active snapshot is selected by `docs/architecture/CURRENT.json`; never manually combine indexes from different snapshots.
- Snapshot data is project-local. Do not transmit source code or private repository content to external services.
