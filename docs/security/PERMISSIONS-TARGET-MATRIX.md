# Permission Matrix - Phase 02 Proposal and Phase 03 Implementation Snapshot

**Status: Product Owner authorized implementation through the P0 task.** The Phase 02 proposal below is retained for historical traceability. The current code-defined catalog and implementation status are recorded in [the final catalog](PERMISSIONS-FINAL-CATALOG.md), [the implementation report](PERMISSIONS-IMPLEMENTATION-REPORT.md), and [the final machine-readable matrix](PERMISSIONS-FINAL-TARGET-MATRIX.csv). Persisted runtime grants and deployment state remain unverified; no database migration was applied.

Companion documents: [Rationalization decisions](PERMISSIONS-RATIONALIZATION-DECISIONS.md), [Remediation plan](PERMISSIONS-REMEDIATION-PLAN.md), [UX specification](PERMISSIONS-TWO-LEVEL-UX.md), [Migration strategy](PERMISSIONS-MIGRATION-STRATEGY.md), [Product Owner decision register](PERMISSIONS-PO-DECISIONS.md), and [machine-readable matrix](PERMISSIONS-TARGET-MATRIX.csv).

Phase 01 inspected 113 route declarations; these are not 113 distinct screens. The 103-key baseline, role-seed/runtime distinction, and current enforcement evidence are carried forward from the existing inventory. The Phase 01 route census grouped them into 24 screen families; the target matrix decomposes these into 34 permission screen groupings as distinct View/action boundaries are separated.

## Phase 03 current source snapshot

| Measure | Current code catalog |
|---|---:|
| Definitions, including retained inactive compatibility keys | 143 |
| Active View permissions | 31 |
| Active Action permissions | 94 |
| Active permissions | 125 |
| Inactive/deprecated compatibility definitions | 18 |
| New active definitions beyond the Phase 01 baseline | 40 |

These are counts from `PermissionCatalog.Entries`, not counts of database rows, role grants, users, routes, or screens. The final CSV contains one row per code-defined catalog key. The seeded role map is source evidence only; persisted `RolePermissions`, direct overrides, effective runtime permissions, JWT snapshots, and caches were not inspected or changed.

The P0 business clarification is implemented in source: Project Edit rejects lifetime ceiling changes. Only final approval of an assigned Change Request that contains a ceiling change can apply it, and that decision requires both `ChangeRequest.Approve` and `ProjectFinancialCeiling.ApproveChange`, plus workflow assignment, scope, separation-of-duties, financial validation, and audit.

The final matrix supersedes the provisional Phase 02 counts and labels below. Keep the historical proposal below as the record of the earlier design review.

## Phase 02 baseline and proposed counts (historical; superseded by Phase 03)

The tables and proposal text in the remainder of this section record the earlier Phase 02 review. They are not the active target. In particular, any historical wording that says cross-Portfolio creation still requires `Program.Manage` is superseded: the implemented rule requires `Program.Create`, `Program.CreateAcrossPortfolios`, `Portfolio.View`, and eligible Tenant/Organization scope. Use the current catalog and final CSV above as the canonical Phase 03 matrix.

| Measure | Current Phase 01 | Proposed active target after compatibility retirement |
|---|---:|---:|
| Current unique seeded keys | 103 | — |
| Current View/Read | 27 | 28 |
| Current Action | 76 | 88 |
| Proposed additions | — | 30 (4 View + 26 Action) |
| Proposed key retirements/merges after verified transition | — | 17 |
| Proposed active target keys | — | 116 |

Target count assumes 17 existing keys retire only after verified compatibility work: three legacy View aliases; six orphan action candidates other than the retained reports.read; four umbrella aliases (Resource.Manage, Team.Manage, Directory.Manage, Planning.Manage); users.write and settings.write; and Program.Manage plus Portfolio.Manage after splitting them into eight operation keys. projects.delete is included in the six legacy action candidates and maps to Project.Delete only after review. finances.write remains for residual financial write consumers until every caller is mapped; it is not retired in this count. Meeting.Manage remains because its current purpose is post-meeting minutes/comments/work-item links, not meeting CRUD. Counts are proposed, not approved. The transition catalog could contain 133 entries (103 current + 30 proposed additions) before 17 old entries retire. The conditional Plugins.View key is excluded; if approved as tenant-facing, add one more View key (29 Views, 88 Actions, 117 active).

## Target hierarchy and permission contract

**Module → Screen/workspace → Level 1 View → Level 2 Actions.** Each configured key remains an independently enforced capability. Tenant, organization, native management assignments, resource scope, workflow assignment, and effective read-only context remain separate checks.

Recommended dependency: saving an Action also explicitly saves that screen's View grant. The UI selects View automatically and shows it as a dependency. Removing View removes dependent Action grants in the unsaved draft and requires confirmation before save. The API rejects inconsistent combinations. Platform policies never appear as tenant permissions.

For assigned reviewers, screen View grants provide entry to the workflow workspace, while review list/detail APIs return only records assigned to that reviewer unless a separately approved read capability permits a broader list. An action key alone never implies broad list access. Direct URL navigation and every read/write API receive matching authorization.

## Recommended functional taxonomy

- **Administration:** Users and access; Roles and Permissions; Tenant/Organization Settings; Reference Data; Directory Integration; Governance and Assignment Recovery; Audit Trail.
- **Portfolio & Program Management:** Portfolios; Programs; Project Settings; Administrative Assignments and Handovers.
- **Project Delivery:** Projects; Planning and Work Items; Change Requests; Risks and Issues; Meetings; Documents; Project Status Reports; Stakeholders; Workflows; Time Tracking.
- **Capacity:** Resources, Teams, Resource Rates, and Capacity.
- **Financial Management:** Financial Overview and Planned Payments; Annual Budgets; Payment Requests and Approval History; Settlements and Reversals; Fiscal Year Closing.
- **Reporting:** Reports and Dashboards.
- **Platform administration:** Platform Settings/Tenant Registry remain governed by platform policies and are not tenant Role & Permissions entries.

## Proposed new keys (30)

All labels/descriptions below are proposals and remain subject to PO approval. The CSV contains current labels, descriptions, enforcement evidence, scopes, source-seeded role maps, compatibility actions, proposed audit requirements, and evidence.

| Level | Screen | Proposed key | Proposed EN | Proposed AR | Description |
|---|---|---|---|---|---|
| View | Annual Budgets | ProjectAnnualBudget.View | View Annual Budgets | عرض الميزانيات السنوية | Open the annual-budget workspace within tenant and project scope. Review-only users see assigned records only. / فتح مساحة الميزانيات السنوية ضمن نطاق المستأجر والمشروع. يقتصر عرض المراجعين على السجلات المُسندة إليهم. |
| View | Payment Requests | ProjectPaymentRequest.View | View Payment Requests | عرض طلبات الدفع | View requests and approval history in tenant/project scope; reviewer-only lists are assignment-filtered. / عرض الطلبات وسجل اعتمادها ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد. |
| View | Fiscal Year Closing | ProjectAnnualBudgetClosing.View | View Fiscal Year Closing | عرض إقفالات السنة المالية | View closings in tenant/project scope; reviewer-only lists are assignment-filtered. / عرض الإقفالات ضمن نطاق المستأجر والمشروع؛ وتُصفى قوائم المراجعين حسب الإسناد. |
| View | Home / Dashboards | Dashboard.View | View Dashboards | عرض لوحات المعلومات | Open the dashboard workspace. Each widget data API continues to enforce its own permission and tenant/organization scope. / فتح مساحة لوحات المعلومات، مع استمرار كل واجهة بيانات لعنصر لوحة بعينه في تطبيق صلاحياتها ونطاق المستأجر والمؤسسة. |
| Action | Annual Budgets | ProjectAnnualBudget.Create | Create Annual Budget | إنشاء ميزانية سنوية | Create an annual-budget draft within project and fiscal-year scope. / إنشاء مسودة ميزانية سنوية ضمن نطاق المشروع والسنة المالية. |
| Action | Annual Budgets | ProjectAnnualBudget.EditDraft | Edit Draft Budget | تعديل مسودة الميزانية | Edit an eligible draft before submission. / تعديل مسودة مستوفية للشروط قبل إرسالها. |
| Action | Annual Budgets | ProjectAnnualBudget.SubmitForReview | Submit for Review | إرسال للمراجعة | Submit an eligible draft to its configured review workflow. / إرسال مسودة مستوفية للشروط إلى مسار المراجعة المحدد. |
| Action | Annual Budgets | ProjectAnnualBudget.ReviseApproved | Revise Approved Budget | تعديل الميزانية المعتمدة | Submit an eligible revision to an approved budget. / إرسال تعديل مستوفٍ للشروط على ميزانية معتمدة. |
| Action | Annual Budgets | ProjectAnnualBudget.ManageApprovalConfiguration | Manage Approval Configuration | إدارة إعدادات مسار الاعتماد | Change persistent Project Settings approval defaults; distinct from a one-submission override. / تغيير إعدادات الاعتماد الافتراضية الدائمة للمشروع، بمعزل عن تجاوز إرسال واحد. |
| Action | Payment Requests | ProjectPaymentRequest.Create | Create Payment Request | إنشاء طلب دفع | Create a request draft within project scope and currency rules. / إنشاء مسودة طلب ضمن نطاق المشروع وقواعد العملة. |
| Action | Payment Requests | ProjectPaymentRequest.EditDraft | Edit Draft Payment Request | تعديل مسودة طلب دفع | Edit an eligible request draft before submission. / تعديل مسودة طلب مستوفية للشروط قبل إرسالها. |
| Action | Payment Requests | ProjectPaymentRequest.Submit | Submit Payment Request | إرسال طلب دفع | Submit an eligible request to its approval workflow. / إرسال طلب مستوفٍ للشروط إلى مسار الاعتماد. |
| Action | Payment Requests | ProjectPaymentRequest.Cancel | Cancel Payment Request | إلغاء طلب دفع | Cancel an eligible request under workflow-state rules; distinct from reversing settlement. / إلغاء طلب مستوفٍ للشروط وفق حالته في سير العمل؛ وهو مستقل عن عكس التسوية. |
| Action | Payment Requests | ProjectPaymentRequest.Approve | Approve or Reject Payment Request | اعتماد أو رفض طلب دفع | Decide only an assigned request; active team membership, project scope, exact reviewer assignment, and separation of duties remain required. / اتخاذ قرار في طلب مُسند فقط؛ مع استمرار اشتراط عضوية الفريق ونطاق المشروع والإسناد الدقيق والفصل بين المهام. |
| Action | Programs | Program.CreateAcrossPortfolios | Create Programs Across Portfolios | إنشاء برامج عبر المحافظ | Allow creation outside native Portfolio assignment only within eligible tenant and organization scope; still requires Program.Manage. / السماح بالإنشاء خارج إسناد المحفظة الأصلي داخل نطاق المستأجر والمؤسسة المؤهل، مع استمرار اشتراط Program.Manage. |
| Action | Users and access | Users.ManageAccounts | Manage User Accounts | إدارة حسابات المستخدمين | Create/update/activate/deactivate users; excludes role assignment, permission overrides, and password reset. / إنشاء حسابات المستخدمين أو تعديلها أو تفعيلها أو إيقافها، دون إسناد الأدوار أو تجاوز الصلاحيات أو إعادة تعيين كلمة المرور. |
| Action | Roles and permissions | Roles.Manage | Manage Roles | إدارة الأدوار | Create/update/delete tenant roles and configure permission sets within actor delegation limits. / إنشاء أدوار المستأجر وتعديلها أو حذفها وتهيئة صلاحياتها ضمن حدود تفويض القائم بالتعديل. |
| Action | Users and access | Users.AssignRoles | Assign Roles to Users | إسناد الأدوار للمستخدمين | Assign/remove tenant roles; changes effective access and require audit. / إسناد أدوار المستأجر أو إزالتها؛ وهي تغييرات تؤثر على الوصول الفعلي وتتطلب التدقيق. |
| Action | Users and access | UserPermissions.Manage | Manage User Permission Overrides | إدارة تجاوزات الصلاحيات المباشرة | Add/remove per-user Allow/Deny overrides with audit and delegation limits. / إضافة أو إزالة تجاوزات السماح أو المنع للمستخدم مع التدقيق وحدود التفويض. |
| Action | Tenant and organization settings | Settings.ManageTenantConfiguration | Manage Tenant Settings | إدارة إعدادات المستأجر | Update tenant settings permitted by typed definitions; platform settings remain separate. / تحديث إعدادات المستأجر المسموح بها وفق التعريفات؛ وتظل إعدادات المنصة منفصلة. |
| Action | Reference data | ReferenceData.Manage | Manage Reference Data | إدارة البيانات المرجعية | Create/update tenant reference data where currently implemented. / إنشاء البيانات المرجعية للمستأجر أو تعديلها حيث توجد هذه العمليات. |
| Action | Programs | Program.Create | Create Program | إنشاء برنامج | Create a Program under an eligible Portfolio. Native assignment is required unless the explicit cross-Portfolio capability is also granted. / إنشاء برنامج تحت محفظة مؤهلة. يلزم إسناد المحفظة ما لم تُمنح صلاحية الإنشاء عبر المحافظ صراحةً. |
| Action | Programs | Program.Edit | Edit Program | تعديل برنامج | Edit an existing Program within native management scope. / تعديل برنامج قائم ضمن نطاق الإدارة الأصلي. |
| Action | Programs | Program.Archive | Archive Program | أرشفة برنامج | Archive a Program within native management scope. / أرشفة برنامج ضمن نطاق الإدارة الأصلي. |
| Action | Programs | Program.Reactivate | Reactivate Program | إعادة تفعيل برنامج | Reactivate an archived Program within native management scope. / إعادة تفعيل برنامج مؤرشف ضمن نطاق الإدارة الأصلي. |
| Action | Portfolios | Portfolio.Create | Create Portfolio | إنشاء محفظة | Create a Portfolio within tenant and organization authority. / إنشاء محفظة ضمن صلاحيات المستأجر والمؤسسة. |
| Action | Portfolios | Portfolio.Edit | Edit Portfolio | تعديل محفظة | Edit a Portfolio within tenant and organization authority. / تعديل محفظة ضمن صلاحيات المستأجر والمؤسسة. |
| Action | Portfolios | Portfolio.Archive | Archive Portfolio | أرشفة محفظة | Archive a Portfolio subject to tenant, organization, and lifecycle checks. / أرشفة محفظة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة. |
| Action | Portfolios | Portfolio.Reactivate | Reactivate Portfolio | إعادة تفعيل محفظة | Reactivate an archived Portfolio subject to tenant, organization, and lifecycle checks. / إعادة تفعيل محفظة مؤرشفة مع الالتزام بنطاق المستأجر والمؤسسة وفحوص دورة الحياة. |
| Action | Change Requests | ProjectFinancialCeiling.ApproveChange | Approve Project Ceiling Change | اعتماد تعديل سقف المشروع | Qualify approval of a change request that alters the approved lifetime ceiling. Also requires the normal ChangeRequest.Approve decision capability, exact workflow assignment, project scope, and separation of duties. / تقييد اعتماد طلب تغيير يبدل السقف المالي المعتمد طوال عمر المشروع. يتطلب أيضًا صلاحية قرار ChangeRequest.Approve والإسناد الدقيق في سير العمل ونطاق المشروع والفصل بين المهام. |
Plugins.View is a conditional missing View item, excluded from the 30-key count. The PO must decide whether Plugins is tenant-facing (add a tenant-scoped View key and align route/API checks) or a platform operator surface (use a platform policy and keep it out of tenant roles).
## Existing-key dispositions

The following table includes every current key plus each proposed addition. Current EN/AR names, descriptions, enforcement, source-seeded role maps, scope, risk, compatibility action, audit requirement, and source evidence are fully recorded in the companion CSV. Seeded role mappings are source-code associations only; persisted grants and effective user permissions remain NOT VERIFIED.

| Module | Screen | Level | Existing key | Proposed key | Proposed EN | Proposed AR | Disposition | Risk |
|---|---|---|---|---|---|---|---|---|
| Security | Audit trail | View | Audit.View | Audit.View | View audit trail | عرض سجل التدقيق | Relabel | Low |
| Delivery | Change requests | Action | ChangeRequest.Approve | ChangeRequest.Approve | Change request · Approve or reject | طلب التغيير · اعتماد أو رفض | Keep | Medium |
| Delivery | Change requests | Action | ChangeRequest.Manage | ChangeRequest.Manage | Create, update, and cancel change requests | إنشاء طلبات التغيير وتعديلها وإلغاؤها | Keep | Medium |
| Delivery | Change requests | View | ChangeRequest.View | ChangeRequest.View | Change requests · View | طلبات التغيير · عرض | Keep | Low |
| Administration | Directory integration | Action | Directory.ExecuteSync | Directory.ExecuteSync | Run directory synchronization | تنفيذ مزامنة الدليل | Relabel | High |
| Administration | Directory integration | Action | Directory.Manage | Retire after compatibility review | Full directory administration (legacy umbrella) | إدارة شاملة للدليل (صلاحية عامة قديمة) | Deprecate | Low |
| Administration | Directory integration | Action | Directory.ManageMappings | Directory.ManageMappings | Manage directory mappings | إدارة تعيينات الدليل | Relabel | Low |
| Administration | Directory integration | Action | Directory.ManageProvisioningPolicy | Directory.ManageProvisioningPolicy | Manage provisioning policy | إدارة سياسة إنشاء الحسابات | Relabel | High |
| Administration | Directory integration | Action | Directory.PreviewSync | Directory.PreviewSync | Preview directory synchronization | معاينة مزامنة الدليل | Relabel | Low |
| Administration | Directory integration | Action | Directory.TestConnection | Directory.TestConnection | Test directory connection | اختبار اتصال الدليل | Relabel | Low |
| Administration | Directory integration | View | Directory.View | Directory.View | Directory integration · View | تكامل الدليل · عرض | Relabel | Low |
| Delivery | Documents | Action | Document.Archive | Document.Archive | Documents · Archive | المستندات · أرشفة | Keep | Low |
| Delivery | Documents | Action | Document.Create | Document.Create | Documents · Create | المستندات · إنشاء | Keep | Low |
| Delivery | Documents | Action | Document.Edit | Document.Edit | Documents · Edit | المستندات · تعديل | Keep | Low |
| Delivery | Documents | Action | Document.TemplateManage | Document.TemplateManage | Manage document templates | إدارة قوالب المستندات | Keep | Low |
| Delivery | Documents | View | Document.View | Document.View | Documents · View | المستندات · عرض | Keep | Low |
| Financial Management | Financial overview and planned payments | View | finances.read | finances.read | View project financial information | عرض المعلومات المالية للمشروعات | Keep | Low |
| Financial Management | Legacy financial write umbrella | Action | finances.write | Split into proposed keys | Edit financial records | تعديل السجلات المالية | Split | High |
| Governance | Assignment recovery | Action | Governance.AssignmentRecovery.Initiate | Governance.AssignmentRecovery.Initiate | Governance assignment recovery · Initiate | استعادة تعيينات الحوكمة · بدء | Relabel | Medium |
| Governance | Assignment recovery | Action | Governance.AssignmentRecovery.Manage | Governance.AssignmentRecovery.Manage | Execute assignment recovery | تنفيذ استعادة الإسناد | Keep | Medium |
| Governance | Assignment recovery | View | Governance.AssignmentRecovery.View | Governance.AssignmentRecovery.View | Governance assignment recovery · View | استعادة تعيينات الحوكمة · عرض | Relabel | Low |
| Delivery | Handovers | Action | Handover.AdminInitiate | Handover.AdminInitiate | Handovers · Initiate handovers administratively | التسليمات · بدء التسليمات إدارياً | Keep | Medium |
| Delivery | Handovers | Action | Handover.Approve | Handover.Approve | Handover · Accept or reject | تسليم المسؤولية · قبول أو رفض | Keep | Medium |
| Delivery | Handovers | Action | Handover.Manage | Handover.Manage | Handovers · Manage | التسليمات · إدارة | Keep | Medium |
| Delivery | Handovers | View | Handover.View | Handover.View | Handovers · View | التسليمات · عرض | Keep | Low |
| Governance | Administrative Assignments and Handovers | Action | manager.handover | Retire after compatibility review | Manager handover (legacy; semantics unresolved) | تسليم المدير (صلاحية قديمة؛ المعنى بحاجة للتحقق) | Deprecate | Low |
| Delivery | Meetings | Action | Meeting.Create | Meeting.Create | Meetings · Create | الاجتماعات · إنشاء | Keep | Low |
| Delivery | Meetings | Action | Meeting.Delete | Meeting.Delete | Meetings · Delete | الاجتماعات · حذف | Relabel | High |
| Delivery | Meetings | Action | Meeting.Edit | Meeting.Edit | Meetings · Edit | الاجتماعات · تعديل | Keep | Low |
| Delivery | Meetings | Action | Meeting.Manage | Meeting.Manage | Manage meeting minutes, comments, and work item links | إدارة محاضر الاجتماعات والتعليقات وروابط عناصر العمل | Keep | Low |
| Delivery | Meetings | View | Meeting.View | Meeting.View | Meetings · View | الاجتماعات · عرض | Keep | Low |
| Financial Management | Payment Requests | Action | PaymentSettlement.Record | PaymentSettlement.Record | Record payment settlement | تسجيل تسوية دفعة | Keep | High |
| Delivery | Planning | Action | Planning.Lifecycle | Planning.Lifecycle | Planning · Manage lifecycle | التخطيط · إدارة دورة الحياة | Keep | Low |
| Delivery | Planning | Action | Planning.Manage | Retire after compatibility review | Manage project planning data | إدارة بيانات تخطيط المشروع | Deprecate | Medium |
| Delivery | Planning | View | Planning.View | Planning.View | Planning · View | التخطيط · عرض | Keep | Low |
| Delivery | Portfolios | Action | Portfolio.Manage | Split into proposed keys | Create and maintain Portfolios | إنشاء المحافظ وتحديثها | Split | Medium |
| Delivery | Portfolios | View | Portfolio.View | Portfolio.View | Portfolios · View | المحافظ · عرض | Keep | Low |
| Delivery | Portfolios | View | portfolios.read | Portfolio.View | View portfolios (legacy alias) | عرض المحافظ (صلاحية قديمة) | Merge | Low |
| Delivery | Portfolios | Action | portfolios.write | Retire after compatibility review | Edit portfolios (legacy permission) | تعديل المحافظ (صلاحية قديمة) | Deprecate | Low |
| Delivery | Programs | Action | Program.Manage | Split into proposed keys | Create and maintain Programs within assigned Portfolio scope | إنشاء البرامج وتحديثها ضمن نطاق المحفظة المُسندة | Split | Medium |
| Delivery | Programs | View | Program.View | Program.View | Programs · View | البرامج · عرض | Keep | Low |
| Delivery | Projects | Action | Project.Create | Project.Create | Projects · Create | المشروعات · إنشاء | Keep | Low |
| Delivery | Projects | Action | Project.Delete | Project.Delete | Projects · Delete | المشروعات · حذف | Relabel | Medium |
| Delivery | Projects | Action | Project.Edit | Project.Edit | Projects · Edit | المشروعات · تعديل | Keep | Low |
| Delivery | Projects | View | Project.View | Project.View | Projects · View | المشروعات · عرض | Keep | Low |
| Financial Management | Annual Budgets | Action | ProjectAnnualBudget.Approve | ProjectAnnualBudget.Approve | Review annual budget: approve or return for changes | مراجعة الميزانية السنوية: اعتماد أو إعادة للتعديل | Keep | High |
| Financial Management | Fiscal Year Closing | Action | ProjectAnnualBudget.CarryForward | ProjectAnnualBudget.CarryForward | Authorize annual budget carry-forward | اعتماد ترحيل رصيد الميزانية السنوية | Keep | High |
| Financial Management | Fiscal Year Closing | Action | ProjectAnnualBudget.Close | ProjectAnnualBudget.Close | Prepare fiscal year closing | إعداد إقفال السنة المالية | Keep | Medium |
| Financial Management | Fiscal Year Closing | Action | ProjectAnnualBudget.CloseApprove | ProjectAnnualBudget.CloseApprove | Approve or reject fiscal year closing | اعتماد أو رفض إقفال السنة المالية | Keep | High |
| Financial Management | Annual Budgets | Action | ProjectApproval.Override | ProjectApproval.Override | Override approval settings for one submission | تجاوز إعدادات الاعتماد لإرسال واحد | Keep | High |
| Financial Management | Payment Requests | Action | ProjectPaymentReversal.Approve | ProjectPaymentReversal.Approve | Approve or reject payment reversal | اعتماد أو رفض عكس دفعة | Keep | High |
| Financial Management | Payment Requests | Action | ProjectPaymentReversal.Create | ProjectPaymentReversal.Create | Create payment reversal request | إنشاء طلب عكس دفعة | Keep | High |
| Delivery | Projects | Action | projects.delete | Project.Delete | Delete projects (legacy alias) | حذف المشروعات (صلاحية قديمة) | Merge | Low |
| Delivery | Projects | View | projects.read | Project.View | View projects (legacy alias) | عرض المشروعات (صلاحية قديمة) | Merge | Low |
| Delivery | Projects | Action | projects.write | Retire after compatibility review | Edit projects (legacy permission) | تعديل المشروعات (صلاحية قديمة) | Deprecate | Low |
| Reporting | Reports | Action | reports.export | Retire after compatibility review | Export reports (legacy; no active endpoint found) | تصدير التقارير (صلاحية قديمة؛ لا توجد نقطة نهاية نشطة معروفة) | Deprecate | Low |
| Reporting | Reports | View | reports.read | reports.read | View reports | عرض التقارير | Keep | Low |
| Capacity | Resources | Action | Resource.Allocate | Resource.Allocate | Resources · Allocate | الموارد · تخصيص | Keep | Low |
| Capacity | Resources | Action | Resource.Assign | Resource.Assign | Resources · Assign | الموارد · إسناد | Keep | Low |
| Capacity | Resources | Action | Resource.Create | Resource.Create | Resources · Create | الموارد · إنشاء | Keep | Low |
| Capacity | Resources | Action | Resource.Delete | Resource.Delete | Deactivate or delete resources and vendors | تعطيل الموارد والموردين أو حذفهم | Relabel | Medium |
| Capacity | Resources | Action | Resource.Edit | Resource.Edit | Resources · Edit | الموارد · تعديل | Keep | Low |
| Capacity | Resources | Action | Resource.EditCostRate | Resource.EditCostRate | Edit resource cost rates | تعديل تكاليف الموارد | Keep | High |
| Capacity | Resources | Action | Resource.Manage | Retire after compatibility review | Full resource administration (legacy umbrella) | إدارة شاملة للموارد (صلاحية عامة قديمة) | Deprecate | Medium |
| Capacity | Resources | Action | Resource.ManageRates | Resource.ManageRates | Resources · Manage rates | الموارد · إدارة الأسعار | Keep | Medium |
| Capacity | Resources | View | Resource.View | Resource.View | Resources · View | الموارد · عرض | Keep | Low |
| Capacity | Resources | Action | Resource.ViewCostRate | Resource.ViewCostRate | View resource cost rates | عرض تكاليف الموارد | Keep | Low |
| Capacity | Resources | View | resources.read | Resource.View | View resources (legacy alias) | عرض الموارد (صلاحية قديمة) | Merge | Low |
| Capacity | Resources | Action | resources.write | Retire after compatibility review | Edit resources (legacy permission) | تعديل الموارد (صلاحية قديمة) | Deprecate | Low |
| Delivery | Risk management | Action | Risk.Create | Risk.Create | Risks · Create | المخاطر · إنشاء | Keep | Low |
| Delivery | Risk management | Action | Risk.Delete | Risk.Delete | Risks · Delete | المخاطر · حذف | Relabel | Medium |
| Delivery | Risk management | Action | Risk.Edit | Risk.Edit | Risks · Edit | المخاطر · تعديل | Keep | Low |
| Delivery | Risk management | Action | Risk.Mitigate | Risk.Mitigate | Risks · Mitigate | المخاطر · تخفيف المخاطر | Keep | Low |
| Delivery | Risk management | View | Risk.View | Risk.View | Risks · View | المخاطر · عرض | Keep | Low |
| Administration | Tenant and organization settings | View | settings.read | settings.read | Settings · Read | الإعدادات · قراءة | Keep | Low |
| Administration | Tenant and organization settings | Action | settings.write | Split into proposed keys | Edit tenant settings and reference data (legacy umbrella) | تعديل إعدادات المستأجر والبيانات المرجعية (صلاحية عامة قديمة) | Split | High |
| Delivery | Project stakeholders | Action | Stakeholders.Manage | Stakeholders.Manage | Stakeholders · Manage | أصحاب المصلحة · إدارة | Keep | Medium |
| Delivery | Project stakeholders | View | Stakeholders.Read | Stakeholders.Read | Stakeholders · Read | أصحاب المصلحة · قراءة | Keep | Low |
| Delivery | Project status reports | Action | StatusReport.Create | StatusReport.Create | Project status reports · Create | تقارير حالة المشروع · إنشاء | Relabel | Low |
| Delivery | Project status reports | Action | StatusReport.Edit | StatusReport.Edit | Project status reports · Edit | تقارير حالة المشروع · تعديل | Relabel | Low |
| Delivery | Project status reports | Action | StatusReport.Export | StatusReport.Export | Project status reports · Export | تقارير حالة المشروع · تصدير | Relabel | Low |
| Delivery | Project status reports | Action | StatusReport.ManageConfiguration | StatusReport.ManageConfiguration | Manage status report configuration | إدارة إعدادات تقارير الحالة | Relabel | Low |
| Delivery | Project status reports | Action | StatusReport.Publish | StatusReport.Publish | Project status reports · Publish | تقارير حالة المشروع · نشر | Relabel | Medium |
| Delivery | Project status reports | View | StatusReport.View | StatusReport.View | Project status reports · View | تقارير حالة المشروع · عرض | Relabel | Low |
| Capacity | Teams | Action | Team.Create | Team.Create | Teams · Create | الفرق · إنشاء | Keep | Low |
| Capacity | Teams | Action | Team.Delete | Team.Delete | Teams · Delete | الفرق · حذف | Relabel | Medium |
| Capacity | Teams | Action | Team.Edit | Team.Edit | Teams · Edit | الفرق · تعديل | Keep | Low |
| Capacity | Teams | Action | Team.Manage | Retire after compatibility review | Full team administration (legacy umbrella) | إدارة شاملة للفرق (صلاحية عامة قديمة) | Deprecate | Medium |
| Capacity | Teams | View | Team.View | Team.View | Teams · View | الفرق · عرض | Keep | Low |
| Capacity | Time tracking | Action | TimeTracking.ManageOwn | TimeTracking.ManageOwn | Manage own time entries | إدارة سجلات الوقت الشخصية | Keep | Low |
| Capacity | Time tracking | Action | TimeTracking.ManageTeam | TimeTracking.ManageTeam | Manage team time entries | إدارة سجلات وقت الفريق | Keep | Low |
| Capacity | Time tracking | View | TimeTracking.ViewOwn | TimeTracking.ViewOwn | View own time entries | عرض سجلات الوقت الشخصية | Keep | Low |
| Capacity | Time tracking | View | TimeTracking.ViewTeam | TimeTracking.ViewTeam | View team time entries | عرض سجلات وقت الفريق | Keep | Low |
| Administration | Users and access | View | users.read | users.read | Users · Read | المستخدمون · قراءة | Keep | Low |
| Administration | Users and access | Action | users.reset-password | users.reset-password | Reset user passwords | إعادة تعيين كلمات مرور المستخدمين | Relabel | Low |
| Administration | Users and access | Action | users.write | Split into proposed keys | Manage users and access (legacy umbrella) | إدارة المستخدمين والصلاحيات (صلاحية عامة قديمة) | Split | High |
| Delivery | Workflow administration | Action | Workflow.Manage | Workflow.Manage | Workflows · Manage | مسارات العمل · إدارة | Keep | Medium |
| Delivery | Workflow administration | Action | Workflow.Publish | Workflow.Publish | Workflows · Publish | مسارات العمل · نشر | Keep | Medium |
| Delivery | Workflow administration | View | Workflow.View | Workflow.View | Workflows · View | مسارات العمل · عرض | Keep | Low |
| Delivery | Work items | Action | WorkItem.Create | WorkItem.Create | Work items · Create | عناصر العمل · إنشاء | Keep | Low |
| Delivery | Work items | Action | WorkItem.Delete | WorkItem.Delete | Work items · Delete | عناصر العمل · حذف | Relabel | Medium |
| Delivery | Work items | Action | WorkItem.Edit | WorkItem.Edit | Work items · Edit | عناصر العمل · تعديل | Keep | Low |
| Delivery | Work items | View | WorkItem.View | WorkItem.View | Work items · View | عناصر العمل · عرض | Keep | Low |
| Financial Management | Annual Budgets | View | — | ProjectAnnualBudget.View | View Annual Budgets | عرض الميزانيات السنوية | Add | High |
| Financial Management | Payment Requests | View | — | ProjectPaymentRequest.View | View Payment Requests | عرض طلبات الدفع | Add | High |
| Financial Management | Fiscal Year Closing | View | — | ProjectAnnualBudgetClosing.View | View Fiscal Year Closing | عرض إقفالات السنة المالية | Add | High |
| Dashboards and Reporting | Home / Dashboards | View | — | Dashboard.View | View Dashboards | عرض لوحات المعلومات | Add | Medium |
| Financial Management | Annual Budgets | Action | — | ProjectAnnualBudget.Create | Create Annual Budget | إنشاء ميزانية سنوية | Split | High |
| Financial Management | Annual Budgets | Action | — | ProjectAnnualBudget.EditDraft | Edit Draft Budget | تعديل مسودة الميزانية | Split | High |
| Financial Management | Annual Budgets | Action | — | ProjectAnnualBudget.SubmitForReview | Submit for Review | إرسال للمراجعة | Split | High |
| Financial Management | Annual Budgets | Action | — | ProjectAnnualBudget.ReviseApproved | Revise Approved Budget | تعديل الميزانية المعتمدة | Split | High |
| Financial Management | Annual Budgets | Action | — | ProjectAnnualBudget.ManageApprovalConfiguration | Manage Approval Configuration | إدارة إعدادات مسار الاعتماد | Split | Critical |
| Financial Management | Payment Requests | Action | — | ProjectPaymentRequest.Create | Create Payment Request | إنشاء طلب دفع | Split | High |
| Financial Management | Payment Requests | Action | — | ProjectPaymentRequest.EditDraft | Edit Draft Payment Request | تعديل مسودة طلب دفع | Split | High |
| Financial Management | Payment Requests | Action | — | ProjectPaymentRequest.Submit | Submit Payment Request | إرسال طلب دفع | Split | High |
| Financial Management | Payment Requests | Action | — | ProjectPaymentRequest.Cancel | Cancel Payment Request | إلغاء طلب دفع | Split | High |
| Financial Management | Payment Requests | Action | — | ProjectPaymentRequest.Approve | Approve or Reject Payment Request | اعتماد أو رفض طلب دفع | Add | Critical |
| Program Management | Programs | Action | — | Program.CreateAcrossPortfolios | Create Programs Across Portfolios | إنشاء برامج عبر المحافظ | Add | Critical |
| Administration | Users and access | Action | — | Users.ManageAccounts | Manage User Accounts | إدارة حسابات المستخدمين | Split | High |
| Administration | Roles and permissions | Action | — | Roles.Manage | Manage Roles | إدارة الأدوار | Split | Critical |
| Administration | Users and access | Action | — | Users.AssignRoles | Assign Roles to Users | إسناد الأدوار للمستخدمين | Split | Critical |
| Administration | Users and access | Action | — | UserPermissions.Manage | Manage User Permission Overrides | إدارة تجاوزات الصلاحيات المباشرة | Split | Critical |
| Administration | Tenant and organization settings | Action | — | Settings.ManageTenantConfiguration | Manage Tenant Settings | إدارة إعدادات المستأجر | Split | High |
| Administration | Reference data | Action | — | ReferenceData.Manage | Manage Reference Data | إدارة البيانات المرجعية | Split | High |
| Portfolio & Program Management | Programs | Action | — | Program.Create | Create Program | إنشاء برنامج | Split | High |
| Portfolio & Program Management | Programs | Action | — | Program.Edit | Edit Program | تعديل برنامج | Split | High |
| Portfolio & Program Management | Programs | Action | — | Program.Archive | Archive Program | أرشفة برنامج | Split | High |
| Portfolio & Program Management | Programs | Action | — | Program.Reactivate | Reactivate Program | إعادة تفعيل برنامج | Split | Medium |
| Portfolio & Program Management | Portfolios | Action | — | Portfolio.Create | Create Portfolio | إنشاء محفظة | Split | High |
| Portfolio & Program Management | Portfolios | Action | — | Portfolio.Edit | Edit Portfolio | تعديل محفظة | Split | High |
| Portfolio & Program Management | Portfolios | Action | — | Portfolio.Archive | Archive Portfolio | أرشفة محفظة | Split | High |
| Portfolio & Program Management | Portfolios | Action | — | Portfolio.Reactivate | Reactivate Portfolio | إعادة تفعيل محفظة | Split | Medium |
| Project Delivery | Change Requests | Action | — | ProjectFinancialCeiling.ApproveChange | Approve Project Ceiling Change | اعتماد تعديل سقف المشروع | Add | Critical |

## Implemented screens without a tenant-role View key

| Screen | Current access evidence | Proposed target |
|---|---|---|
| Home / Dashboards | Authenticated route; widget APIs have their own checks; no screen View key. | Add Dashboard.View; keep each widget API's own permission and tenant/organization/resource scope. |
| Notifications | User/tenant-scoped APIs; authenticated user access. | Keep as authenticated self-service, with owner and tenant filters; no role key proposed. |
| Account, My Work, and self-service | Authenticated identity or workflow assignment controls data; no common tenant permission family. | Keep owner/assignment-scoped access; no broad tenant key proposed. |
| Administration hub | Navigation shell; cards are individually filtered. | Keep card-level permission/policy checks; no separate hub permission. |
| Tickets and Test Management | Routes exist; Phase 01 did not confirm active business status or dedicated authorization. | Hold. PO must confirm whether these are active modules before proposing tenant View keys. |
| Plugins | Authenticated-only surface; no tenant plugin permission family was found. | Conditional: Plugins.View if tenant-facing; platform policy if operator-only. |
| Platform Settings / Tenant Registry | Separate platform policies and trusted platform authority. | Keep outside tenant Role & Permissions. |

## Financial workflow decisions embodied in the proposal

- Annual Budget approve/return remains ProjectAnnualBudget.Approve; the proposed policy removes the  > inances.write conjunction only after approval and retains assignment, linked active Project Team eligibility, project scope, self-approval prevention, and audit.
- ProjectApproval.Override remains a one-submission exception requiring reason and audit; it is not the persistent approval-configuration action.
- Payment Request approve/reject gets ProjectPaymentRequest.Approve; this is not inherited from  > inances.write and requires the exact assigned reviewer slot.
- PaymentSettlement.Record, reversal create/approve, ProjectAnnualBudget.Close, .CloseApprove, and .CarryForward remain distinct capabilities. Do not add a preview/execute permission unless that operation exists in current source.
-  > inances.write remains a transitional/general financial-write capability while every residual caller is mapped. It must never alone authorize approval, settlement, reversal, closing, carry-forward, or approval override.

## Program creation rule

Normal: require Program.Create, Portfolio.View, active target Portfolio, and native Portfolio Manager assignment to that Portfolio; enforce tenant, organization, and project/Portfolio management boundaries.

Exceptional: require Program.Create plus Program.CreateAcrossPortfolios, Portfolio.View, an active eligible target Portfolio inside the user's authorized tenant **and organization** scope, and all other current validation. The cross-Portfolio permission expands capability; it does not grant global scope or replace tenant/organization checks. No role-name shortcut is proposed. No automatic grant to Program Managers or pmo.admin.

## Translation and key rules

Keys are stable machine identifiers; display labels/descriptions may be corrected without renaming keys. Use View=عرض, Create=إنشاء, Edit=تعديل, Submit for Review=إرسال للمراجعة, Approve=اعتماد, Reject=رفض, Return for Changes=إعادة للتعديل, Assign=إسناد, Reassign=إعادة إسناد, Override=تجاوز الإعداد الافتراضي, Archive=أرشفة, Close=إقفال, Reactivate=إعادة تفعيل. The Phase 01 replacement-character separator must be removed from displayed labels. All proposals remain unapproved.

