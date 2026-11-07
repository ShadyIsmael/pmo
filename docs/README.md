# NEXUS PMO / FOPX PM — Documentation Index

Welcome to the central documentation index for **NEXUS PMO** (FOPX PM). This directory contains all architectural specifications, API references, domain models, reports, security documentation, and testing guides.

---

## 📚 Table of Contents

1. [Architecture & Design](#architecture)
2. [API Reference](#api)
3. [Domain Models & Modules](#domain-and-modules)
4. [Reports & Audits](#reports-and-audits)
5. [Quality Assurance & Verification](#quality-assurance)
6. [Security & Compliance](#security)
7. [Testing & Performance](#testing)
8. [UI & Design System](#ui-and-design-system)
9. [Releases & Versioning](#releases-and-versioning)
10. [Development & Guides](#development)
11. [Integrations & Plugins](#integrations-and-plugins)
12. [System Logs](#system-logs)

---

## Architecture
- [Architecture Overview](architecture/README.md)
- [NEXUS PMO Architecture (Historical Snapshot)](architecture/NEXUS-PMO-ARCHITECTURE.md)
- [Architecture Status](architecture/ARCHITECTURE-STATUS.md)
- [Module Catalogue](architecture/module-catalogue.md)
- [Plugin Architecture](architecture/plugin-architecture.md)
- [Domain Model](architecture/DOMAIN-MODEL.md)
- [Deployment Architecture](architecture/DEPLOYMENT-ARCHITECTURE.md)
- [Security Architecture](architecture/SECURITY-ARCHITECTURE.md)
- [Integration Architecture](architecture/INTEGRATION-ARCHITECTURE.md)
- [Multi-Tenancy Strategy](architecture/multi-tenancy.md)
- [Permission Model](architecture/permission-model.md)
- [Project Planning Model](architecture/PROJECT-PLANNING-MODEL.md)
- [Code Quality Guidelines](architecture/CODE-QUALITY-GUIDELINES.md)
- [Frontend Engineering Guidelines](architecture/FRONTEND-ENGINEERING-GUIDELINES.md)

### Architecture Decisions (ADRs)
- [ADR-001: Modular Monolith](decisions/adr-001-modular-monolith.md)
- [ADR-002: Technology Stack](decisions/adr-002-technology-stack.md)
- [ADR-003: Plugin Architecture](decisions/adr-003-plugin-architecture.md)
- [ADR-005: .NET 10 Upgrade](decisions/adr-005-dotnet-10-upgrade.md)
- [ADR-006: Project Planning Module](decisions/adr-006-project-planning-module.md)
- [ADR-007: Project Planning Architecture Final](decisions/adr-007-project-planning-architecture-final.md)
- [ADR-009: React/Vite Frontend Adoption](decisions/adr-009-react-vite-frontend.md)

---

## API
- [API Conventions](api/conventions.md)
- [API Endpoints Reference](api/endpoints.md)
- [Validation Errors Specification](api/validation-errors.md)

---

## Domain and Modules
- [Domain Entities Reference](domain/entities.md)
- [Project Management Core & EVM](modules/PROJECT-MANAGEMENT-CORE.md)
- [Financial Management Planned Payments](modules/FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md)
- [Financial Target Business Model](modules/FINANCIAL-TARGET-BUSINESS-MODEL.md)
- [Financial Redesign Roadmap](modules/FINANCIAL-REDESIGN-ROADMAP.md)
- [Financial Data Preservation Inventory](modules/FINANCIAL-DATA-PRESERVATION-INVENTORY.md)
- [Portfolio Management Core](modules/PORTFOLIO-MANAGEMENT-CORE.md)
- [Resource Management Core](modules/RESOURCE-MANAGEMENT-CORE.md)
- [Resource Capacity and Allocation](modules/RESOURCE-CAPACITY-AND-ALLOCATION.md)
- [Document Management Core](modules/DOCUMENT-MANAGEMENT-CORE.md)
- [My Work](modules/MY-WORK.md)
- [Authentication and Identity](modules/AUTHENTICATION-AND-IDENTITY.md)
- [Meetings Workspace](modules/MEETINGS.md)
- [Platform Settings](modules/PLATFORM-SETTINGS.md)
- [Audit Trail Specification](modules/AUDIT-TRAIL.md)

---

## Reports and Audits
See the dedicated **[Reports Catalog](reports/README.md)** for a full index of all reports across the repository:
- [Security Audit Report](reports/security/SECURITY-AUDIT-REPORT.md)
- [Project Plan Desktop Parity](reports/PROJECT-PLAN-DESKTOP-PARITY.md)
- [Vite POC Benchmark Report](reports/poc-vite/BENCHMARK-REPORT.md)
- [Vitest Focused Test Results](reports/testing/)
- [Financial Current State Audit](modules/FINANCIAL-CURRENT-STATE-AUDIT.md)
- [Dead Code Audit](development/DEAD-CODE-AUDIT.md)

---

## Quality Assurance
- [Master UAT Checklist](quality/MASTER-UAT-CHECKLIST.md)
- [React/Vite Parity Gate](quality/react-vite-parity-gate.md)
- [Sprint 7B-01 UAT Execution Report](quality/SPRINT-7B-01-UAT-EXECUTION-REPORT.md)
- [Frontend Data Integrity Remediation](quality/FRONTEND-DATA-INTEGRITY-REMEDIATION.md)
- [Financial Permission Matrix Proposal](quality/SPRINT-7B-01-FINANCIAL-PERMISSION-MATRIX-PROPOSAL.md)

---

## Security
- [Security Audit Report](security/SECURITY-AUDIT-REPORT.md)
- [Full Penetration Test Report](security/FULL-PENETRATION-TEST-REPORT.md)
- [Permissions Implementation Report](security/PERMISSIONS-IMPLEMENTATION-REPORT.md)
- [Permissions Target Matrix](security/PERMISSIONS-TARGET-MATRIX.md)
- [Permissions Gap Analysis](security/PERMISSIONS-GAP-ANALYSIS.md)
- [Permission Audit Reports](security/permission-audit/)

---

## Testing
- [Load & Latency Testing Documentation](testing/load/README.md)
- [Baseline Load Test Report](testing/load/LOAD-TEST-REPORT.md)
- [Load Test Report After Auth Fix](testing/load/LOAD-TEST-REPORT-AFTER-AUTH-FIX.md)
- [Login Latency Diagnosis Report](testing/load/LOGIN-LATENCY-DIAGNOSIS-REPORT.md)

---

## UI and Design System
- [Design System Guide](ui/design-system.md)
- [Dashboard Premium Redesign](ui/dashboard-premium-redesign.md)

---

## Releases and Versioning
- [Release History](releases/RELEASE-HISTORY.md)
- [Versioning Policy](releases/VERSIONING-POLICY.md)
- [Release Notes 1.0.0](releases/1.0.0.md)
- [Versioning Criteria Specification](releases/FOPX-PM-VERSIONING-CRITERIA.docx)

---

## Development
- [Local Setup Guide](development/local-setup.md)
- [Coding Conventions](development/coding-conventions.md)
- [Development Roadmap](development/roadmap.md)
- [EF Core Database Workflow and Seeding](development/ef-core-database-workflow.md)
- [Agent Context & Guide](architecture/AGENT-CONTEXT.md)
- [.NET SDK Troubleshooting](development/dotnet-sdk-troubleshooting.md)

---

## Integrations and Plugins
- [Email Notifications](integrations/email-notifications.md)
- [Firebase Cloud Messaging](integrations/firebase-cloud-messaging.md)
- [Plugin System Architecture](architecture/plugin-architecture.md)
- [Plugins Catalog](plugins/)

---

## Project Overview & Branding
- [Comprehensive Project Overview](project/README.md)
- [FOPX PM Rebranding Implementation](project/fopx-pm-rebranding.md)

---

## System Logs
Archived diagnostic, build, and execution logs:
- [Root Build Logs](logs/root/)
- [API Application Logs](logs/apps/api/)
- [Web Runtime Logs](logs/apps/web/)
- [Load Test Logs](logs/tests/load/results/)
- [Work Logs](logs/work/)

- [Manager Appointment Independence](modules/MANAGER-ASSIGNMENT-SYSTEM-USERS.md)
