# تقرير قبول Universal Project Intelligence (UPI)

**التاريخ:** 23 نوفمبر 2026  
**المشروع:** C:\Projects\pmo  
**النطاق:** اختبار قراءة فقط؛ لا تنفيذ لإصلاح الحالة، ولا تعديل لكود التطبيق أو قاعدة البيانات.

## خلاصة القبول

نجح تحميل تعليمات المستودع والمهارة في مهمة Codex جديدة، ونجح التوجيه للعبارة المطلوبة، والتحديث التزايدي، واختبارات UPI الاثنا عشر. بعد تحديث ملف فهرسة متقادم واحد، اجتاز الفهرس التحقق بلا أخطاء أو تحذيرات.

مع ذلك، **دقة اختيار ملفات مسار الحفظ لم تجتز الفحص المصدرّي**: اختار التوجيه وحدة المشاريع، لكنه ركز على تقارير الحالة والانتقالات والمقارنة، ولم يختَر تنفيذ حفظ حالة المشروع أو تنفيذ تحديث تقرير الحالة. لذا فالنتيجة **قبول جزئي**: آلية التثبيت والتوجيه والتحديث تعمل، لكن اختيار النطاق لعبارة العطل المحددة يحتاج تحسينًا قبل اعتبارها قبولًا كاملًا.

## الجلسة الجديدة والقواعد

أُنشئت مهمة Codex مستقلة بسياق جديد، وظهر فيها مسار مهارة UPI داخل .agents/skills/universal-project-intelligence، كما كانت قواعد المستودع متاحة. قرأت المهمة AGENTS.md وdocs/architecture/INDEX.md والمهارة، ثم شغّلت التوجيه قبل قراءة أي كود تطبيق. هذا يثبت أن جلسة جديدة استطاعت تحميل التعليمات واستخدامها عمليًا. لا يتيح سجل المهمة إثبات تفاصيل آلية التحميل الداخلية لدى المضيف خارج التعليمات التي ظهرت في السياق.

## نتيجة التوجيه

- العبارة: Fix Project Status update not saving correctly
- الأمر: .\tools\upi\upi.ps1 route --root . --task "Fix Project Status update not saving correctly" --budget 16000
- الوحدة الأساسية: projects؛ أساس الاختيار: task-explicit-alias
- بيان النطاق: docs/architecture/task-scopes/20261122T233310-fix-project-status-update-not-saving-correctly-d3fde.md
- لقطة التوجيه: 20261122T233247-4353a44
- الملفات المختارة: 17 ملفًا، بتقدير 9,581 توكن للمصدر؛ مخصص 6,400 توكن للسياق الآخر ضمن ميزانية تخطيط 16,000.
- طريقة التقدير: estimated:chars/3.5، لذا الأعداد تقريبية وليست قياسًا لاستهلاك التنفيذ الكلي.

### الملفات والرموز وأسباب الاختيار

| الملف | النطاق | الرمز المطابق | سبب الاختيار كما سجله الموجّه | التوكن |
|---|---:|---|---|---:|
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs | 6-12, 14-19, 21-26, 28-42, 44-89 | ProjectResourceUtilizationStatuses | تطابق اسم status، الوحدة الأساسية والمذكورة، وتطابق الرمز | 1,173 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs | 1-17 | IProjectStatusReportService | تطابق اسم status، الوحدة، وتطابق الرمز | 484 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportComparison.cs | 1-132 | ProjectStatusReportComparison | تطابق اسم status، الوحدة، وتطابق الرمز | 2,194 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusCatalogEntry.cs | 1-20 | ProjectStatusCatalogEntry | تطابق اسم status، الوحدة، وتطابق الرمز | 268 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReport.cs | 1-84 | ProjectStatusReport | تطابق اسم status، الوحدة، وتطابق الرمز | 1,069 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportSnapshots.cs | 1-62 | ProjectStatusReportRiskSnapshot | تطابق اسم status، الوحدة، وتطابق الرمز | 753 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportingSemantics.cs | 1-49 | ProjectStatusReportingCategories | تطابق اسم status، الوحدة، وتطابق الرمز | 454 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusTransitionRule.cs | 6-13 | ProjectStatusTransitionRule | تطابق اسم status، الوحدة، وتطابق الرمز | 86 |
| apps/web-react/src/features/delivery/WorkItemStatusTrack.tsx | 1-75 | WorkItemStatusTrackProps | تطابق اسم status، الوحدة الأساسية والمذكورة، وتطابق الرمز | 915 |
| apps/web-react/src/features/projects/lib/gantt-status.ts | 1-21 | GanttStatus | تطابق اسم status، الوحدة، وتطابق الرمز | 326 |
| apps/web-react/src/test/project-change-status.test.tsx | 120-127 | statuses | تطابق اسم status، الوحدة، وتطابق الرمز | 173 |
| apps/web-react/src/test/project-status-reports-parity.test.tsx | 114-126 | renderStatusReports | تطابق اسم status، الوحدة، وتطابق الرمز | 118 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs | 99-104, 108-113, 117-122, 126-133, 163-170 | GetStatusReports | الوحدة الأساسية والمذكورة، وتطابق الرمز | 701 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectLifecycleCatalogController.cs | 97-101, 104-108, 111-115 | ActivateStatus | الوحدة الأساسية والمذكورة، وتطابق الرمز | 315 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs | 3-9, 11-17, 19-26, 28-33, 62 | ProjectPhaseGateReadinessStatuses | الوحدة الأساسية والمذكورة، وتطابق الرمز | 342 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs | 210-225 | SnapshotStatusReportDto | الوحدة الأساسية والمذكورة، وتطابق الرمز | 182 |
| apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogContracts.cs | 70 | ProjectStatusTransitionOptionDto | الوحدة الأساسية والمذكورة، وتطابق الرمز | 28 |

## مطابقة الاختيار مع مسارات الحفظ

العبارة تحتمل مسارين مختلفين، وفحص المصدر يبين أن كليهما لم يُغطَّ بما يكفي:

1. **حالة المشروع:** نموذج apps/web-react/src/features/projects/components/CreateProjectSheet.tsx يستخدم useUpdateProject ويرسل statusId؛ دالة apps/web-react/src/features/projects/api/projects.ts ترسل PUT إلى /api/projects/{id}. يصل الطلب إلى ProjectController.Update ثم ProjectService.UpdateAsync، الذي يستدعي ProjectMapping.UpdateEntity ويحفظ عبر SaveChangesAsync. الحقول StatusId وStatusChangeReason موجودة في UpdateProjectDto، والتحويل يكتب StatusId إلى ProjectStatusId. التوجيه لم يختر CreateProjectSheet أو projects.ts أو ProjectService.cs أو ProjectMapping.cs أو ProjectDto.cs، كما لم يشمل نطاق ProjectController طريقة Update عند الأسطر 282-295.
2. **تقرير الحالة:** واجهة ProjectGovernanceTabs تستدعي useUpdateProjectStatusReport؛ الخطاف في projects.ts يرسل PUT إلى /api/projects/{projectId}/status-reports/{reportId}. يستدعي ProjectController.UpdateStatusReport خدمة ProjectStatusReportService.UpdateAsync التي تحفظ المسودة. التوجيه اختار واجهة IProjectStatusReportService التي تعلن UpdateAsync، لكنه لم يختر تنفيذ ProjectStatusReportService.cs ولا واجهة الاستخدام. نطاق ProjectController المختار 163-170 يغطي إنشاء التقرير، ولا يغطي التحديث عند 174-181.

الملفات المختارة ذات الصلة بالحالة أو تقريرها، مثل المقارنة وواجهات القراءة واختبارات التكافؤ، قد تساعد بعد تحديد نوع الحالة، لكنها لا تقود مباشرة إلى مسار الحفظ. لذلك دقة الوحدة الأساسية **ناجحة**، أما دقة ترتيب الملفات بالنسبة إلى عطل «لا يُحفظ» فهي **فاشلة**. الاختبارات الحالية لا تكشف ذلك: اختبار UPI الذي يغطي العبارة يتحقق من اختيار المشاريع واستبعاد Issues وCost Management، ولا يثبت اختيار endpoint/service الخاص بالحفظ.

## الوحدات المستبعدة والعلاقات

لم يقرأ النطاق الأولي أي مصدر من وحدتي Issues أو Cost Management؛ وهذا مناسب كبداية لعطل حفظ الحالة نفسه. يحتوي نموذج تقرير الحالة على مراجع ولقطات لمخاطر وقضايا وتكاليف، كما قد تظهر علاقات تلك الوحدات في الرسم البياني، لكنها ليست سببًا كافيًا لقراءة مصادرها قبل ظهور دليل. إذا اتضح أن العطل في توليد بيانات التقرير أو لقطات التكلفة/القضايا، عندها يوسّع النطاق مع توثيق السبب. عناصر “Dependency awareness” في البيان إرشادية فقط، وتشمل علاقات مستنتجة؛ لم تُعامل كملفات مختارة للقراءة.

## سلامة الفهرس والتحديث التزايدي

- قبل التوجيه كان الفهرس يسجل 1,880 ملفًا و36 وحدة؛ وهذا يختلف بملف واحد عن رقم 1,879 في تقرير المحادثة السابق.
- تحديث التوجيه التلقائي: ملف واحد أُعيد تحليله، 1,879 دون تغيير، 1 ملف موجود متغير، 0 جديد، 0 محذوف؛ index_updated=true.
- أظهر تحقق لاحق ملف اختبار تطبيق متغيرًا وقديمًا في الفهرس. حدّثت بيانات الفهرسة فقط؛ لقطة الفهرس الحالية 20261122T233657-129cca3 وتسجل 1,880 ملفًا و11,599 رمزًا و71,508 علاقة. لم أعدّل ملف التطبيق.
- نتيجة التحقق النهائية: ok=true، لا أخطاء ولا تحذيرات.
- اختبارات UPI: 12/12 ناجحة، بما فيها اكتشاف الإضافة/إعادة التسمية/الحذف، عدم إعادة تحليل الملفات غير المتغيرة، التحديث عند التوجيه، واستبعاد مصادر Issues وCost Management من نطاق مشروع الحالة.

## معايير القبول

| المعيار | النتيجة | الدليل |
|---|---|---|
| المهارة والقواعد متاحتان في جلسة Codex جديدة واستُخدمتا | PASS | المهمة الجديدة قرأت AGENTS.md وINDEX والمهارة، ثم اتبعت التوجيه |
| قراءة القواعد والفهرس قبل كود التطبيق | PASS | سجل المهمة الجديدة؛ التوجيه سبق فحص نطاقات المصدر |
| نجاح التوجيه الدقيق بميزانية 16,000 | PASS | بيان المهمة ونتيجة خروج الأمر بنجاح |
| اختيار وحدة المشاريع | PASS | الرمز الصريح في العبارة اختار projects |
| مطابقة الملفات المختارة لمسار الحفظ | FAIL | غياب ProjectService.UpdateAsync وتنفيذ تحديث تقرير الحالة ونقاط الواجهة |
| استبعاد Issues وCost Management من القراءة الأولية | PASS | لا ملف مختار من وحدتيهما؛ الروابط المستنتجة لم تُقرأ |
| تقدير التوكن وحدوده موضّحان | PASS | 9,581 مصدرًا، 6,400 للسياق، وتقدير chars/3.5 معلّم كتقريبي |
| التحديث التزايدي وسلامة الفهرس | PASS | تحليل ملف متغير واحد، ثم validate نهائي بلا تحذيرات |
| اختبارات UPI | PASS | 12 من 12 |
| عدم تعديل التطبيق أو قاعدة البيانات | PASS | العمل اقتصر على القراءة، الاختبارات المؤقتة، وفهرسة docs/architecture؛ لم يُنفذ إصلاح |

**الحكم:** قبول جزئي. UPI محمّل وقابل للاستخدام في جلسة جديدة، وفهرسه واختباراته سليمون. يلزم تحسين دقة التوجيه لتمييز حالة المشروع عن تقرير الحالة وإبراز مسار الحفظ عند عبارة “update not saving” قبل إقرار قبول كامل لهذه الحالة.
