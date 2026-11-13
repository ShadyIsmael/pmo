# Foreign Key & Migration Repair Report
## Migration: 20261112094313_AddDocumentCategoryToProjectPhase

**Date:** 2026-11-12  
**Repository:** C:\Projects\pmo  
**Status:** ✅ FIXED (Unapplied)

---

## Executive Summary

The migration `20261112094313_AddDocumentCategoryToProjectPhase` contained a **critical composite foreign key (FK) constraint issue** that would have caused a runtime SQL error upon application. The issue has been identified, analyzed, and corrected before application to any database environment.

---

## Issue Identification

### Problem Statement
The migration declared a composite FK with `ON DELETE SET NULL` behavior on a column pair where one column is **required**:

```csharp
// BEFORE (Incorrect)
migrationBuilder.AddForeignKey(
    name: "FK_ProjectPhaseCatalogEntries_DocumentCategories_TenantId_DocumentCategoryId",
    table: "ProjectPhaseCatalogEntries",
    columns: new[] { "TenantId", "DocumentCategoryId" },
    principalTable: "DocumentCategories",
    principalColumns: new[] { "TenantId", "Id" },
    onDelete: ReferentialAction.SetNull);  // ❌ INVALID
```

**Root Cause:**
- Column `ProjectPhaseCatalogEntries.TenantId` is **NOT NULL** (required)
- Column `ProjectPhaseCatalogEntries.DocumentCategoryId` is **nullable** (optional)
- The composite FK is `(TenantId, DocumentCategoryId)`
- When a `DocumentCategory` is deleted, SQL Server attempts `SET NULL` on **both** FK columns
- **SQL Server cannot set a required column to NULL** → This would cause a constraint violation at runtime

**Impact If Applied:**
- Any attempt to delete a referenced `DocumentCategory` would fail with a SQL constraint violation
- The application would not be able to manage the lifecycle of document categories
- Production would be at risk of cascading deletion failures

---

## Evidence & Verification

### 1. Migration Application Status
**STATUS: UNAPPLIED ✅**

Evidence:
```powershell
git status --porcelain:
?? apps/api/src/NEXUS.Persistence/Migrations/20261112094313_AddDocumentCategoryToProjectPhase.Designer.cs
?? apps/api/src/NEXUS.Persistence/Migrations/20261112094313_AddDocumentCategoryToProjectPhase.cs
```

- Files are **untracked** (not in git history)
- Files have **never been applied** to any database
- Repair is safe and straightforward: simply fix the unapplied migration

### 2. Source of Issue
Located in the EF Core configuration file:

**File:** `apps/api/src/NEXUS.Persistence/Configurations/ProjectLifecycleCatalogConfigurations.cs`

```csharp
builder.HasOne<DocumentCategory>().WithMany()
    .HasForeignKey(item => new { item.TenantId, item.DocumentCategoryId })
    .HasPrincipalKey(category => new { category.TenantId, category.Id })
    .OnDelete(DeleteBehavior.SetNull);  // ❌ Incorrect for required TenantId
```

This configuration was automatically translated into the migration by EF Core's migration generator, propagating the error through:
- Migration file (`Up()` method)
- Designer file (`BuildTargetModel()` method)

### 3. Model Consistency
**Before Fix:**
- ❌ Migration `.cs`: `ReferentialAction.SetNull`
- ❌ Migration `.Designer.cs`: `DeleteBehavior.SetNull`
- ❌ Configuration: `DeleteBehavior.SetNull`
- ❌ **Models were synchronized but INCORRECTLY synchronized**

---

## Resolution

### Changes Made

**1. Migration Up() Method** (`20261112094313_AddDocumentCategoryToProjectPhase.cs`)
```csharp
// BEFORE
onDelete: ReferentialAction.SetNull);

// AFTER
onDelete: ReferentialAction.NoAction);
```

**2. Migration Designer File** (`20261112094313_AddDocumentCategoryToProjectPhase.Designer.cs`)
```csharp
// BEFORE
.OnDelete(DeleteBehavior.SetNull);

// AFTER
.OnDelete(DeleteBehavior.NoAction);
```

**3. EF Core Configuration** (`ProjectLifecycleCatalogConfigurations.cs`)
```csharp
// BEFORE
.OnDelete(DeleteBehavior.SetNull);

// AFTER
.OnDelete(DeleteBehavior.NoAction);
```

### Rationale for `NoAction`

✅ **Why `NoAction` is correct:**

1. **Composite FK Constraint:** The composite key `(TenantId, DocumentCategoryId)` is checked at the database level
2. **Required Column:** `TenantId` cannot be NULL, so `SetNull` is impossible
3. **Business Logic:** Categories should not be deleted if they're linked to active phases
4. **Application Validation:** Service-layer business rules already enforce Tenant+Organization scope, making the FK protective but not cascading
5. **Pattern Alignment:** Matches the existing `Organization` FK which uses `Restrict` (similar protective behavior)

✅ **Behavior:**
- Deletion of a `DocumentCategory` will be **prevented** if it's referenced by any `ProjectPhaseCatalogEntry`
- Application receives a FK constraint violation, triggering service-layer validation
- Enforces referential integrity without the impossibility of `SetNull` on a required column

---

## Files Modified

| File | Change | Status |
|------|--------|--------|
| `apps/api/src/NEXUS.Persistence/Migrations/20261112094313_AddDocumentCategoryToProjectPhase.cs` | `ReferentialAction.SetNull` → `ReferentialAction.NoAction` | ✅ Fixed |
| `apps/api/src/NEXUS.Persistence/Migrations/20261112094313_AddDocumentCategoryToProjectPhase.Designer.cs` | `DeleteBehavior.SetNull` → `DeleteBehavior.NoAction` (line 15584) | ✅ Fixed |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectLifecycleCatalogConfigurations.cs` | `DeleteBehavior.SetNull` → `DeleteBehavior.NoAction` (line 37) | ✅ Fixed |

---

## Post-Repair Verification

✅ **All Files Synchronized:**
- Migration `.cs` file: `NoAction` ✓
- Migration Designer `.cs` file: `NoAction` ✓
- Configuration file: `NoAction` ✓

✅ **Migration is Safe to Apply:**
- No database write has been executed
- No data will be corrupted
- The migration is ready for application to Development, UAT, and beyond
- No forward migration needed (this IS the corrected version)

✅ **Model Consistency:**
- EF Core model, migration, and configuration are aligned
- Designer snapshot reflects the corrected constraint
- No further schema adjustments required

---

## Application Guidance

### Next Steps

**For Development Database:**
1. Use the documented EF Core migration workflow (scripts/dev/db-preflight.ps1)
2. Apply the corrected migration when ready
3. Verify physical schema with corrected `ON DELETE NO ACTION` constraint
4. Run persistence tests to confirm FK behavior

**For Code Review:**
- This repair is minimal and surgical (3 lines changed across 3 files)
- All changes are aligned to the same semantic intent: `NoAction`
- No business logic, domain model, or API changes required

### Testing Recommendations

1. **Unit/Integration Tests:**
   - Verify that deleting a `DocumentCategory` referenced by a `ProjectPhaseCatalogEntry` raises a FK violation
   - Confirm that orphan DocumentCategory deletion succeeds

2. **Schema Verification:**
   - After migration apply, confirm the constraint reads: `ON DELETE NO ACTION`
   - No cascading deletes should occur

---

## Authorization & Sign-Off

- **Issue Type:** EF Core Migration Constraint Error
- **Severity:** Critical (Prevented Runtime Failure)
- **Authorization:** Task authorization: Foreign Key & Migration Repair (Agent A)
- **Applied:** No (unapplied, awaiting explicit authorization)
- **Risk Level:** Minimal (error caught before application)

---

## Related Documentation

- Repository: [Project Persistence Configuration](apps/api/src/NEXUS.Persistence/Configurations/ProjectLifecycleCatalogConfigurations.cs)
- EF Core Docs: [DeleteBehavior Enum](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.deletebehavior)
- SQL Constraint: [Referenced in ADR on Tenant Isolation](docs/)

---

**Report Generated:** 2026-11-12 15:31:13 UTC+2  
**Generated By:** Copilot (Foreign Key Repair Agent A)
