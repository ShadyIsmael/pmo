# NEXUS PMO / FOPX PM — Central Reports Directory

This catalog indexes and classifies all official reports, audits, benchmarks, and diagnostic logs across the repository.

---

## 1. Security & Compliance Reports
Comprehensive security evaluations, vulnerability assessments, and permission audit reports.

- **[Security Audit Report](security/SECURITY-AUDIT-REPORT.md)** — Architectural security evaluation, frontend dependency analysis, DB TLS configuration, and JWT recommendations.
- **[Full Penetration Test Report](../security/FULL-PENETRATION-TEST-REPORT.md)** — Repository penetration test and vulnerability verification.
- **[Permissions Implementation Report](../security/PERMISSIONS-IMPLEMENTATION-REPORT.md)** — Authorization system implementation status and permission alignment.
- **Permission Audit Suite (`docs/security/permission-audit/`):**
  - [Permission Audit Executive Summary](../security/permission-audit/PERMISSION-AUDIT-EXECUTIVE-SUMMARY.md)
  - [Active Deny Enforcement Report](../security/permission-audit/ACTIVE-DENY-ENFORCEMENT-REPORT.md)
  - [Permission Gap Report](../security/permission-audit/PERMISSION-GAP-REPORT.md)
  - [Security Test Results](../security/permission-audit/SECURITY-TEST-RESULTS.md)
  - [Final Permissions Closure Report](../security/permission-audit/FINAL-PERMISSIONS-CLOSURE-REPORT.md)
  - [Database Migration & Seed Report](../security/permission-audit/DATABASE-MIGRATION-SEED-REPORT.md)
  - [Legacy Removal Report](../security/permission-audit/LEGACY-REMOVAL-REPORT.md)

---

## 2. Testing, Load & Diagnostics Reports
Test run outputs, load testing evaluations, and latency diagnostics.

- **Automated Test Run Snapshots (`docs/reports/testing/`):**
  - [Vitest Focused Suite (Final)](testing/vitest-focused-final.json)
  - [Vitest Focused Suite (Next)](testing/vitest-focused-next.json)
  - [Vitest Focused Suite (Base)](testing/vitest-focused.json)
- **Performance & Load Testing (`docs/testing/load/`):**
  - [Load Test Report (Baseline)](../testing/load/LOAD-TEST-REPORT.md)
  - [Load Test Report After Auth Fix](../testing/load/LOAD-TEST-REPORT-AFTER-AUTH-FIX.md)
  - [Login Latency Diagnosis Report](../testing/load/LOGIN-LATENCY-DIAGNOSIS-REPORT.md)
  - [Login Cancellation Diagnosis Report](../testing/load/LOGIN-CANCELLATION-DIAGNOSIS-REPORT.md)
  - [Login Pipeline Optimization Report](../testing/load/LOGIN-PIPELINE-OPTIMIZATION-REPORT.md)
  - [Auth 5xx Diagnosis Report](../testing/load/AUTH-5XX-DIAGNOSIS-REPORT.md)

---

## 3. Quality Assurance & Parity Verification
Verification gates, UAT checklists, and release readiness audits.

- **[React/Vite Parity Gate](../quality/react-vite-parity-gate.md)** — Route and feature parity verification for the supported React/Vite frontend under ADR-009.
- **[Sprint 7B-01 UAT Execution Report](../quality/SPRINT-7B-01-UAT-EXECUTION-REPORT.md)** — UAT execution results for the financial permission matrix.
- **[Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md)** — Master UAT scenarios, regression criteria, and permissions verification checklist.
- **[Frontend Data Integrity Remediation](../quality/FRONTEND-DATA-INTEGRITY-REMEDIATION.md)** — Data integrity audit and remediation report.

---

## 4. Architecture, Planning & Benchmarks
Project roadmaps, parity benchmarks, and architectural audits.

- **[Project Plan Desktop Parity](PROJECT-PLAN-DESKTOP-PARITY.md)** — Strategic plan and execution stages for desktop parity.
- **[Vite POC Benchmark Report](poc-vite/BENCHMARK-REPORT.md)** — Performance benchmarking for the Vite architecture adoption.
- **[Dead Code Audit](../development/DEAD-CODE-AUDIT.md)** — Unused files, dead components, and cleanup backlog.
- **[Financial Current State Audit](../modules/FINANCIAL-CURRENT-STATE-AUDIT.md)** — Deep dive into financial domain models and legacy constraints.
- **[Financial Sprint 2 Implementation Report](../modules/FINANCIAL-SPRINT-2-IMPLEMENTATION-REPORT.md)** — Implementation summary for project financial baseline.

---

## 5. Execution & System Build Logs
Detailed build and execution logs archived by subsystem.

- **[Root Build & Runtime Logs](../logs/root/)** — Build logs, API logs, and execution traces.
- **[API Application Logs](../logs/apps/api/)** — Backend API execution and error logs.
- **[Web Application Logs](../logs/apps/web/)** — Frontend runtime logs.
