# Sprint 5 — Task 04: Program & Portfolio Cash Flow

**Status:** IMPLEMENTED — UNVERIFIED  
**Owner:** Reporting & Aggregation; Frontend and QA support  
**Date:** 2026-11-23

## Outcome

Added Program and Portfolio Cash Flow endpoints and views using the same Project Cash Flow reader/calculator. Existing scoped hierarchy membership resolution is reused. Program and Portfolio endpoints require their existing paired view policies; membership is Tenant/scope filtered, Portfolio membership includes direct and Program projects, and duplicate Project IDs are removed before aggregation.

The aggregation keeps date buckets and lifecycle measures separate, groups by currency without conversion, propagates Partial/Unavailable status and quality codes, and exposes contributing Projects and their direct/Program relationship. Missing or mismatched Project responses are reported as unavailable rather than silently omitted.

Added Excel/PDF exports for Project, Program, and Portfolio reports. Export builders normalize the authoritative report DTO and include period, currency-specific lifecycle totals, dated/undated source detail, timing variance, and data-quality notices. Program/Portfolio views are mounted in the existing financial tabs and drill down to Project Financial Workspace routes.

## Verification

- Frontend typecheck: **PASS**.
- Focused Cash Flow frontend/export tests: **21 passed across 4 files**, including Excel export coverage.
- Frontend production build: **PASS**.
- C# hierarchy aggregation and authorization tests were added but could not be executed. The focused .NET test build was stopped in the existing IKVM/MppAdapter dependency phase before test execution; earlier attempts exited without useful test/compiler output.
- Endpoint policy source was reviewed for Program and Portfolio permission pairs. Runtime authorization and persisted RolePermission grants are not verified.

No database operation or Agent UAT was performed. No new schema dependency or Sprint 5 migration was introduced. Product Owner runtime/persistence verification remains pending.
