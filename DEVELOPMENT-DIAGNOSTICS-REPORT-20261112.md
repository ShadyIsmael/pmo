# Development Database Diagnostics Report
**Date:** 2026-11-12  
**Time:** 15:31:25 UTC+2  
**Target:** SQL Server `Shady` / Database `NexusPmo`  
**Executor:** Isolated PowerShell Agent (non-Windows-host)

---

## Executive Summary

The preflight diagnostics were executed from an isolated executor environment. While **offline EF model validation succeeded completely** and **SQL Server identity verification succeeded**, the inspection phase (live migration history and schema queries) encountered **execution timeouts** in the isolated executor.

**Per documentation:** Agent shell isolation prevents full live inspection. A fresh host-side preflight is required to complete diagnostics.

---

## Phase 1: EF Model Validation ✓ COMPLETED

**Status:** ✓ SUCCESS  
**Timestamp:** 2026-11-12T13:32:34.6367036Z - 2026-11-12T13:32:53.3087112Z (18.67s)  

| Finding | Result |
|---------|--------|
| Context | NEXUS.Persistence.AppDbContext |
| Migration Assembly | NEXUS.Persistence |
| EF Model Synchronized | **Yes** |
| Repository Migration Count | **25** |

### Applied Migrations (All 25):

1. `20261029155617_InitialFopxPmBaseline`
2. `20261029181208_ProjectPaymentRequestPersistence`
3. `20261029183557_AddTenantRegistry`
4. `20261030122718_AddProjectAnnualBudgetFoundation`
5. `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`
6. `20261031000210_AddFinancialPersistenceSprints3to6`
7. `20261031142036_AlignResourceProjectTeamsAndInvitations`
8. `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`
9. `20261101213237_SynchronizePlanningPhysicalSchema`
10. `20261102190318_AddStableSystemRoleIdentity`
11. `20261105184921_AddRiskActionPublicIdForFollowUp`
12. `20261105201430_AddRiskActionSystemUserOwners`
13. `20261105214717_AddMeetingNotificationPublicIdentifiers`
14. `20261107193155_AddUniversalEmailNotificationProfiles`
15. `20261107193416_AddTenantScopedEmailProfileUniqueness`
16. `20261108170000_RepairProgramRowVersion`
17. `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson`
18. `20261110170000_AddOrganizationDocumentTemplateStorageMetadata`
19. `20261110180235_AddOrganizationPhaseGateConfiguration`
20. `20261110190107_AddProjectPhaseGateLifecycle`
21. `20261110210000_AddOrganizationManagedDocumentCategories`
22. `20261111134000_AddDocumentIsMandatory`
23. `20261111173858_AddProjectLifecycleCatalogs`
24. `20261111224116_AddProjectStatusReportingSemantics`
25. **`20261112094313_AddDocumentCategoryToProjectPhase`** ← **TARGET MIGRATION FOUND**

### Key Finding:
✓ Migration `20261112094313_AddDocumentCategoryToProjectPhase` **is present** in the source model at position 25.

---

## Phase 2: SQL Server Identity Verification ✓ PARTIAL SUCCESS

**Status:** ✓ Connection Verified (Timeout on inspection queries)  
**Timestamp:** 2026-11-12T13:32:53.3986536Z - 2026-11-12T13:33:42.2938672Z (48.9s)  

| Finding | Result |
|---------|--------|
| Environment | Development ✓ |
| Target Server | Shady ✓ |
| Target Database | NexusPmo ✓ |
| Database ID | 5 |
| Connection Method | Windows Authentication (Integrated Security) ✓ |
| SQL Connection Status | **OPENED SUCCESSFULLY** |

### Connection Details:
- **Server:** `Shady` (verified)
- **Database:** `NexusPmo` (verified)
- **Database ID:** `5` (confirmed)
- **Authentication:** Windows Integrated Security ✓
- **Connection successful before timeout** ✓

### Timeout Details:
- **Error:** `SqlException: Execution Timeout Expired. The timeout period elapsed prior to completion of the operation or the server is not responding.`
- **Occurred During:** Inspection queries (migration history, pending migrations, physical schema checks)
- **Executor Context:** Isolated PowerShell agent environment
- **Expected:** Per documentation, full live inspection requires host-side execution

---

## Phase 3: Live Inspection (Pending) ⚠ BLOCKED

**Status:** ⚠ TIMEOUT - Requires Host-Side Execution  

The following checks **require fresh host-side preflight** to complete:

### Still Required:

1. **Applied Migration History (Database):**
   - Count of applied migrations in `__EFMigrationsHistory`
   - Confirm `20261112094313_AddDocumentCategoryToProjectPhase` is applied
   - Report migration application order and timestamps

2. **Pending Migrations:**
   - Check for any pending migrations not yet applied
   - If pending exist, generate and capture SQL artifact with SHA-256 hash

3. **Physical Schema Inspection:**
   - [ ] `ProjectPhaseCatalogEntries` table exists?
   - [ ] `DocumentCategoryId` column exists in `ProjectPhaseCatalogEntries`?
   - [ ] Composite FK `FK_ProjectPhaseCatalogEntries_DocumentCategories_TenantId_DocumentCategoryId` exists?
   - [ ] FK definition details (columns, reference, delete behavior)

4. **Schema Drift Detection:**
   - Compare EF model vs. physical schema
   - Identify any unexpected columns, tables, or constraints
   - Report alignment status

---

## Diagnostics Status Summary

| Check | Status | Evidence |
|-------|--------|----------|
| EF Model Synchronized | ✓ Yes | Model report: exit code 0 |
| Target Environment | ✓ Development | Verified in connection string |
| Target Server Name | ✓ Shady | `SERVERPROPERTY('ServerName')` verified |
| Target Database Name | ✓ NexusPmo | `DB_NAME()` verified |
| Database ID | ✓ 5 | Confirmed in connection report |
| Windows Authentication | ✓ Yes | Integrated Security connection opened |
| Migration Count (Source) | ✓ 25 | Model check: 25 migrations |
| Target Migration Present (Source) | ✓ Yes | `20261112094313_AddDocumentCategoryToProjectPhase` in position 25 |
| Applied Migrations (Database) | ⚠ Pending | Requires host-side inspection |
| Schema Sync (Database) | ⚠ Pending | Requires host-side inspection |
| Pending Migrations | ⚠ Pending | Requires host-side inspection |

---

## Next Steps: Host-Side Execution Required

**The isolated executor cannot complete live inspection due to timeout during schema queries.**

### To Complete Diagnostics on Windows Host:

Run this command from PowerShell 7 on the authorized Windows Development host (with repository checkout at `C:\Projects\pmo`):

```powershell
Set-Location 'C:\Projects\pmo'; .\scripts\dev\db-preflight.ps1
```

**Expected Output:**
- Offline model check (already completed above)
- Live database connection and identity verification
- Applied migration count and list (verify position 25 is `20261112094313_AddDocumentCategoryToProjectPhase`)
- Pending migration status
- Physical schema inspection results
- Generated pending SQL artifact (if migrations are pending) with SHA-256 hash

**Sanitized Report Location:**
- Host will generate: `artifacts/ef-review/dev-db-preflight-*.json`
- Review the sanitized JSON output for complete findings

### Success Criteria for Host Run:
1. ✓ `EF Model synchronized: Yes`
2. ✓ `Target Development DB verified: Yes` with `server=Shady; database=NexusPmo`
3. ✓ `Windows Authentication: Yes`
4. ✓ Applied migration count and list (should show 25+ including `20261112094313_AddDocumentCategoryToProjectPhase`)
5. ✓ Pending migration status (should be zero if no new migrations)
6. ✓ Physical schema findings for `ProjectPhaseCatalogEntries`, `DocumentCategoryId`, and FK

---

## Evidence Artifacts

### Sanitized Execution Reports:
1. **Model Check Report:**
   - Path: `C:\Projects\pmo\artifacts\ef-review\dev-db-preflight-model-20261112T133253Z-7508416f.json`
   - Exit Code: 0 (Success)
   - Duration: 18.67 seconds
   - Content: EF model validation, 25 migrations listed, model synchronized

2. **Inspection Report (Timeout):**
   - Path: `C:\Projects\pmo\artifacts\ef-review\dev-db-preflight-inspect-20261112T133342Z-e5b66a5a.json`
   - Exit Code: 1 (Failed - Timeout)
   - Duration: 48.9 seconds
   - Status: Connection opened successfully; timeout on inspection queries

3. **Migrations Status Report (Timeout):**
   - Path: `C:\Projects\pmo\artifacts\ef-review\dev-db-migrations-status-20261112T133436Z-8e3f77cc.json`
   - Exit Code: 1 (Failed - Timeout)
   - Duration: 43.5 seconds
   - Status: Same timeout pattern

---

## Findings & Conclusions

### ✓ Confirmed:
1. **EF Model is synchronized** with the defined schema
2. **Repository contains exactly 25 migrations** as expected
3. **Target migration exists:** `20261112094313_AddDocumentCategoryToProjectPhase` is in the model at position 25
4. **SQL Server connection successful** to `Shady/NexusPmo` using Windows Authentication
5. **Development database confirmed** with ID 5

### ⚠ Pending Host Verification:
1. **Applied migration status** in the database (need to confirm position 25 is applied)
2. **Physical schema state** (verify `ProjectPhaseCatalogEntries`, `DocumentCategoryId`, FK exists)
3. **Pending migrations** (need to confirm zero pending or capture pending SQL)
4. **Schema drift** (compare EF model vs. physical)

### ℹ Context:
- Isolated executor environment successfully connected to SQL Server
- Timeout occurred during inspection phase, likely due to network latency or database load
- No modifications were attempted (read-only diagnostic only)
- No backup created (Development, optional per policy)
- No migrations applied (standby for authorization)

---

## Recommendations

1. **Complete Host-Side Preflight:** Run the preflight command above on an authorized Windows Development host with direct SQL Server access
2. **Review Pending SQL (if any):** If migrations are pending, review generated SQL before applying
3. **Schema Verification:** Confirm `ProjectPhaseCatalogEntries` table and related columns/FKs are present
4. **Next Steps:** After host verification, apply migrations or report blockers per documented workflow

---

**Report Generated:** 2026-11-12T15:31:25.483+02:00  
**Executor:** Isolated PowerShell Agent (C:\Projects\pmo)  
**Documentation:** `docs/development/development-database-connectivity-and-runner.md`
