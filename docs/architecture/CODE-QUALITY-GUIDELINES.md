# NEXUS PMO — Code Quality Guidelines

These guidelines are mandatory for every coding agent working in the NEXUS PMO repository. They define the minimum quality, safety, maintainability, and verification standard for application changes.

## Mandatory Reading and Acceptance Criteria

Before writing or modifying code, read and follow `docs/architecture/AGENT-CONTEXT.md` and this file. Treat both files as mandatory acceptance criteria.

## Architecture and Scope

- Follow the repository's existing architecture, module boundaries, naming conventions, layering, and implementation patterns.
- Fix root causes rather than addressing only visible symptoms.
- Preserve the existing application architecture and keep changes structured, maintainable, and production-quality.
- Preserve scope discipline. Do not add unrelated features, refactor unrelated code, or introduce new architecture without a clear requirement.
- Keep module ownership and dependency direction intact. Use established extension points and contracts.

## Separation of Concerns

- Keep UI rendering, business logic, data transformation, formatting, and infrastructure concerns separate.
- Keep functions and components focused and reasonably sized.
- Extract repeated or complex logic into named helpers, hooks, services, or components only when that extraction is justified by reuse, complexity, or a clear boundary.
- Avoid over-abstraction of harmless one-time literals and avoid overengineering.

## Reuse and Consistency

- Reuse existing helpers, shared components, API domain modules, endpoint definitions, query-key factories, configuration, constants, enums, and i18n resources.
- Do not duplicate logic or create parallel versions of existing repository capabilities.
- Avoid hardcoded application values, including API URLs and paths, query keys, statuses, roles, permissions, routes, tab IDs, currencies, locales, tenant IDs, user IDs, magic numbers, repeated labels, and environment-specific values.
- Add new shared values only when they are genuinely shared and place them in the repository's established location.
- Use the existing localization system for user-facing text; do not introduce repeated inline labels when an i18n resource is appropriate.

## Type Safety

- Preserve strict TypeScript and C# typing.
- Do not use `any`, unsafe casts, or non-null assertions to hide unresolved problems.
- Do not suppress compiler or lint errors without documenting the specific technical reason and the narrowest safe scope.
- Keep domain, API, and UI contracts aligned rather than weakening types at boundaries.

## Null, State, and Boundary Safety

- Explicitly handle `null`, `undefined`, loading, error, empty, zero, `NaN`, `Infinity`, and relevant boundary cases.
- Treat zero as a valid value; do not convert unavailable values to zero.
- Ensure formatting, calculations, comparisons, sorting, progress indicators, pagination, and date/timeline logic remain safe for missing, invalid, zero, and extreme values.
- Preserve meaningful unavailable-value behavior and use the repository's established display conventions.

## React and Side-Effect Safety

- Do not perform state updates, navigation, data mutations, or other side effects during React render.
- Keep render logic pure. Place navigation and lifecycle-dependent effects in guarded effects or event handlers with stable dependencies and loop protection where needed.
- Preserve loading, error, and empty states in interactive screens.

## Code Hygiene

- Do not leave duplicated logic, dead code, commented-out blocks, debug logs, temporary workarounds, or TODO patches in changed code.
- Keep naming clear and consistent with nearby code.
- Prefer a small, understandable change that fits the repository over a broad rewrite.
- Do not change generated files or generated output unless the repository workflow explicitly requires it.

## Tests and Verification

- Add or update focused tests whenever changed logic could regress.
- Run the applicable typecheck, lint, tests, build, and runtime verification commands for the affected area.
- Verify relevant success, failure, empty, loading, null, zero, invalid-input, and boundary paths when applicable.
- Do not claim a check passed unless it was actually run, and report meaningful limitations such as unavailable authenticated fixtures or services.

## Mandatory Self-Review Checklist

Before final reporting, confirm all applicable items below:

- [ ] `AGENT-CONTEXT.md` and this guideline file were read before implementation.
- [ ] The change follows existing architecture, module boundaries, naming, and repository patterns.
- [ ] The root cause was addressed and scope remained limited to the requested work.
- [ ] UI, business logic, data transformation, formatting, and infrastructure concerns remain appropriately separated.
- [ ] Existing helpers, shared components, API modules, endpoint definitions, query-key factories, configuration, constants, enums, and i18n were reused where applicable.
- [ ] No prohibited hardcoded application values, weakened typing, unsafe casts, hidden null assertions, or unjustified suppressions were introduced.
- [ ] Null, undefined, loading, error, empty, zero, `NaN`, `Infinity`, and relevant boundary cases were handled.
- [ ] No render-phase state updates, navigation, or side effects were introduced.
- [ ] Changed functions and components remain focused and reasonably sized.
- [ ] No duplicated logic, dead code, commented-out blocks, debug logs, temporary workarounds, or TODO patches remain.
- [ ] Focused regression tests were added or updated where appropriate.
- [ ] Applicable typecheck, lint, test, build, and runtime verification checks were run and accurately reported.

## Standard Prompt Snippet

Use this exact snippet in future coding-agent prompts:

```text
Before writing or modifying code, read and follow docs/architecture/AGENT-CONTEXT.md and docs/architecture/CODE-QUALITY-GUIDELINES.md. Treat both files as mandatory acceptance criteria. Do not start implementation before reviewing them. In the final report, confirm the changed code was reviewed against the code-quality checklist.
```
