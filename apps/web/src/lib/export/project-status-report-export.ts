import type {
  ProjectStatusReport,
  ProjectStatusReportListDto,
  StatusReportChangeRequestSnapshot,
  StatusReportIssueSnapshot,
  StatusReportRiskSnapshot,
} from "@/hooks/projects/useProjectStatusReports";
import { getStatusTone, statusTonePdfColors } from "@/lib/status-presentation";
import { formatCurrency, formatDate, formatNullablePercentage, isFiniteNumber } from "@/lib/utils";

type PdfAlignment = "left" | "right" | "center";
type PdfMargin = [number, number, number, number];
type PdfCell = { text?: string; stack?: unknown[]; bold?: boolean; color?: string; fillColor?: string; alignment?: PdfAlignment; margin?: PdfMargin; fontSize?: number; colSpan?: number };
type PdfContent = PdfCell & { table?: { rtl?: boolean; headerRows?: number; widths?: unknown[]; body: PdfCell[][] }; layout?: unknown; columns?: unknown[]; style?: string | string[]; pageBreak?: string };

export interface ProjectStatusReportPdfInput {
  report: ProjectStatusReport;
  publishedHistory?: readonly ProjectStatusReportListDto[];
  locale?: string;
}

const LABELS = {
  en: {
    report: "PROJECT STATUS REPORT", projectName: "Project Name", projectCode: "Project Code", reportDate: "Report Date", reportingPeriod: "Reporting Period", projectManager: "Project Manager", overallHealth: "Overall Health", overallProgress: "Overall Progress", healthOverview: "PROJECT HEALTH OVERVIEW", schedule: "Schedule", costBudget: "Cost / Budget", scope: "Scope", quality: "Quality", resources: "Resources", progressOverview: "Progress Overview", reportHistory: "Published Report History", current: "Current", financialSummary: "Financial Summary", currency: "Currency", budget: "Budget", actualCost: "Actual Cost", committedCost: "Committed Cost", forecastCost: "Forecast Cost", costVariance: "Cost Variance", scheduleSummary: "Schedule / Baseline Summary", baseline: "Baseline", scheduleVarianceDays: "Schedule Variance (days)", scheduleVariancePercent: "Schedule Variance (%)", executiveSummary: "Executive Summary", keyAchievements: "Key Achievements", challenges: "Challenges / Concerns", nextPriorities: "Next Period Priorities", keyRisks: "Key Risks", keyIssues: "Key Issues", changeRequests: "Change Requests", title: "Title", level: "Level", status: "Status", owner: "Owner", severity: "Severity", priority: "Priority", assignee: "Assignee", number: "Number", type: "Type", reason: "Reason", requestedBy: "Requested By", preparedBy: "Prepared By", publishedBy: "Published By", publishedAt: "Published At", sourceCapturedAt: "Source Data Captured At", noData: "No snapshot data available", unavailable: "—", days: "days", page: "Page", of: "of", pmo: "NEXUS PMO",
  },
  ar: {
    report: "تقرير حالة المشروع", projectName: "اسم المشروع", projectCode: "رمز المشروع", reportDate: "تاريخ التقرير", reportingPeriod: "فترة التقرير", projectManager: "مدير المشروع", overallHealth: "الصحة العامة", overallProgress: "التقدم العام", healthOverview: "نظرة عامة على صحة المشروع", schedule: "الجدول الزمني", costBudget: "التكلفة / الميزانية", scope: "النطاق", quality: "الجودة", resources: "الموارد", progressOverview: "نظرة عامة على التقدم", reportHistory: "سجل التقارير المنشورة", current: "الحالي", financialSummary: "الملخص المالي", currency: "العملة", budget: "الميزانية", actualCost: "التكلفة الفعلية", committedCost: "التكلفة الملتزم بها", forecastCost: "التكلفة المتوقعة", costVariance: "انحراف التكلفة", scheduleSummary: "ملخص الجدول / خط الأساس", baseline: "خط الأساس", scheduleVarianceDays: "انحراف الجدول (أيام)", scheduleVariancePercent: "انحراف الجدول (%)", executiveSummary: "الملخص التنفيذي", keyAchievements: "الإنجازات الرئيسية", challenges: "التحديات / المخاوف", nextPriorities: "أولويات الفترة القادمة", keyRisks: "أهم المخاطر", keyIssues: "أهم المشكلات", changeRequests: "طلبات التغيير", title: "العنوان", level: "المستوى", status: "الحالة", owner: "المالك", severity: "الحدة", priority: "الأولوية", assignee: "المكلف", number: "الرقم", type: "النوع", reason: "السبب", requestedBy: "مقدم الطلب", preparedBy: "أعده", publishedBy: "نشره", publishedAt: "تاريخ النشر", sourceCapturedAt: "وقت التقاط البيانات", noData: "لا تتوفر بيانات لقطة", unavailable: "—", days: "يوم", page: "صفحة", of: "من", pmo: "NEXUS PMO",
  },
} as const;

type Labels = (typeof LABELS)[keyof typeof LABELS];

const HEALTH_LABELS = {
  en: { Green: "Green", Amber: "Amber", Red: "Red" },
  ar: { Green: "أخضر", Amber: "كهرماني", Red: "أحمر" },
} as const;

const ENUM_LABELS = {
  en: {
    critical: "Critical", high: "High", medium: "Medium", low: "Low", identified: "Identified", actionrequired: "Action Required", followup: "Follow Up", mitigated: "Mitigated", closed: "Closed", open: "Open", inprogress: "In Progress", blocked: "Blocked", resolved: "Resolved", pending: "Pending", approved: "Approved", rejected: "Rejected", cancelled: "Cancelled", administrative: "Administrative", baseline: "Baseline",
  },
  ar: {
    critical: "حرج", high: "مرتفع", medium: "متوسط", low: "منخفض", identified: "محدد", actionrequired: "يتطلب إجراء", followup: "متابعة", mitigated: "تمت المعالجة", closed: "مغلق", open: "مفتوح", inprogress: "قيد التنفيذ", blocked: "متوقف", resolved: "تم الحل", pending: "معلق", approved: "معتمد", rejected: "مرفوض", cancelled: "ملغى", administrative: "إداري", baseline: "خط الأساس",
  },
} as const;

function normalize(value: string | undefined): string {
  return (value ?? "").trim().toLowerCase().replace(/[\s_-]/g, "");
}

function text(value: unknown, fallback: string): string {
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function enumLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string {
  return ENUM_LABELS[locale][normalize(value) as keyof (typeof ENUM_LABELS)[typeof locale]] ?? labels.unavailable;
}

function healthLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string {
  return HEALTH_LABELS[locale][value as keyof (typeof HEALTH_LABELS)[typeof locale]] ?? labels.unavailable;
}

function statusColor(value: string | undefined): string {
  return statusTonePdfColors[getStatusTone(value)];
}

function finite(value: number | undefined | null): number | undefined {
  return isFiniteNumber(value) ? value : undefined;
}

function money(value: number | undefined, currency: string | undefined, locale: string, fallback: string): string {
  if (!isFiniteNumber(value)) return fallback;
  if (!currency?.trim()) return value.toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  try {
    return formatCurrency(value, currency, locale);
  } catch {
    return value.toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}

function cell(value: unknown, alignment: PdfAlignment, bold = false): PdfCell {
  return { text: String(value ?? ""), alignment, bold, margin: [6, 5, 6, 5] };
}

function sectionTitle(value: string, alignment: PdfAlignment): PdfContent {
  return { text: value, style: "sectionTitle", alignment, margin: [0, 14, 0, 7] };
}

function narrative(label: string, value: string | undefined, alignment: PdfAlignment, labels: Labels): PdfContent {
  return { table: { widths: ["*"], body: [[{ stack: [{ text: label, bold: true, color: "#0F4C5C", margin: [0, 0, 0, 5] }, { text: text(value, labels.unavailable), alignment, color: value?.trim() ? "#1E293B" : "#64748B" }], margin: [9, 8, 9, 8] }]] }, layout: "lightBox" };
}

function snapshotTable(
  title: string,
  headers: string[],
  rows: PdfCell[][],
  alignment: PdfAlignment,
  isRtl: boolean,
  labels: Labels,
): PdfContent {
  const body: PdfCell[][] = [headers.map((header) => ({ ...cell(header, alignment, true), color: "#FFFFFF", fillColor: "#0F4C5C" }))];
  const noDataRow: PdfCell[] = [{ ...cell(labels.noData, alignment), colSpan: headers.length }];
  noDataRow.push(...Array.from({ length: Math.max(0, headers.length - 1) }, () => ({})));
  body.push(...(rows.length ? rows : [noDataRow]));
  return { stack: [sectionTitle(title, alignment), { table: { rtl: isRtl, headerRows: 1, widths: headers.map(() => "*"), body }, layout: "lightGrid" }] };
}

function formatReportDate(value: string | undefined, locale: string, fallback: string): string {
  return formatDate(value, locale) || fallback;
}

function reportHistory(report: ProjectStatusReport, history: readonly ProjectStatusReportListDto[] | undefined): ProjectStatusReportListDto[] {
  const points = (history ?? []).filter((item) => item.reportState === "Published" && isFiniteNumber(item.overallProgressPercent));
  if (isFiniteNumber(report.overallProgressPercent) && !points.some((item) => item.id === report.id)) {
    points.push({ id: report.id, projectId: report.projectId, reportDate: report.reportDate, periodFrom: report.periodFrom, periodTo: report.periodTo, reportState: "Published", overallHealth: report.overallHealth, overallProgressPercent: report.overallProgressPercent, preparedByNameSnapshot: report.preparedByNameSnapshot, publishedAt: report.publishedAt });
  }
  return points.sort((a, b) => a.reportDate.localeCompare(b.reportDate));
}

export function buildProjectStatusReportDocument(input: ProjectStatusReportPdfInput): Record<string, unknown> {
  const { report, publishedHistory } = input;
  const locale = input.locale === "ar" ? "ar" : "en";
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const labels = LABELS[locale];
  const isRtl = locale === "ar";
  const alignment: PdfAlignment = isRtl ? "right" : "left";
  const font = isRtl ? "Cairo" : "Roboto";
  const fallback = labels.unavailable;
  const currency = report.currencyCodeSnapshot?.trim();
  const history = reportHistory(report, publishedHistory);
  const healthDimensions: Array<[string, string | undefined]> = [[labels.schedule, report.scheduleHealth], [labels.costBudget, report.costHealth], [labels.scope, report.scopeHealth], [labels.quality, report.qualityHealth], [labels.resources, report.resourceHealth]];
  const healthBody: PdfCell[][] = [healthDimensions.map(([label, value]) => ({ stack: [{ text: label, bold: true, color: "#475569", alignment }, { text: healthLabel(value, labels, locale), bold: true, color: statusColor(value), alignment, margin: [0, 5, 0, 0] }], margin: [7, 8, 7, 8], fillColor: "#F8FAFC" }))];
  const progress = finite(report.overallProgressPercent);
  const progressValue = progress === undefined ? fallback : formatNullablePercentage(progress, 1);
  const progressPercent = progress === undefined ? 0 : Math.max(0, Math.min(100, progress));
  const content: PdfContent[] = [
    { text: labels.pmo, color: "#0F4C5C", bold: true, fontSize: 10, alignment, margin: [0, 0, 0, 2] },
    { text: labels.report, color: "#0F172A", bold: true, fontSize: 20, alignment, margin: [0, 0, 0, 12] },
    { table: { rtl: isRtl, widths: ["*", "*", "*"], body: [[cell(`${labels.projectName}: ${text(report.projectNameSnapshot, fallback)}`, alignment), cell(`${labels.projectCode}: ${text(report.projectCodeSnapshot, fallback)}`, alignment), cell(`${labels.projectManager}: ${text(report.projectManagerNameSnapshot, fallback)}`, alignment)], [cell(`${labels.reportDate}: ${formatReportDate(report.reportDate, dateLocale, fallback)}`, alignment), cell(`${labels.reportingPeriod}: ${formatReportDate(report.periodFrom, dateLocale, fallback)} - ${formatReportDate(report.periodTo, dateLocale, fallback)}`, alignment), cell(`${labels.publishedAt}: ${formatReportDate(report.publishedAt, dateLocale, fallback)}`, alignment)]] }, layout: "lightGrid" },
    { table: { rtl: isRtl, widths: ["*", "*"], body: [[{ stack: [{ text: labels.overallHealth, color: "#475569", bold: true }, { text: healthLabel(report.overallHealth, labels, locale), color: statusColor(report.overallHealth), bold: true, fontSize: 16, margin: [0, 6, 0, 0] }], margin: [10, 10, 10, 10], fillColor: "#F8FAFC" }, { stack: [{ text: labels.overallProgress, color: "#475569", bold: true }, { text: progressValue, color: "#0F4C5C", bold: true, fontSize: 16, margin: [0, 6, 0, 0] }], margin: [10, 10, 10, 10], fillColor: "#F8FAFC" }]] }, layout: "lightGrid", margin: [0, 12, 0, 0] },
    sectionTitle(labels.healthOverview, alignment),
    { table: { rtl: isRtl, widths: ["*", "*", "*", "*", "*"], body: healthBody }, layout: "lightGrid" },
    sectionTitle(labels.executiveSummary, alignment),
    narrative(labels.executiveSummary, report.executiveSummary, alignment, labels),
    sectionTitle(labels.keyAchievements, alignment),
    narrative(labels.keyAchievements, report.keyAchievements, alignment, labels),
    sectionTitle(labels.challenges, alignment),
    narrative(labels.challenges, report.challengesConcerns, alignment, labels),
    sectionTitle(labels.nextPriorities, alignment),
    narrative(labels.nextPriorities, report.nextPeriodPriorities, alignment, labels),
    sectionTitle(labels.progressOverview, alignment),
    { table: { rtl: isRtl, widths: [`${progressPercent}%`, `${100 - progressPercent}%`], body: [[{ text: "", fillColor: "#0F4C5C", margin: [0, 7, 0, 7] }, { text: "", fillColor: "#E2E8F0", margin: [0, 7, 0, 7] }]] }, layout: "noBorders" },
    { text: `${labels.current}: ${progressValue}`, alignment, bold: true, color: "#0F4C5C", margin: [0, 0, 0, 7] },
  ];
  if (history.length >= 2) {
    content.push({ stack: [sectionTitle(labels.reportHistory, alignment), { table: { rtl: isRtl, headerRows: 1, widths: ["*", "*"], body: [[cell(labels.reportDate, alignment, true), cell(labels.overallProgress, alignment, true)], ...history.map((item) => [cell(formatReportDate(item.reportDate, dateLocale, fallback), alignment), cell(formatNullablePercentage(item.overallProgressPercent, 1), alignment)])] }, layout: "lightGrid" }] });
  }
  content.push(sectionTitle(labels.financialSummary, alignment));
  content.push({ table: { rtl: isRtl, widths: ["*", "*", "*"], body: [[cell(labels.currency, alignment, true), cell(labels.budget, alignment, true), cell(labels.actualCost, alignment, true)], [cell(text(currency, fallback), alignment), cell(money(report.budgetAmountSnapshot, currency, dateLocale, fallback), alignment), cell(money(report.actualCostAmountSnapshot, currency, dateLocale, fallback), alignment)], [cell(labels.committedCost, alignment, true), cell(labels.forecastCost, alignment, true), cell(labels.costVariance, alignment, true)], [cell(money(report.committedCostAmountSnapshot, currency, dateLocale, fallback), alignment), cell(money(report.forecastCostAmountSnapshot, currency, dateLocale, fallback), alignment), cell(money(report.costVarianceAmountSnapshot, currency, dateLocale, fallback), alignment)]] }, layout: "lightGrid" });
  content.push(sectionTitle(labels.scheduleSummary, alignment));
  content.push({ table: { rtl: isRtl, widths: ["*", "*", "*"], body: [[cell(labels.baseline, alignment, true), cell(labels.scheduleVarianceDays, alignment, true), cell(labels.scheduleVariancePercent, alignment, true)], [cell(report.baselineNameSnapshot ? `${report.baselineNameSnapshot}${report.baselineVersionSnapshot == null ? "" : ` - v${report.baselineVersionSnapshot}`}` : fallback, alignment), cell(report.scheduleVarianceDaysSnapshot == null ? fallback : `${report.scheduleVarianceDaysSnapshot} ${labels.days}`, alignment), cell(formatNullablePercentage(report.scheduleVariancePercentSnapshot, 1, true), alignment)]] }, layout: "lightGrid" });
  content.push(snapshotTable(labels.keyRisks, [labels.title, labels.level, labels.status, labels.owner], report.risks.map((item: StatusReportRiskSnapshot) => [cell(text(item.title, fallback), alignment), cell(enumLabel(item.riskLevel, labels, locale), alignment), cell(enumLabel(item.riskStatus, labels, locale), alignment), cell(text(item.ownerNameSnapshot, fallback), alignment)]), alignment, isRtl, labels));
  content.push(snapshotTable(labels.keyIssues, [labels.title, labels.severity, labels.priority, labels.status, labels.assignee], report.issues.map((item: StatusReportIssueSnapshot) => [cell(text(item.title, fallback), alignment), cell(enumLabel(item.severity, labels, locale), alignment), cell(enumLabel(item.priority, labels, locale), alignment), cell(enumLabel(item.issueStatus, labels, locale), alignment), cell(text(item.assigneeNameSnapshot, fallback), alignment)]), alignment, isRtl, labels));
  content.push(snapshotTable(labels.changeRequests, [labels.number, labels.reason, labels.type, labels.status, labels.requestedBy], report.changeRequests.map((item: StatusReportChangeRequestSnapshot) => [cell(item.number == null ? fallback : item.number, alignment), cell(text(item.reason, fallback), alignment), cell(enumLabel(item.changeType, labels, locale), alignment), cell(enumLabel(item.changeRequestStatus, labels, locale), alignment), cell(text(item.requestedByNameSnapshot, fallback), alignment)]), alignment, isRtl, labels));
  content.push({ table: { rtl: isRtl, widths: ["*", "*", "*", "*"], body: [[cell(`${labels.preparedBy}: ${text(report.preparedByNameSnapshot, fallback)}`, alignment), cell(`${labels.publishedBy}: ${text(report.publishedByNameSnapshot, fallback)}`, alignment), cell(`${labels.publishedAt}: ${formatReportDate(report.publishedAt, dateLocale, fallback)}`, alignment), cell(`${labels.sourceCapturedAt}: ${formatReportDate(report.sourceDataCapturedAt, dateLocale, fallback)}`, alignment)]] }, layout: "lightGrid", margin: [0, 16, 0, 0] });
  return {
    rtl: isRtl,
    pageSize: "A4",
    pageMargins: [32, 32, 32, 42],
    defaultStyle: { font, fontSize: 9, color: "#1E293B" },
    styles: { sectionTitle: { fontSize: 12, bold: true, color: "#0F4C5C" } },
    info: { title: `${labels.report} - ${text(report.projectNameSnapshot, fallback)}`, author: labels.pmo },
    content,
    footer: (currentPage: number, pageCount: number) => ({ text: `${text(report.projectCodeSnapshot, fallback)} | ${formatReportDate(report.reportDate, dateLocale, fallback)} | ${labels.page} ${currentPage} ${labels.of} ${pageCount}`, alignment: "center", fontSize: 8, color: "#64748B", margin: [0, 12, 0, 0] }),
  };
}

function safeFilenamePart(value: string | undefined, fallback: string): string {
  return text(value, fallback).replace(/[^a-zA-Z0-9._-]+/g, "_").replace(/^\.+|\.+$/g, "") || fallback;
}

export function projectStatusReportPdfFilename(report: ProjectStatusReport): string {
  return `ProjectStatus_${safeFilenamePart(report.projectCodeSnapshot, "Project")}_${safeFilenamePart(report.reportDate?.slice(0, 10), "Report")}.pdf`;
}

export async function exportProjectStatusReportPdf(input: ProjectStatusReportPdfInput): Promise<void> {
  if (input.report.reportState !== "Published") throw new Error("Only published status reports can be exported.");
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default ?? pdfModule;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  const vfs = vfsModule.default ?? vfsModule;
  pdfMake.addVirtualFileSystem(vfs);
  pdfMake.addFonts({ Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" }, Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" } });
  const doc = pdfMake.createPdf(buildProjectStatusReportDocument(input));
  const blob = await doc.getBlob();
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = projectStatusReportPdfFilename(input.report);
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
