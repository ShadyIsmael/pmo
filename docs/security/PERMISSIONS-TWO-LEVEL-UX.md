# Roles & Permissions: Current Two-Level Behavior

**Status:** Current source contract, 2026-11-09. This document describes the implemented screen/feature grouping and ordinary grant rules.

## Permission hierarchy and labels

The editor presents the canonical catalog as:

1. **Permission Group** — the actual React screen, tab, or recognizable feature, sourced from the catalog screen label.
2. **Subpermissions** — one View row and the supported action rows for that screen or feature.

The editor does not render Module as an outer group, and it does not create a second permission catalog. Module labels classify entries for the module filter and search. Each canonical key appears once in its group, even when it is shown as dependency context in search results.

View and Action are filters and row types, not additional hierarchy levels. Selecting an action also selects its declared prerequisites. Removing a required View asks before removing dependent actions. The API remains authoritative for dependencies, delegation, scope, and permission enforcement.

Permission screen labels and action labels come from PermissionCatalog. Search includes canonical keys, resource/action names, English and Arabic labels, group/module labels, descriptions, and prerequisite keys. The module, level, grant-status, and sensitivity filters remain available. Each group can expand/collapse and select/deselect its currently eligible rows. A selected action displays its prerequisites and scope.

## Ordinary permission grants and role categories

Role Category is descriptive metadata for custom roles. An administrator authorized through `Roles.ConfigurePermissions` may grant or revoke any active, assignable, scoped non-Administration permission to a same-Tenant user-managed custom role, regardless of target category or whether the actor holds that permission. This supersedes the earlier actor-possession ceiling for ordinary custom-role grants.

The backend continues to enforce role-administration authority, tenant and role ownership, permission dependencies, resource scope, Active Deny, and protected-role rules. Administration-module permissions remain actor-authorized only. The protected Admin exception remains limited to its exact 13-key set; other system-managed roles do not receive bootstrap-only grants. A role assigned to the actor cannot be changed to add an effective permission beyond the permissions currently effective for the actor. No permission is granted automatically.

Category-based eligibility is temporarily suspended for ordinary permission grants. The earlier actor-possession ceiling has been superseded for ordinary non-Administration permissions on user-managed custom roles; it is not a prerequisite for an authorized `Roles.ConfigurePermissions` holder to grant those permissions.

## Restrictions that remain

- Custom role categories must be selectable values; System is reserved for system roles. System-role identity/category protections remain in place.
- A role administrator may grant ordinary active, assignable, scoped non-Administration permissions to a user-managed custom role without personally holding each permission. Administration permissions remain actor-authorized only; platform/global permissions and protected/system-managed roles remain restricted.
- Active Deny overrides Allow. Tenant, Organization, Project, resource, workflow assignment, and separation-of-duties checks remain enforced by the relevant API/service.
- Protected tenant Admin permissions remain locked and cannot be removed or denied. Last-administrator protection remains active.
- Platform, CrossTenant, and SystemOwner permissions remain outside ordinary tenant role grants and require the appropriate platform authority.
- Some business eligibility rules can still use Role Category independently of permission grant eligibility. Examples include Management-role eligibility for manager assignment and System/Management reviewer eligibility in the Annual Budget approval workflow.

## Display-name mapping

The complete catalog mapping is recorded in PERMISSION-DISPLAY-NAME-MAPPING.csv. It contains 174 current entries. Keys that have no repository HEAD baseline are marked unavailable in the prior-label columns rather than assigned an invented old label.

| Previous group / action | Current group / subpermission, EN | Current group / subpermission, AR | Example key |
|---|---|---|---|
| Roles and permissions / Configure Role Permissions | Roles & Permissions / Edit permissions | الأدوار والصلاحيات / تعديل الصلاحيات | Roles.ConfigurePermissions |
| Planning / Create project plans | Project Plan / Create plan | خطة المشروع / إنشاء خطة | ProjectPlan.Create |
| Annual Budgets / Configure Draft Approval | Annual Budgets / Configure approval | الموازنات السنوية / تهيئة الموافقة | ProjectAnnualBudget.ConfigureDraftApproval |
| Annual Budgets / Change Annual Budget Approval Defaults | Annual Budget Approval Defaults / Set approval defaults | الإعدادات الافتراضية لاعتماد الموازنة السنوية / ضبط إعدادات الاعتماد الافتراضية | ProjectAnnualBudget.ManageApprovalConfiguration |
| Payment Requests / Record | Settlements & Reversals / Record settlement | التسويات وعمليات العكس / تسجيل التسوية | PaymentSettlement.Record |
| Payment Requests / Request reversal | Settlements & Reversals / Request reversal | التسويات وعمليات العكس / طلب عكس الالتزام | ProjectPaymentReversal.Create |
| Risk management / Risks – Delete | Risks / Delete | المخاطر / حذف | Risk.Delete |
| Project status reports / Project status reports – Export | Project Status Reports / Export | تقارير حالة المشروع / تصدير | StatusReport.Export |
| Portfolio Financial Rollups / View | Portfolio Financials / View | ماليات المحفظة / عرض | PortfolioFinancialRollup.View |
| Program Financial Rollups / View | Program Financials / View | ماليات البرنامج / عرض | ProgramFinancialRollup.View |
| Home / Dashboards / View Dashboards | Home / View | الرئيسية / عرض | Dashboard.View |
| Workflow administration / Workflows – Publish | Workflow Management / Publish | إدارة سير العمل / نشر | Workflow.Publish |
| Purchase commitments / Create commitments | Commitments / Create | الالتزامات / إنشاء | Commitment.Create |
| Tenant and organization settings / Settings – Read | Tenant Settings / View | إعدادات المستأجر / عرض | Settings.View |

Module filter names also use React navigation labels: Capacity becomes Resources; Project Delivery and Project Management become Delivery Management; Reporting becomes Reports & Insights. Screen/feature names use the React page or tab where it exists. Sensitive or ambiguous operations retain scope explanations in their catalog descriptions.

## Preservation

Display metadata changes do not rename canonical permission keys or seed IDs, merge permissions, change permission scopes or dependencies, alter role grants, or write to the database. Catalog grouping is presentational; authorization continues to use canonical keys and server-side scope checks.
