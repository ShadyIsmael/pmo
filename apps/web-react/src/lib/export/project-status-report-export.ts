import type { ProjectStatusReportDto, ProjectStatusReportListDto, ProjectStatusReportRiskSnapshotDto, ProjectStatusReportIssueSnapshotDto } from "@features/projects/api/projects";
import { brand } from "@lib/brand";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import { getEvmReasonTranslationCandidates } from "@features/projects/lib/project-labels";
import enMessages from "../../i18n/messages/en.json";
import arMessages from "../../i18n/messages/ar.json";

type PdfAlignment = "left" | "right" | "center";
type PdfMargin = [number, number, number, number];
type PdfCell = { text?: string; stack?: unknown[]; bold?: boolean; color?: string; fillColor?: string; alignment?: PdfAlignment; margin?: PdfMargin; fontSize?: number; lineHeight?: number; colSpan?: number };
type PdfContent = PdfCell & { table?: { rtl?: boolean; headerRows?: number; keepWithHeaderRows?: number; dontBreakRows?: boolean; widths?: unknown[]; body: PdfCell[][] }; layout?: unknown; columns?: unknown[]; columnGap?: number; svg?: string; width?: number; height?: number; style?: string | string[]; pageBreak?: string; unbreakable?: boolean };

export interface ProjectStatusReportPdfInput {
  report: ProjectStatusReportDto;
  publishedHistory?: readonly ProjectStatusReportListDto[];
  workItemStatuses?: readonly { code: string; nameEn: string; nameAr: string }[];
  locale?: string;
}

const LABELS = {
  en: {
    report: "Project Status Report", projectInformation: "Project Information", projectName: "Project Name", projectCode: "Project Code", reportDate: "Report Date", preparedBy: "Prepared By", sourceCapturedAt: "Source Data Captured At", publishedAt: "Published At", reportState: "Report Status", reportingPeriodStatus: "Reporting Period & Overall Status", statusDate: "Status Date", reportingPeriod: "Reporting Period", projectStatus: "Project Status at Report", overallHealth: "Overall Health", overallProgress: "Overall Progress", costHealth: "Cost Health", qualityHealth: "Quality Health", resourceUtilization: "Resource Utilization", progressPerformance: "Progress & Performance", performanceKpis: "Performance KPIs", dataQuality: "Data Quality", dataQualityReasons: "Data Quality Notes", financialSummary: "Financial Summary", currency: "Currency", approvedBudget: "Approved Budget", actualPaid: "Actual Paid", outstandingPlannedPayments: "Outstanding Planned Payments", costVariance: "Cost Variance", budget: "Budget", actualCost: "Actual Cost", committedCost: "Committed Cost", forecastCost: "Forecast", unallocatedProjectActualCost: "Unallocated Project Actual Cost", reportingConfigurationVersion: "Reporting Configuration Version", executiveSummary: "Executive Summary", keyAchievements: "Key Achievements", risksIssues: "Risks & Issues", challenges: "Challenges & Dependencies", challengesConcerns: "Challenges & Concerns", nextStepsActions: "Next Steps & Action Items", nextPriorities: "Next Period Priorities", actionsNeeded: "Actions Needed", supportNeeded: "Support Needed", keyRisks: "Top Risks", keyIssues: "Top Issues", milestones: "Milestones", plan: "Plan", baseline: "Baseline Date", plannedDate: "Planned Date", actualDate: "Actual Completion", variance: "Variance", decisionsRequired: "Management Decisions", decisionsSourceNote: "Decision items come from pending project change requests; report snapshots capture up to five prioritized requests.", title: "Title", level: "Level", status: "Status", owner: "Owner", severity: "Severity", assignee: "Assignee", number: "Number", reason: "Reason", justification: "Justification", impact: "Impact", requestedBy: "Requested By", requestedAt: "Requested At", pendingDecision: "Decision Required", noData: "No snapshot items", noDecisionsRequired: "No pending change-request decisions were captured.", unavailable: "—", days: "days", page: "Page", of: "of",
  },
  ar: {
    report: "تقرير حالة المشروع", projectInformation: "معلومات المشروع", projectName: "اسم المشروع", projectCode: "رمز المشروع", reportDate: "تاريخ التقرير", preparedBy: "أعده", sourceCapturedAt: "وقت التقاط البيانات المصدر", publishedAt: "تاريخ النشر", reportState: "حالة التقرير", reportingPeriodStatus: "فترة التقرير والحالة العامة", statusDate: "تاريخ الحالة", reportingPeriod: "فترة التقرير", projectStatus: "حالة المشروع وقت التقرير", overallHealth: "الصحة العامة", overallProgress: "التقدم الإجمالي", costHealth: "صحة التكلفة", qualityHealth: "صحة الجودة", resourceUtilization: "استغلال الموارد", progressPerformance: "التقدم والأداء", performanceKpis: "مؤشرات الأداء", dataQuality: "جودة البيانات", dataQualityReasons: "ملاحظات جودة البيانات", financialSummary: "الملخص المالي", currency: "العملة", approvedBudget: "الميزانية المعتمدة", actualPaid: "المدفوع فعلياً", outstandingPlannedPayments: "المدفوعات المستحقة المخططة", costVariance: "انحراف التكلفة", budget: "الميزانية", actualCost: "التكلفة الفعلية", committedCost: "التكلفة الملتزم بها", forecastCost: "التوقعات", unallocatedProjectActualCost: "التكلفة الفعلية غير المخصصة", reportingConfigurationVersion: "إصدار إعداد التقارير", executiveSummary: "الملخص التنفيذي", keyAchievements: "أهم الإنجازات", risksIssues: "المخاطر والمشكلات", challenges: "التحديات والاعتماديات", challengesConcerns: "التحديات والمخاوف", nextStepsActions: "الخطوات التالية والإجراءات", nextPriorities: "أولويات الفترة القادمة", actionsNeeded: "الإجراءات المطلوبة", supportNeeded: "الدعم المطلوب", keyRisks: "أهم المخاطر", keyIssues: "أهم المشكلات", milestones: "المعالم", plan: "الخطة", baseline: "تاريخ خط الأساس", plannedDate: "التاريخ المخطط", actualDate: "الإكمال الفعلي", variance: "الانحراف", decisionsRequired: "قرارات الإدارة", decisionsSourceNote: "تأتي عناصر القرار من طلبات التغيير المعلّقة، وتلتقط لقطات التقرير حتى خمسة طلبات حسب الأولوية.", title: "العنوان", level: "المستوى", status: "الحالة", owner: "المالك", severity: "الحدة", assignee: "المسؤول", number: "الرقم", reason: "السبب", justification: "المبرر", impact: "الأثر", requestedBy: "مقدم الطلب", requestedAt: "تاريخ الطلب", pendingDecision: "يتطلب قراراً", noData: "لا توجد عناصر في اللقطة", noDecisionsRequired: "لم تُلتقط طلبات تغيير معلّقة تتطلب قراراً.", unavailable: "—", days: "يوم", page: "صفحة", of: "من",
  },
} as const;

type Labels = Record<keyof typeof LABELS.en, string>;

const HEALTH_LABELS = { en: { Green: "Green", Amber: "Amber", Red: "Red" }, ar: { Green: "أخضر", Amber: "كهرماني", Red: "أحمر" } } as const;
const RESOURCE_UTILIZATION_LABELS: Record<"en" | "ar", { label: string; states: Record<string, string> }> = {
  en: { label: "Resource Utilization", states: { Overallocated: "Overallocated", WithinCapacity: "Within capacity", NoAssignedWork: "No assigned work", Unavailable: "Unavailable" } },
  ar: { label: "استغلال الموارد", states: { Overallocated: "تجاوز السعة", WithinCapacity: "ضمن السعة", NoAssignedWork: "لا توجد أعمال مسندة", Unavailable: "غير متاح" } },
};
const ENUM_LABELS = {
  en: { critical: "Critical", high: "High", medium: "Medium", low: "Low", major: "Major", minor: "Minor", identified: "Identified", expected: "Expected", active: "Active", actionrequired: "Action Required", followup: "Follow Up", mitigated: "Mitigated", closed: "Closed", open: "Open", inprogress: "In Progress", blocked: "Blocked", resolved: "Resolved", pending: "Pending", approved: "Approved", rejected: "Rejected", cancelled: "Cancelled", administrative: "Administrative", baseline: "Baseline", completed: "Completed", delayed: "Delayed", upcoming: "Upcoming", initiation: "Initiation", planning: "Planning", onhold: "On Hold", draft: "Draft", published: "Published", complete: "Complete", partial: "Partial", unavailable: "Unavailable" },
  ar: { critical: "حرج", high: "مرتفع", medium: "متوسط", low: "منخفض", major: "رئيسي", minor: "طفيف", identified: "محدد", expected: "متوقع", active: "نشط", actionrequired: "يتطلب إجراء", followup: "متابعة", mitigated: "تمت المعالجة", closed: "مغلق", open: "مفتوح", inprogress: "قيد التنفيذ", blocked: "متوقف", resolved: "تم الحل", pending: "معلق", approved: "معتمد", rejected: "مرفوض", cancelled: "ملغي", administrative: "إداري", baseline: "خط الأساس", completed: "مكتمل", delayed: "متأخر", upcoming: "قادم", initiation: "المبادرة", planning: "التخطيط", onhold: "معلق", draft: "مسودة", published: "منشور", complete: "مكتمل", partial: "جزئي", unavailable: "غير متاح" },
} as const;

const REPORT_SECTION_KEYS = ["ExecutiveSummary", "PerformanceKPIs", "PlanPerformance", "Milestones", "RisksIssues", "DecisionsRequired"] as const;
type ReportSection = (typeof REPORT_SECTION_KEYS)[number];
function includedReportSections(report: ProjectStatusReportDto): readonly string[] {
  const configured = report.includedSectionsSnapshot?.length ? report.includedSectionsSnapshot : report.performanceSnapshot?.includedReportSections;
  return configured?.length ? configured : REPORT_SECTION_KEYS;
}
function hasReportSection(report: ProjectStatusReportDto, section: ReportSection): boolean { return includedReportSections(report).includes(section); }

function normalize(value: string | undefined): string { return (value ?? "").trim().toLowerCase().replace(/[\s_-]/g, ""); }
function text(value: unknown, fallback: string): string { return typeof value === "string" && value.trim() ? value.trim() : fallback; }
function isFiniteNumber(value: unknown): value is number { return typeof value === "number" && Number.isFinite(value); }
function localizedMessage(locale: "en" | "ar", key: string, fallback: string): string {
  let current: unknown = locale === "ar" ? arMessages : enMessages;
  for (const part of key.split(".")) {
    if (!current || typeof current !== "object" || !(part in current)) return fallback;
    current = (current as Record<string, unknown>)[part];
  }
  return typeof current === "string" ? current : fallback;
}
function localizedEnumMessage(locale: "en" | "ar", namespace: string, value: string | undefined, fallback: string): string {
  if (!value) return fallback;
  const key = `${value.charAt(0).toLowerCase()}${value.slice(1)}`;
  return localizedMessage(locale, `${namespace}.${key}`, fallback);
}
function displayEnumLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string { return value ? ENUM_LABELS[locale][normalize(value) as keyof (typeof ENUM_LABELS)[typeof locale]] ?? value : labels.unavailable; }
function healthLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string { return localizedEnumMessage(locale, "enums.projectHealth", value, HEALTH_LABELS[locale][value as keyof (typeof HEALTH_LABELS)[typeof locale]] ?? labels.unavailable); }
function statusColor(value: string | undefined): string { const normalized = normalize(value); return normalized === "green" ? "#15803D" : normalized === "amber" ? "#B45309" : normalized === "red" ? "#B91C1C" : "#697586"; }
function utilizationLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string { return value ? localizedMessage(locale, `projects.resourceUtilizationStates.${value}`, RESOURCE_UTILIZATION_LABELS[locale].states[value] ?? healthLabel(value, labels, locale)) : labels.unavailable; }
function utilizationColor(value: string | undefined): string {
  if (value === "Overallocated") return "#B91C1C";
  if (value === "WithinCapacity") return "#15803D";
  if (value === "Unavailable") return "#B45309";
  return statusColor(value);
}
function dataQualityReasonLabel(reason: string, locale: "en" | "ar"): string {
  const reasons = (locale === "ar" ? arMessages.projects.evmReasons : enMessages.projects.evmReasons) as Record<string, string>;
  for (const candidate of getEvmReasonTranslationCandidates(reason)) {
    if (reasons[candidate]) return reasons[candidate];
  }
  return reason;
}
function workItemStatusLabel(value: string | undefined, statuses: ProjectStatusReportPdfInput["workItemStatuses"], locale: "en" | "ar", fallback: string): string {
  if (!value) return fallback;
  const match = statuses?.find((status) => status.code === value);
  if (!match) return value;
  const names = locale === "ar" ? [match.nameAr, match.nameEn] : [match.nameEn, match.nameAr];
  return names.find((name) => name.trim())?.trim() ?? value;
}
function financialDonutSvg(actualValue: number | undefined, outstandingValue: number | undefined): string {
  const actual = isFiniteNumber(actualValue) && actualValue > 0 ? actualValue : 0;
  const outstanding = isFiniteNumber(outstandingValue) && outstandingValue > 0 ? outstandingValue : 0;
  const scale = Math.max(actual, outstanding);
  const scaledActual = scale > 0 ? actual / scale : 0;
  const scaledOutstanding = scale > 0 ? outstanding / scale : 0;
  const total = scaledActual + scaledOutstanding;
  const circumference = 2 * Math.PI * 44;
  const actualLength = total > 0 ? circumference * scaledActual / total : 0;
  const outstandingLength = total > 0 ? circumference * scaledOutstanding / total : 0;
  const track = `<circle cx="64" cy="64" r="44" fill="none" stroke="#E2E8F0" stroke-width="16"/>`;
  const actualSlice = actualLength > 0 ? `<circle cx="64" cy="64" r="44" fill="none" stroke="#2468A8" stroke-width="16" stroke-dasharray="${actualLength} ${circumference - actualLength}" transform="rotate(-90 64 64)"/>` : "";
  const outstandingSlice = outstandingLength > 0 ? `<circle cx="64" cy="64" r="44" fill="none" stroke="#104AC6" stroke-width="16" stroke-dasharray="${outstandingLength} ${circumference - outstandingLength}" stroke-dashoffset="${-actualLength}" transform="rotate(-90 64 64)"/>` : "";
  return `<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">${track}${actualSlice}${outstandingSlice}</svg>`;
}
function percentage(value: number | undefined | null, locale: "en" | "ar", digits = 1, withSign = false): string { if (!isFiniteNumber(value)) return "—"; const prefix = withSign && value > 0 ? "+" : ""; return `${prefix}${formatNumberValue(value, locale, { maximumFractionDigits: digits })}%`; }
function formatReportDate(value: string | null | undefined, locale: "en" | "ar", fallback: string): string { if (!value) return fallback; const date = new Date(value); return Number.isNaN(date.getTime()) ? fallback : new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", { dateStyle: "medium", timeZone: "UTC" }).format(date); }
function formatReportDateTime(value: string | null | undefined, locale: "en" | "ar", fallback: string): string { if (!value) return fallback; const date = new Date(value); return Number.isNaN(date.getTime()) ? fallback : new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US-u-nu-latn", { dateStyle: "medium", timeStyle: "short" }).format(date); }
function money(value: number | undefined, currency: string | undefined, locale: "en" | "ar", fallback: string): string {
  if (!isFiniteNumber(value)) return fallback;
  const formatted = formatCurrencyValue(value, currency, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  if (formatted === "—") return fallback;
  return locale === "ar" ? "‎" + formatted + "‎" : formatted;
}
function cell(value: unknown, alignment: PdfAlignment, bold = false): PdfCell { return { text: String(value ?? ""), alignment, bold, margin: [6, 5, 6, 5] }; }
function fieldCell(label: string, value: string, alignment: PdfAlignment, color = "#202833"): PdfCell { return { stack: [{ text: label, color: "#697586", fontSize: 9, alignment }, { text: value, bold: true, color, fontSize: 11, alignment, margin: [0, 3, 0, 0] }], margin: [6, 6, 6, 6] }; }
function sectionTitle(value: string, alignment: PdfAlignment): PdfContent { return { text: value, style: "sectionTitle", alignment, margin: [0, 15, 0, 7] }; }
function compactSectionTitle(value: string, alignment: PdfAlignment): PdfContent { return { text: value, bold: true, color: "#1E4E79", fontSize: 12, alignment, margin: [0, 8, 0, 5] }; }
function subsectionTitle(value: string, alignment: PdfAlignment): PdfContent { return { text: value, bold: true, color: "#334155", fontSize: 11, alignment, margin: [0, 4, 0, 6] }; }
function narrative(label: string, value: string | undefined, alignment: PdfAlignment, labels: Labels): PdfContent { return { stack: [{ text: label, bold: true, color: "#334155", fontSize: 11, alignment, margin: [0, 0, 0, 4] }, { text: text(value, labels.unavailable), alignment, color: value?.trim() ? "#202833" : "#697586", fontSize: 10, lineHeight: 1.25 }], margin: [0, 0, 0, 11] }; }
function snapshotTable(title: string, headers: string[], rows: PdfCell[][], alignment: PdfAlignment, isRtl: boolean, labels: Labels, widths?: unknown[], compact = false, titleFontSize = 9): PdfContent {
  const dataRows = rows.length ? rows : [[{ ...cell(labels.noData, alignment), colSpan: headers.length }, ...Array.from({ length: Math.max(0, headers.length - 1) }, () => ({}))]];
  const formatCell = (value: PdfCell): PdfCell => {
    if (!compact || (value.text === undefined && !value.stack?.length)) return value;
    return { ...value, fontSize: 8.5, margin: [3, 3, 3, 3] };
  };
  const titleBlock: PdfContent[] = title ? [{ text: title, bold: true, color: "#334155", fontSize: titleFontSize, alignment, margin: compact ? [0, 4, 0, 3] : [0, 15, 0, 7] }] : [];
  const body: PdfCell[][] = [
    headers.map((header) => formatCell({ ...cell(header, alignment, true), color: "#334155", fillColor: "#E8EEF2" })),
    ...dataRows.map((row) => row.map(formatCell)),
  ];
  return { stack: [...titleBlock, { table: { rtl: isRtl, headerRows: 1, keepWithHeaderRows: 1, dontBreakRows: true, widths: widths ?? headers.map(() => "*"), body }, layout: "lightHorizontalLines" }], ...(dataRows.length <= 3 ? { unbreakable: true } : {}) };
}
function narrativeSection(title: string, value: string | undefined, alignment: PdfAlignment, labels: Labels): PdfContent { return { stack: [sectionTitle(title, alignment), narrative(title, value, alignment, labels)], unbreakable: true }; }

export function buildProjectStatusReportDocument(input: ProjectStatusReportPdfInput): Record<string, unknown> {
  const { report } = input;
  const locale = input.locale === "ar" ? "ar" : "en";
  const fallbackLabels = LABELS[locale];
  const message = (key: string, fallback: string) => localizedMessage(locale, key, fallback);
  const labels: Labels = {
    ...fallbackLabels,
    report: message("projects.statusReportTitle", fallbackLabels.report),
    performanceKpis: message("projects.reporting.sections.performanceKpis", fallbackLabels.performanceKpis),
    projectInformation: message("projects.statusReportView.sections.projectInformation", fallbackLabels.projectInformation),
    reportDate: message("projects.reportDate", fallbackLabels.reportDate),
    preparedBy: message("projects.preparedBy", fallbackLabels.preparedBy),
    sourceCapturedAt: message("projects.sourceDataCapturedAt", fallbackLabels.sourceCapturedAt),
    publishedAt: message("projects.publishedAt", fallbackLabels.publishedAt),
    reportState: message("projects.reportState", fallbackLabels.reportState),
    reportingPeriodStatus: message("projects.statusReportView.sections.reportingPeriodStatus", fallbackLabels.reportingPeriodStatus),
    statusDate: message("projects.statusDate", fallbackLabels.statusDate),
    dataQuality: message("projects.statusReportView.dataQuality", fallbackLabels.dataQuality),
    reportingPeriod: message("projects.reportingPeriod", fallbackLabels.reportingPeriod),
    projectStatus: message("projects.projectStatusAtReport", fallbackLabels.projectStatus),
    overallHealth: message("projects.overallHealth", fallbackLabels.overallHealth),
    overallProgress: message("projects.overallProgress", fallbackLabels.overallProgress),
    costHealth: message("projects.costHealth", fallbackLabels.costHealth),
    qualityHealth: message("projects.qualityHealth", fallbackLabels.qualityHealth),
    resourceUtilization: message("projects.resourceUtilization", fallbackLabels.resourceUtilization),
    progressPerformance: message("projects.statusReportView.sections.progressPerformance", fallbackLabels.progressPerformance),
    financialSummary: message("projects.statusReportView.sections.financialSummary", fallbackLabels.financialSummary),
    currency: message("projects.currencyCode", fallbackLabels.currency),
    approvedBudget: message("projects.approvedBudget", fallbackLabels.approvedBudget),
    actualPaid: message("projects.actualPaid", fallbackLabels.actualPaid),
    outstandingPlannedPayments: message("projects.outstandingPlannedPayments", fallbackLabels.outstandingPlannedPayments),
    costVariance: message("projects.costVariance", fallbackLabels.costVariance),
    budget: message("projects.budget", fallbackLabels.budget),
    actualCost: message("projects.actualCost", fallbackLabels.actualCost),
    committedCost: message("projects.committedCost", fallbackLabels.committedCost),
    forecastCost: message("projects.forecast", fallbackLabels.forecastCost),
    unallocatedProjectActualCost: message("projects.unallocatedProjectActualCost", fallbackLabels.unallocatedProjectActualCost),
    reportingConfigurationVersion: message("projects.reportingConfigurationVersion", fallbackLabels.reportingConfigurationVersion),
    executiveSummary: message("projects.executiveSummary", fallbackLabels.executiveSummary),
    keyAchievements: message("projects.keyAchievements", fallbackLabels.keyAchievements),
    risksIssues: message("projects.reporting.sections.risksIssues", fallbackLabels.risksIssues),
    challenges: message("projects.statusReportView.sections.challengesDependencies", fallbackLabels.challenges),
    challengesConcerns: message("projects.challengesConcerns", fallbackLabels.challengesConcerns),
    nextStepsActions: message("projects.statusReportView.sections.nextStepsActions", fallbackLabels.nextStepsActions),
    nextPriorities: message("projects.nextPeriodPriorities", fallbackLabels.nextPriorities),
    actionsNeeded: message("projects.actionsNeeded", fallbackLabels.actionsNeeded),
    supportNeeded: message("projects.supportNeeded", fallbackLabels.supportNeeded),
    keyRisks: message("projects.topRisks", fallbackLabels.keyRisks),
    keyIssues: message("projects.topIssues", fallbackLabels.keyIssues),
    milestones: message("projects.reporting.sections.milestones", fallbackLabels.milestones),
    plan: message("projects.plan", fallbackLabels.plan),
    baseline: message("projects.statusReportComparison.baselineDate", fallbackLabels.baseline),
    plannedDate: message("projects.statusReportComparison.plannedDate", fallbackLabels.plannedDate),
    actualDate: message("projects.statusReportComparison.actualDate", fallbackLabels.actualDate),
    variance: message("projects.statusReportComparison.variance", fallbackLabels.variance),
    decisionsRequired: message("projects.reporting.sections.decisionsRequired", fallbackLabels.decisionsRequired),
    decisionsSourceNote: message("projects.statusReportComparison.decisionsSourceNote", fallbackLabels.decisionsSourceNote),
    title: message("projects.name", fallbackLabels.title),
    level: message("risks.riskLevel", fallbackLabels.level),
    status: message("common.status", fallbackLabels.status),
    owner: message("projects.owner", fallbackLabels.owner),
    severity: message("workItems.severity", fallbackLabels.severity),
    assignee: message("projects.assignee", fallbackLabels.assignee),
    requestedBy: message("projects.requestedBy", fallbackLabels.requestedBy),
    requestedAt: message("projects.requestedAt", fallbackLabels.requestedAt),
    impact: message("projects.statusReportComparison.impact", fallbackLabels.impact),
    pendingDecision: message("projects.statusReportComparison.pendingDecision", fallbackLabels.pendingDecision),
    noData: message("projects.noSnapshotItems", fallbackLabels.noData),
    noDecisionsRequired: message("projects.statusReportComparison.noDecisionsRequired", fallbackLabels.noDecisionsRequired),
    days: message("projects.days", fallbackLabels.days),
  };
  const performanceLabels = { statusDate: labels.statusDate, quality: labels.dataQuality, bac: "BAC", pv: "PV", ev: "EV", ac: "AC", sv: "SV", cv: "CV", spi: "SPI", cpi: "CPI", plan: labels.plan };
  const planPerformanceTitle = localizedMessage(locale, "projects.reporting.sections.planPerformance", locale === "ar" ? "????? ???? ?????" : "Plan Performance Breakdown");
  const isRtl = locale === "ar";
  const alignment: PdfAlignment = isRtl ? "right" : "left";
  const fallback = labels.unavailable;
  const currency = report.currencyCodeSnapshot?.trim();
  const hasFinancialSummary = Boolean(report.currencyCodeSnapshot || report.budgetAmountSnapshot != null);
  const projectName = text(report.projectNameSnapshot, fallback);
  const projectCode = text(report.projectCodeSnapshot, fallback);
  const reportStatus = message(`projects.statusReportStates.${report.reportState}`, displayEnumLabel(report.reportState, labels, locale));
  const progressValue = percentage(report.overallProgressPercent, locale);
  const content: PdfContent[] = [
    {
      columns: [
        { width: "*", stack: [
          { text: brand.product.name, color: "#1E4E79", bold: true, fontSize: 10, alignment, margin: [0, 0, 0, 3] },
          { text: projectName, color: "#172B3A", bold: true, fontSize: 22, alignment, margin: [0, 0, 0, 4] },
          { text: labels.report, color: "#64748B", fontSize: 11, alignment },
        ] },
        { width: 110, stack: [
          { text: labels.reportState, color: "#64748B", fontSize: 9, alignment },
          { text: reportStatus, color: "#15803D", bold: true, fontSize: 11, alignment, margin: [0, 4, 0, 0] },
        ] },
      ],
      columnGap: 18,
      margin: [0, 0, 0, 12],
    },
    sectionTitle(labels.projectInformation, alignment),
    { table: { rtl: isRtl, dontBreakRows: true, widths: ["*", "*", "*"], body: [
      [fieldCell(labels.projectCode, projectCode, alignment), fieldCell(labels.reportDate, formatReportDate(report.reportDate, locale, fallback), alignment), fieldCell(labels.preparedBy, text(report.preparedByNameSnapshot, fallback), alignment)],
      [fieldCell(labels.sourceCapturedAt, formatReportDateTime(report.sourceDataCapturedAt, locale, fallback), alignment), fieldCell(labels.publishedAt, formatReportDateTime(report.publishedAt, locale, fallback), alignment), {}],
    ] }, layout: "lightHorizontalLines" },
    sectionTitle(labels.reportingPeriodStatus, alignment),
    { table: { rtl: isRtl, dontBreakRows: true, widths: ["*", "*", "*"], body: [
      [fieldCell(labels.reportingPeriod, `${formatReportDate(report.periodFrom, locale, fallback)} – ${formatReportDate(report.periodTo, locale, fallback)}`, alignment), fieldCell(labels.statusDate, formatReportDate(report.statusDate || report.reportDate, locale, fallback), alignment), fieldCell(labels.projectStatus, localizedEnumMessage(locale, "enums.projectStatus", report.projectLifecycleStatusAtReport, displayEnumLabel(report.projectLifecycleStatusAtReport, labels, locale)), alignment)],
      [fieldCell(labels.overallHealth, healthLabel(report.overallHealth, labels, locale), alignment, statusColor(report.overallHealth)), fieldCell(labels.overallProgress, progressValue, alignment, "#1E4E79"), fieldCell(labels.costHealth, healthLabel(report.costHealth, labels, locale), alignment, statusColor(report.costHealth))],
      [fieldCell(labels.qualityHealth, healthLabel(report.qualityHealth, labels, locale), alignment, statusColor(report.qualityHealth)), fieldCell(labels.resourceUtilization, utilizationLabel(report.resourceHealth, labels, locale), alignment, utilizationColor(report.resourceHealth)), {}],
    ] }, layout: "lightHorizontalLines", margin: [0, 0, 0, 10] },
  ];

  const mainColumn: PdfContent[] = [];
  if (hasReportSection(report, "ExecutiveSummary")) {
    mainColumn.push(narrativeSection(labels.executiveSummary, report.executiveSummary, alignment, labels));
    mainColumn.push(narrativeSection(labels.keyAchievements, report.keyAchievements, alignment, labels));
  }
  if (hasReportSection(report, "PerformanceKPIs")) {
    mainColumn.push(sectionTitle(labels.progressPerformance, alignment));
    mainColumn.push(subsectionTitle(labels.performanceKpis, alignment));
    const performance = report.performanceSnapshot;
    const performanceCurrency = performance?.currency || currency;
    if (performance) {
      mainColumn.push({ text: `${performanceLabels.statusDate}: ${formatReportDate(performance.statusDate, locale, fallback)} · ${performanceLabels.quality}: ${localizedMessage(locale, `projects.reporting.quality.${performance.dataQuality.toLowerCase()}`, displayEnumLabel(performance.dataQuality, labels, locale))}`, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 7] });
    } else {
      mainColumn.push({ text: labels.unavailable, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 7] });
    }
    const metrics: Array<[string, string]> = [
      [labels.budget, money(report.budgetAmountSnapshot, performanceCurrency, locale, fallback)],
      [labels.actualCost, money(report.actualCostAmountSnapshot, performanceCurrency, locale, fallback)],
      [labels.committedCost, money(report.committedCostAmountSnapshot, performanceCurrency, locale, fallback)],
      [labels.forecastCost, money(report.forecastCostAmountSnapshot, performanceCurrency, locale, fallback)],
      [labels.costVariance, money(report.costVarianceAmountSnapshot, performanceCurrency, locale, fallback)],
    ];
    if (performance) metrics.push(
      [performanceLabels.bac, money(performance.budgetAtCompletion, performanceCurrency, locale, fallback)],
      [performanceLabels.pv, money(performance.plannedValue, performanceCurrency, locale, fallback)],
      [performanceLabels.ev, money(performance.earnedValue, performanceCurrency, locale, fallback)],
      [performanceLabels.ac, money(performance.actualCost, performanceCurrency, locale, fallback)],
      [performanceLabels.spi, isFiniteNumber(performance.schedulePerformanceIndex) ? formatNumberValue(performance.schedulePerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : fallback],
      [performanceLabels.cpi, isFiniteNumber(performance.costPerformanceIndex) ? formatNumberValue(performance.costPerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : fallback],
      [performanceLabels.sv, money(performance.scheduleVariance, performanceCurrency, locale, fallback)],
      [performanceLabels.cv, money(performance.costVariance, performanceCurrency, locale, fallback)],
      [labels.unallocatedProjectActualCost, money(performance.unallocatedProjectActualCost, performanceCurrency, locale, fallback)],
    );
    metrics.push([labels.reportingConfigurationVersion, report.reportingConfigurationVersionSnapshot == null ? fallback : String(report.reportingConfigurationVersionSnapshot)]);
    mainColumn.push({ table: { rtl: isRtl, dontBreakRows: true, widths: ["*", "auto"], body: metrics.map(([label, value]) => [cell(label, alignment), cell(value, isRtl ? "left" : "right", true)]) }, layout: "lightHorizontalLines" });
    if (performance?.dataQualityReasons.length) mainColumn.push({ text: `${labels.dataQualityReasons}: ${performance.dataQualityReasons.map((reason) => dataQualityReasonLabel(reason, locale)).join(" · ")}`, alignment, color: "#64748B", fontSize: 9, margin: [0, 7, 0, 0], unbreakable: true });
  }

  if (hasReportSection(report, "PlanPerformance")) {
    const plans = report.performanceSnapshot?.plans.filter((plan) => plan.includedInOfficialReporting) ?? [];
    const planCurrency = report.performanceSnapshot?.currency || currency;
    mainColumn.push(snapshotTable(planPerformanceTitle, [
      performanceLabels.plan,
      performanceLabels.bac,
      performanceLabels.pv,
      performanceLabels.ev,
      performanceLabels.ac,
      performanceLabels.spi,
      performanceLabels.cpi,
    ], plans.map((plan) => [
      cell(text(plan.planName, fallback), alignment),
      cell(money(plan.budgetAtCompletion, planCurrency, locale, fallback), isRtl ? "left" : "right"),
      cell(money(plan.plannedValue, planCurrency, locale, fallback), isRtl ? "left" : "right"),
      cell(money(plan.earnedValue, planCurrency, locale, fallback), isRtl ? "left" : "right"),
      cell(money(plan.actualCost, planCurrency, locale, fallback), isRtl ? "left" : "right"),
      cell(isFiniteNumber(plan.schedulePerformanceIndex) ? formatNumberValue(plan.schedulePerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : fallback, isRtl ? "left" : "right"),
      cell(isFiniteNumber(plan.costPerformanceIndex) ? formatNumberValue(plan.costPerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : fallback, isRtl ? "left" : "right"),
    ]), alignment, isRtl, labels, undefined, true, 10));
  }

  const financialColumn: PdfContent[] = [];
  if (hasFinancialSummary) {
    financialColumn.push(sectionTitle(labels.financialSummary, alignment));
    financialColumn.push({ table: { rtl: isRtl, dontBreakRows: true, widths: ["*", "auto"], body: [[cell(labels.currency, alignment), cell(text(currency, fallback), isRtl ? "left" : "right", true)]] }, layout: "lightHorizontalLines" });
    financialColumn.push({ svg: financialDonutSvg(report.actualCostAmountSnapshot, report.committedCostAmountSnapshot), width: 120, height: 120, alignment: "center", margin: [0, 7, 0, 1] });
    financialColumn.push({ text: labels.approvedBudget, alignment: "center", color: "#697586", fontSize: 9, margin: [0, 0, 0, 2] });
    financialColumn.push({ text: money(report.budgetAmountSnapshot, currency, locale, fallback), alignment: "center", color: "#202833", bold: true, fontSize: 11, margin: [0, 0, 0, 5] });
    financialColumn.push({ table: { rtl: isRtl, dontBreakRows: true, widths: ["*", "auto"], body: [
      [{ ...cell(labels.actualPaid, alignment), color: "#2468A8" }, cell(money(report.actualCostAmountSnapshot, currency, locale, fallback), isRtl ? "left" : "right", true)],
      [{ ...cell(labels.outstandingPlannedPayments, alignment), color: "#104AC6" }, cell(money(report.committedCostAmountSnapshot, currency, locale, fallback), isRtl ? "left" : "right", true)],
      [cell(labels.costVariance, alignment), cell(money(report.costVarianceAmountSnapshot, currency, locale, fallback), isRtl ? "left" : "right", true)],
    ] }, layout: "lightHorizontalLines" });
    financialColumn.push({ text: message("projects.financialDonutNote", locale === "ar" ? "تقارن الحلقة بين المدفوع فعليًا والمدفوعات المخططة المستحقة؛ ولا تمثل القطاعات حصصًا من الميزانية المعتمدة." : "The ring compares actual paid with outstanding planned payments; the slices are not shares of the approved budget."), alignment, color: "#64748B", fontSize: 9, lineHeight: 1.25, margin: [0, 6, 0, 0], unbreakable: true });
  }
  if (mainColumn.length && financialColumn.length) {
    const main = { width: "*", stack: mainColumn };
    const financial = { width: 175, stack: financialColumn };
    content.push({ columns: [main, financial], columnGap: 18, margin: [0, 6, 0, 0] });
  } else if (mainColumn.length) {
    content.push(...mainColumn);
  } else if (financialColumn.length) {
    content.push(...financialColumn);
  }

  if (hasReportSection(report, "Milestones") && report.performanceSnapshot) {
    const milestones = report.performanceSnapshot.milestones;
    content.push(snapshotTable(labels.milestones, [performanceLabels.plan, localizedMessage(locale, "projects.statusReportComparison.milestone", "Milestone"), labels.baseline, labels.plannedDate, labels.actualDate, labels.variance, labels.status], milestones.map((item) => [
      cell(item.planName, alignment), cell(item.name, alignment), cell(formatReportDate(item.baselineDate, locale, fallback), alignment), cell(formatReportDate(item.plannedDate, locale, fallback), alignment),
      cell(formatReportDate(item.actualCompletionDate, locale, fallback), alignment), cell(item.varianceDays == null ? fallback : `${item.varianceDays} ${labels.days}`, alignment), cell(localizedMessage(locale, `projects.statusReportComparison.milestoneStatuses.${item.status}`, displayEnumLabel(item.status, labels, locale)), alignment),
    ]), alignment, isRtl, labels, ["12%", "20%", "14%", "14%", "14%", "12%", "14%"], true, 12));
  }
  if (hasReportSection(report, "RisksIssues")) {
    content.push(compactSectionTitle(labels.risksIssues, alignment));
    const risks = snapshotTable(`${labels.keyRisks} (${report.risks.length})`, [labels.title, labels.level, labels.status, labels.owner], (report.risks ?? []).map((item: ProjectStatusReportRiskSnapshotDto) => [cell(text(item.title, fallback), alignment), cell(localizedEnumMessage(locale, "enums.riskLevel", item.riskLevel, displayEnumLabel(item.riskLevel, labels, locale)), alignment), cell(localizedEnumMessage(locale, "enums.riskStatus", item.riskStatus, displayEnumLabel(item.riskStatus, labels, locale)), alignment), cell(text(item.ownerNameSnapshot, fallback), alignment)]), alignment, isRtl, labels, ["38%", "20%", "20%", "22%"], true);
    const issues = snapshotTable(`${labels.keyIssues} (${report.issues.length})`, [labels.title, labels.severity, labels.status, labels.assignee], (report.issues ?? []).map((item: ProjectStatusReportIssueSnapshotDto) => [cell(text(item.title, fallback), alignment), cell(localizedMessage(locale, `workItems.severity${item.severity}`, displayEnumLabel(item.severity, labels, locale)), alignment), cell(workItemStatusLabel(item.issueStatus, input.workItemStatuses, locale, fallback), alignment), cell(text(item.assigneeNameSnapshot, fallback), alignment)]), alignment, isRtl, labels, ["38%", "20%", "20%", "22%"], true);
    content.push({ columns: isRtl ? [{ width: "*", stack: [issues] }, { width: "*", stack: [risks] }] : [{ width: "*", stack: [risks] }, { width: "*", stack: [issues] }], columnGap: 18, margin: [0, 0, 0, 4] });
  }
  const includesExecutiveSummary = hasReportSection(report, "ExecutiveSummary");
  const challengesFirstItem = includesExecutiveSummary
    ? narrative(labels.challengesConcerns, report.challengesConcerns, alignment, labels)
    : narrative(labels.supportNeeded, report.supportNeeded, alignment, labels);
  content.push({ stack: [sectionTitle(labels.challenges, alignment), challengesFirstItem], unbreakable: true });
  if (includesExecutiveSummary) content.push({ ...narrative(labels.supportNeeded, report.supportNeeded, alignment, labels), unbreakable: true });
  const nextStepsFirstItem = includesExecutiveSummary
    ? narrative(labels.nextPriorities, report.nextPeriodPriorities, alignment, labels)
    : narrative(labels.actionsNeeded, report.actionsNeeded, alignment, labels);
  content.push({ stack: [sectionTitle(labels.nextStepsActions, alignment), nextStepsFirstItem], unbreakable: true });
  if (includesExecutiveSummary) content.push({ ...narrative(labels.actionsNeeded, report.actionsNeeded, alignment, labels), unbreakable: true });

  if (hasReportSection(report, "DecisionsRequired")) {
    const pending = (report.changeRequests ?? []).filter((item) => item.changeRequestStatus === "Pending");
    content.push({ stack: [sectionTitle(labels.decisionsRequired, alignment), { text: labels.decisionsSourceNote, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 7] }], unbreakable: true });
    if (pending.length) content.push(snapshotTable("", [message("projects.changeRequest", locale === "ar" ? "طلب التغيير" : "Change Request"), labels.status, labels.requestedBy, labels.requestedAt], pending.map((item) => [
      { stack: [
        { text: `#${item.number} · ${text(item.reason, fallback)}`, bold: true, alignment, margin: [0, 0, 0, 4] },
        ...(item.justification?.trim() ? [{ text: item.justification.trim(), alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 4] }] : []),
        ...(item.impact?.trim() ? [{ text: `${labels.impact}: ${item.impact.trim()}`, alignment, fontSize: 9 }] : []),
      ], margin: [6, 5, 6, 5] },
      cell(labels.pendingDecision, alignment), cell(text(item.requestedByNameSnapshot, fallback), alignment), cell(formatReportDate(item.requestedAt, locale, fallback), alignment),
    ]), alignment, isRtl, labels, ["42%", "20%", "20%", "18%"]));
    else content.push({ text: labels.noDecisionsRequired, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 10], unbreakable: true });
  }

  return { rtl: isRtl, pageSize: "A4", pageMargins: [38, 34, 38, 42], defaultStyle: { font: isRtl ? "Cairo" : "Roboto", fontSize: 10, color: "#202833", lineHeight: 1.25 }, styles: { sectionTitle: { fontSize: 14, bold: true, color: "#1E4E79" } }, info: { title: `${labels.report} - ${projectName}`, author: brand.product.fullName }, content, footer: (currentPage: number, pageCount: number) => ({ text: `${projectCode} | ${formatReportDate(report.reportDate, locale, fallback)} | ${labels.page} ${currentPage} ${labels.of} ${pageCount}`, alignment: "center", fontSize: 9, color: "#697586", margin: [0, 12, 0, 0] }) };
}

function safeFilenamePart(value: string | undefined, fallback: string): string { return text(value, fallback).replace(/[^a-zA-Z0-9._-]+/g, "_").replace(/^\.+|\.+$/g, "") || fallback; }
export function projectStatusReportPdfFilename(report: ProjectStatusReportDto): string { return `ProjectStatus_${safeFilenamePart(report.projectCodeSnapshot, "Project")}_${safeFilenamePart(report.reportDate?.slice(0, 10), "Report")}.pdf`; }

export async function exportProjectStatusReportPdf(input: ProjectStatusReportPdfInput): Promise<void> {
  if (input.report.reportState !== "Published") throw new Error("Only published status reports can be exported.");
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(vfsModule.default);
  pdfMake.addFonts({ Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" }, Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" } });
  const blob = await pdfMake.createPdf(buildProjectStatusReportDocument(input)).getBlob();
  const url = URL.createObjectURL(blob);
  const revokeObjectUrl = typeof URL.revokeObjectURL === "function" ? URL.revokeObjectURL.bind(URL) : undefined;
  const anchor = document.createElement("a"); anchor.href = url; anchor.download = projectStatusReportPdfFilename(input.report); document.body.appendChild(anchor); anchor.click(); document.body.removeChild(anchor);
  window.setTimeout(() => revokeObjectUrl?.(url), 1000);
}

export async function printProjectStatusReport(input: ProjectStatusReportPdfInput): Promise<void> {
  if (input.report.reportState !== "Published") throw new Error("Only published status reports can be printed.");
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(vfsModule.default);
  pdfMake.addFonts({ Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" }, Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" } });
  pdfMake.createPdf(buildProjectStatusReportDocument(input)).print();
}

const EXCEL_LABELS = {
  en: {
    summary: "Report Summary", project: "Project", code: "Project Code", reportDate: "Report Date", statusDate: "Status Date", period: "Reporting Period", state: "State", health: "Health", progress: "Progress", executiveSummary: "Executive Summary", achievements: "Key Achievements", challenges: "Challenges / Concerns", nextPriorities: "Next Period Priorities", capturedAt: "Source Data Captured At", configVersion: "Reporting Configuration Version", includedSections: "Included Sections", performance: "Project Performance", metric: "Metric", value: "Value", currency: "Currency", quality: "Data Quality", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", spi: "SPI", cpi: "CPI", sv: "SV", cv: "CV", unallocated: "Unallocated Actual Cost", plans: "Plan Performance", milestones: "Milestones", milestone: "Milestone", baselineDate: "Baseline Date", plannedDate: "Current Planned Date", actualDate: "Actual Completion Date", varianceDays: "Variance (days)", plan: "Plan", plannedProgress: "Planned Progress", actualProgress: "Actual Progress", baseline: "Baseline", reasons: "Data Quality Reasons", risks: "Risks", issues: "Issues", decisions: "Decisions Required", title: "Title", status: "Status", owner: "Owner", severity: "Severity", priority: "Priority", assignee: "Assignee", number: "Number", reason: "Reason", justification: "Justification", impact: "Impact", requestedBy: "Requested By", requestedAt: "Requested At", missing: "Unavailable", report: "PROJECT STATUS REPORT",
  },
  ar: {
    summary: "ملخص التقرير", project: "المشروع", code: "رمز المشروع", reportDate: "تاريخ التقرير", statusDate: "تاريخ الحالة", period: "فترة التقرير", state: "الحالة", health: "الصحة", progress: "التقدم", executiveSummary: "الملخص التنفيذي", achievements: "أهم الإنجازات", challenges: "التحديات والمخاوف", nextPriorities: "أولويات الفترة القادمة", capturedAt: "وقت التقاط البيانات المصدر", configVersion: "إصدار إعداد التقارير", includedSections: "الأقسام المضمنة", performance: "أداء المشروع", metric: "المؤشر", value: "القيمة", currency: "العملة", quality: "جودة البيانات", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", spi: "SPI", cpi: "CPI", sv: "SV", cv: "CV", unallocated: "التكلفة الفعلية غير المخصصة", plans: "أداء الخطط", milestones: "المعالم", milestone: "المعلم", baselineDate: "تاريخ خط الأساس", plannedDate: "التاريخ المخطط الحالي", actualDate: "تاريخ الإكمال الفعلي", varianceDays: "الانحراف (أيام)", plan: "الخطة", plannedProgress: "التقدم المخطط", actualProgress: "التقدم الفعلي", baseline: "خط الأساس", reasons: "أسباب جودة البيانات", risks: "المخاطر", issues: "المشكلات", decisions: "القرارات المطلوبة", title: "العنوان", status: "الحالة", owner: "المالك", severity: "الحدة", priority: "الأولوية", assignee: "المسؤول", number: "الرقم", reason: "السبب", justification: "المبرر", impact: "الأثر", requestedBy: "مقدم الطلب", requestedAt: "تاريخ الطلب", missing: "غير متاح", report: "تقرير حالة المشروع",
  },
} as const;

export interface ProjectStatusReportExcelInput {
  report: ProjectStatusReportDto;
  locale?: string;
}

export function projectStatusReportExcelFilename(report: ProjectStatusReportDto): string {
  return projectStatusReportPdfFilename(report).replace(/\.pdf$/i, ".xlsx");
}

export async function buildProjectStatusReportWorkbook(input: ProjectStatusReportExcelInput): Promise<import("exceljs").Workbook> {
  const { report } = input;
  if (report.reportState !== "Published") throw new Error("Only published status reports can be exported.");

  const locale = input.locale === "ar" ? "ar" : "en";
  const labels = EXCEL_LABELS[locale];
  const enumText = (value: string | undefined) => ENUM_LABELS[locale][normalize(value) as keyof (typeof ENUM_LABELS)[typeof locale]] ?? labels.missing;
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  workbook.creator = brand.product.fullName;
  workbook.subject = `${labels.report} ${report.projectCodeSnapshot ?? ""}`;
  workbook.created = new Date();

  const addSheet = (name: string, headers: string[], rows: Array<Array<string | number | null | undefined>>) => {
    const worksheet = workbook.addWorksheet(name.slice(0, 31), { views: [{ state: "frozen", ySplit: 1 }] });
    const header = worksheet.addRow(headers);
    header.font = { bold: true, color: { argb: "FFFFFFFF" }, size: 11 };
    header.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF0F4C5C" } };
    header.alignment = { vertical: "middle", horizontal: locale === "ar" ? "right" : "left" };
    header.height = 22;
    rows.forEach((row) => worksheet.addRow(row));
    worksheet.views = [{ state: "frozen", ySplit: 1, rightToLeft: locale === "ar" }];
    worksheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: Math.max(1, rows.length + 1), column: headers.length } };
    headers.forEach((value, index) => {
      const column = worksheet.getColumn(index + 1);
      let width = Math.max(12, Math.min(42, value.length + 3));
      column.eachCell({ includeEmpty: false }, (current) => { width = Math.max(width, Math.min(42, String(current.value ?? "").length + 2)); });
      column.width = width;
      column.alignment = { vertical: "middle", horizontal: locale === "ar" ? "right" : "left", wrapText: true };
    });
    worksheet.eachRow((row, rowNumber) => { if (rowNumber > 1 && rowNumber % 2 === 0) row.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF1F5F9" } }; });
    return worksheet;
  };

  const performance = report.performanceSnapshot;
  const included = includedReportSections(report);
  const hasSection = (section: ReportSection) => included.includes(section);
  const currency = performance?.currency || report.currencyCodeSnapshot || "";
  const percentageValue = (value?: number | null) => typeof value === "number" && Number.isFinite(value) ? value : labels.missing;
  const summaryRows: Array<Array<string | number | null | undefined>> = [
    [labels.report, labels.report], [labels.project, report.projectNameSnapshot ?? labels.missing], [labels.code, report.projectCodeSnapshot ?? labels.missing],
    [labels.reportDate, report.reportDate], [labels.statusDate, report.statusDate || report.reportDate], [labels.period, `${report.periodFrom} – ${report.periodTo}`],
    [labels.state, report.reportState], [labels.health, report.overallHealth ?? labels.missing], [labels.progress, percentageValue(report.overallProgressPercent)],
    [labels.currency, currency || labels.missing], [labels.capturedAt, report.sourceDataCapturedAt ?? labels.missing],
    [labels.configVersion, report.reportingConfigurationVersionSnapshot ?? labels.missing],
    [labels.includedSections, included.join(", ")],
  ];
  if (hasSection("ExecutiveSummary")) summaryRows.push(
    [labels.executiveSummary, report.executiveSummary ?? ""], [labels.achievements, report.keyAchievements ?? ""],
    [labels.challenges, report.challengesConcerns ?? ""], [labels.nextPriorities, report.nextPeriodPriorities ?? ""],
  );
  addSheet(labels.summary, [labels.metric, labels.value], summaryRows);

  if (performance && hasSection("PerformanceKPIs")) {
    addSheet(labels.performance, [labels.metric, labels.value], [
      [labels.statusDate, performance.statusDate], [labels.quality, performance.dataQuality], [labels.currency, currency || labels.missing],
      [labels.bac, percentageValue(performance.budgetAtCompletion)], [labels.pv, percentageValue(performance.plannedValue)],
      [labels.ev, percentageValue(performance.earnedValue)], [labels.ac, percentageValue(performance.actualCost)],
      [labels.spi, percentageValue(performance.schedulePerformanceIndex)], [labels.cpi, percentageValue(performance.costPerformanceIndex)],
      [labels.sv, percentageValue(performance.scheduleVariance)], [labels.cv, percentageValue(performance.costVariance)],
      [labels.unallocated, percentageValue(performance.unallocatedProjectActualCost)],
      [labels.reasons, performance.dataQualityReasons.join(", ") || labels.missing],
    ]);
  }
  if (performance && hasSection("PlanPerformance")) {
    addSheet(labels.plans, [labels.plan, labels.baseline, labels.actualProgress, labels.plannedProgress, labels.bac, labels.pv, labels.ev, labels.ac, labels.spi, labels.cpi, labels.quality], performance.plans.filter((plan) => plan.includedInOfficialReporting).map((plan) => [
      plan.planName, plan.baselineName ?? labels.missing, percentageValue(plan.actualProgressPercent), percentageValue(plan.plannedProgressPercent),
      percentageValue(plan.budgetAtCompletion), percentageValue(plan.plannedValue), percentageValue(plan.earnedValue), percentageValue(plan.actualCost),
      percentageValue(plan.schedulePerformanceIndex), percentageValue(plan.costPerformanceIndex), plan.dataQualityReasons.join(", ") || labels.missing,
    ]));
  }
  if (performance && hasSection("Milestones")) {
    addSheet(labels.milestones, [labels.plan, labels.milestone, labels.baselineDate, labels.plannedDate, labels.actualDate, labels.varianceDays, labels.status], performance.milestones.map((milestone) => [
      milestone.planName, milestone.name, milestone.baselineDate ?? labels.missing, milestone.plannedDate ?? labels.missing,
      milestone.actualCompletionDate ?? labels.missing, percentageValue(milestone.varianceDays), enumText(milestone.status),
    ]));
  }

  if (hasSection("RisksIssues")) {
    addSheet(labels.risks, [labels.title, labels.severity, labels.status, labels.owner], (report.risks ?? []).map((item) => [item.title, enumText(item.riskLevel), enumText(item.riskStatus), item.ownerNameSnapshot ?? ""]));
    addSheet(labels.issues, [labels.title, labels.severity, labels.priority, labels.status, labels.assignee], (report.issues ?? []).map((item) => [item.title, enumText(item.severity), enumText(item.priority), enumText(item.issueStatus), item.assigneeNameSnapshot ?? ""]));
  }
  if (hasSection("DecisionsRequired")) {
    addSheet(labels.decisions, [labels.number, labels.reason, labels.justification, labels.impact, labels.status, labels.requestedBy, labels.requestedAt], (report.changeRequests ?? []).filter((item) => item.changeRequestStatus === "Pending").map((item) => [item.number, item.reason, item.justification ?? "", item.impact ?? "", enumText(item.changeRequestStatus), item.requestedByNameSnapshot ?? "", item.requestedAt]));
  }

  return workbook;
}

export async function exportProjectStatusReportExcel(input: ProjectStatusReportExcelInput): Promise<void> {
  const workbook = await buildProjectStatusReportWorkbook(input);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = projectStatusReportExcelFilename(input.report);
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
