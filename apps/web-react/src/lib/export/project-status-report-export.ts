import type { ProjectStatusReportDto, ProjectStatusReportListDto, ProjectStatusReportRiskSnapshotDto, ProjectStatusReportIssueSnapshotDto } from "@features/projects/api/projects";

type PdfAlignment = "left" | "right" | "center";
type PdfMargin = [number, number, number, number];
type PdfCell = { text?: string; stack?: unknown[]; bold?: boolean; color?: string; fillColor?: string; alignment?: PdfAlignment; margin?: PdfMargin; fontSize?: number; colSpan?: number };
type PdfContent = PdfCell & { table?: { rtl?: boolean; headerRows?: number; keepWithHeaderRows?: number; widths?: unknown[]; body: PdfCell[][] }; layout?: unknown; columns?: unknown[]; style?: string | string[]; pageBreak?: string; unbreakable?: boolean };

export interface ProjectStatusReportPdfInput {
  report: ProjectStatusReportDto;
  publishedHistory?: readonly ProjectStatusReportListDto[];
  locale?: string;
}

const LABELS = {
  en: {
    report: "PROJECT STATUS REPORT", projectName: "Project Name", projectCode: "Project Code", reportDate: "Report Date", statusDate: "Status Date", reportingPeriod: "Reporting Period", projectManager: "Project Manager", overallHealth: "Overall Health", overallProgress: "Overall Progress", healthOverview: "PROJECT HEALTH OVERVIEW", schedule: "Schedule", costBudget: "Cost / Budget", scope: "Scope", quality: "Quality", resources: "Resources", progressOverview: "Progress Overview", reportHistory: "Published Report History", current: "Current", financialSummary: "Financial Summary", currency: "Currency", budget: "Budget", actualCost: "Actual Cost", committedCost: "Committed Cost", forecastCost: "Forecast Cost", costVariance: "Cost Variance", scheduleSummary: "Schedule / Baseline Summary", baseline: "Baseline", scheduleVarianceDays: "Schedule Variance (days)", scheduleVariancePercent: "Schedule Variance (%)", executiveSummary: "Executive Summary", keyAchievements: "Key Achievements", challenges: "Challenges / Concerns", nextPriorities: "Next Period Priorities", keyRisks: "Key Risks", keyIssues: "Key Issues", milestones: "Milestones", plannedDate: "Current planned date", actualDate: "Actual completion date", variance: "Variance", decisionsRequired: "Decisions Required", title: "Title", level: "Level", status: "Status", owner: "Owner", severity: "Severity", priority: "Priority", assignee: "Assignee", number: "Number", type: "Type", reason: "Reason", justification: "Justification", impact: "Impact", requestedBy: "Requested By", requestedAt: "Requested At", preparedBy: "Prepared By", publishedBy: "Published By", publishedAt: "Published At", sourceCapturedAt: "Source Data Captured At", noData: "No snapshot data available", unavailable: "â€”", days: "days", page: "Page", of: "of", pmo: "NEXUS PMO",
  },
  ar: {
    report: "ØªÙ‚Ø±ÙŠØ± Ø­Ø§Ù„Ø© Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", projectName: "Ø§Ø³Ù… Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", projectCode: "Ø±Ù…Ø² Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", reportDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„ØªÙ‚Ø±ÙŠØ±", statusDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø­Ø§Ù„Ø©", reportingPeriod: "ÙØªØ±Ø© Ø§Ù„ØªÙ‚Ø±ÙŠØ±", projectManager: "Ù…Ø¯ÙŠØ± Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", overallHealth: "Ø§Ù„ØµØ­Ø© Ø§Ù„Ø¹Ø§Ù…Ø©", overallProgress: "Ø§Ù„ØªÙ‚Ø¯Ù… Ø§Ù„Ø¹Ø§Ù…", healthOverview: "Ù†Ø¸Ø±Ø© Ø¹Ø§Ù…Ø© Ø¹Ù„Ù‰ ØµØ­Ø© Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", schedule: "Ø§Ù„Ø¬Ø¯ÙˆÙ„ Ø§Ù„Ø²Ù…Ù†ÙŠ", costBudget: "Ø§Ù„ØªÙƒÙ„ÙØ© / Ø§Ù„Ù…ÙŠØ²Ø§Ù†ÙŠØ©", scope: "Ø§Ù„Ù†Ø·Ø§Ù‚", quality: "Ø§Ù„Ø¬ÙˆØ¯Ø©", resources: "Ø§Ù„Ù…ÙˆØ§Ø±Ø¯", progressOverview: "Ù†Ø¸Ø±Ø© Ø¹Ø§Ù…Ø© Ø¹Ù„Ù‰ Ø§Ù„ØªÙ‚Ø¯Ù…", reportHistory: "Ø³Ø¬Ù„ Ø§Ù„ØªÙ‚Ø§Ø±ÙŠØ± Ø§Ù„Ù…Ù†Ø´ÙˆØ±Ø©", current: "Ø§Ù„Ø­Ø§Ù„ÙŠ", financialSummary: "Ø§Ù„Ù…Ù„Ø®Øµ Ø§Ù„Ù…Ø§Ù„ÙŠ", currency: "Ø§Ù„Ø¹Ù…Ù„Ø©", budget: "Ø§Ù„Ù…ÙŠØ²Ø§Ù†ÙŠØ©", actualCost: "Ø§Ù„ØªÙƒÙ„ÙØ© Ø§Ù„ÙØ¹Ù„ÙŠØ©", committedCost: "Ø§Ù„ØªÙƒÙ„ÙØ© Ø§Ù„Ù…Ù„ØªØ²Ù… Ø¨Ù‡Ø§", forecastCost: "Ø§Ù„ØªÙƒÙ„ÙØ© Ø§Ù„Ù…ØªÙˆÙ‚Ø¹Ø©", costVariance: "Ø§Ù†Ø­Ø±Ø§Ù Ø§Ù„ØªÙƒÙ„ÙØ©", scheduleSummary: "Ù…Ù„Ø®Øµ Ø§Ù„Ø¬Ø¯ÙˆÙ„ / Ø®Ø· Ø§Ù„Ø£Ø³Ø§Ø³", baseline: "Ø®Ø· Ø§Ù„Ø£Ø³Ø§Ø³", scheduleVarianceDays: "Ø§Ù†Ø­Ø±Ø§Ù Ø§Ù„Ø¬Ø¯ÙˆÙ„ (Ø£ÙŠØ§Ù…)", scheduleVariancePercent: "Ø§Ù†Ø­Ø±Ø§Ù Ø§Ù„Ø¬Ø¯ÙˆÙ„ (%)", executiveSummary: "Ø§Ù„Ù…Ù„Ø®Øµ Ø§Ù„ØªÙ†ÙÙŠØ°ÙŠ", keyAchievements: "Ø§Ù„Ø¥Ù†Ø¬Ø§Ø²Ø§Øª Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠØ©", challenges: "Ø§Ù„ØªØ­Ø¯ÙŠØ§Øª / Ø§Ù„Ù…Ø®Ø§ÙˆÙ", nextPriorities: "Ø£ÙˆÙ„ÙˆÙŠØ§Øª Ø§Ù„ÙØªØ±Ø© Ø§Ù„Ù‚Ø§Ø¯Ù…Ø©", keyRisks: "Ø£Ù‡Ù… Ø§Ù„Ù…Ø®Ø§Ø·Ø±", keyIssues: "Ø£Ù‡Ù… Ø§Ù„Ù…Ø´ÙƒÙ„Ø§Øª", milestones: "Ø§Ù„Ù…Ø¹Ø§Ù„Ù…", plannedDate: "Ø§Ù„ØªØ§Ø±ÙŠØ® Ø§Ù„Ù…Ø®Ø·Ø· Ø§Ù„Ø­Ø§Ù„ÙŠ", actualDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¥Ù†Ø¬Ø§Ø² Ø§Ù„ÙØ¹Ù„ÙŠ", variance: "Ø§Ù„Ø§Ù†Ø­Ø±Ø§Ù", decisionsRequired: "Ù‚Ø±Ø§Ø±Ø§Øª Ù…Ø·Ù„ÙˆØ¨Ø©", title: "Ø§Ù„Ø¹Ù†ÙˆØ§Ù†", level: "Ø§Ù„Ù…Ø³ØªÙˆÙ‰", status: "Ø§Ù„Ø­Ø§Ù„Ø©", owner: "Ø§Ù„Ù…Ø§Ù„Ùƒ", severity: "Ø§Ù„Ø­Ø¯Ø©", priority: "Ø§Ù„Ø£ÙˆÙ„ÙˆÙŠØ©", assignee: "Ø§Ù„Ù…ÙƒÙ„Ù", number: "Ø§Ù„Ø±Ù‚Ù…", type: "Ø§Ù„Ù†ÙˆØ¹", reason: "Ø§Ù„Ø³Ø¨Ø¨", justification: "Ø§Ù„Ù…Ø¨Ø±Ø±", impact: "Ø§Ù„Ø£Ø«Ø±", requestedBy: "Ù…Ù‚Ø¯Ù… Ø§Ù„Ø·Ù„Ø¨", requestedAt: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø·Ù„Ø¨", preparedBy: "Ø£Ø¹Ø¯Ù‡", publishedBy: "Ù†Ø´Ø±Ù‡", publishedAt: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†Ø´Ø±", sourceCapturedAt: "ÙˆÙ‚Øª Ø§Ù„ØªÙ‚Ø§Ø· Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª", noData: "Ù„Ø§ ØªØªÙˆÙØ± Ø¨ÙŠØ§Ù†Ø§Øª Ù„Ù‚Ø·Ø©", unavailable: "â€”", days: "ÙŠÙˆÙ…", page: "ØµÙØ­Ø©", of: "Ù…Ù†", pmo: "NEXUS PMO",
  },
} as const;

type Labels = (typeof LABELS)[keyof typeof LABELS];

const PERFORMANCE_LABELS = {
  en: { title: "Multi-plan EVM Snapshot", planPerformance: "Plan Performance", statusDate: "Status Date", quality: "Data Quality", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", sv: "SV", cv: "CV", spi: "SPI", cpi: "CPI", plan: "Plan", unavailable: "Unavailable" },
  ar: { title: "Ù„Ù‚Ø·Ø© EVM Ù…ØªØ¹Ø¯Ø¯Ø© Ø§Ù„Ø®Ø·Ø·", planPerformance: "Ø£Ø¯Ø§Ø¡ Ø§Ù„Ø®Ø·Ø·", statusDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø­Ø§Ù„Ø©", quality: "Ø¬ÙˆØ¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", sv: "SV", cv: "CV", spi: "SPI", cpi: "CPI", plan: "Ø§Ù„Ø®Ø·Ø©", unavailable: "ØºÙŠØ± Ù…ØªØ§Ø­" },
} as const;

const HEALTH_LABELS = { en: { Green: "Green", Amber: "Amber", Red: "Red" }, ar: { Green: "Ø£Ø®Ø¶Ø±", Amber: "ÙƒÙ‡Ø±Ù…Ø§Ù†ÙŠ", Red: "Ø£Ø­Ù…Ø±" } } as const;
const ENUM_LABELS = {
  en: { critical: "Critical", high: "High", medium: "Medium", low: "Low", identified: "Identified", actionrequired: "Action Required", followup: "Follow Up", mitigated: "Mitigated", closed: "Closed", open: "Open", inprogress: "In Progress", blocked: "Blocked", resolved: "Resolved", pending: "Pending", approved: "Approved", rejected: "Rejected", cancelled: "Cancelled", administrative: "Administrative", baseline: "Baseline", completed: "Completed", delayed: "Delayed", upcoming: "Upcoming" },
  ar: { critical: "Ø­Ø±Ø¬", high: "Ù…Ø±ØªÙØ¹", medium: "Ù…ØªÙˆØ³Ø·", low: "Ù…Ù†Ø®ÙØ¶", identified: "Ù…Ø­Ø¯Ø¯", actionrequired: "ÙŠØªØ·Ù„Ø¨ Ø¥Ø¬Ø±Ø§Ø¡", followup: "Ù…ØªØ§Ø¨Ø¹Ø©", mitigated: "ØªÙ…Øª Ø§Ù„Ù…Ø¹Ø§Ù„Ø¬Ø©", closed: "Ù…ØºÙ„Ù‚", open: "Ù…ÙØªÙˆØ­", inprogress: "Ù‚ÙŠØ¯ Ø§Ù„ØªÙ†ÙÙŠØ°", blocked: "Ù…ØªÙˆÙ‚Ù", resolved: "ØªÙ… Ø§Ù„Ø­Ù„", pending: "Ù…Ø¹Ù„Ù‚", approved: "Ù…Ø¹ØªÙ…Ø¯", rejected: "Ù…Ø±ÙÙˆØ¶", cancelled: "Ù…Ù„ØºÙ‰", administrative: "Ø¥Ø¯Ø§Ø±ÙŠ", baseline: "Ø®Ø· Ø§Ù„Ø£Ø³Ø§Ø³", completed: "Ù…ÙƒØªÙ…Ù„", delayed: "Ù…ØªØ£Ø®Ø±", upcoming: "Ù‚Ø§Ø¯Ù…" },
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
function enumLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string { return ENUM_LABELS[locale][normalize(value) as keyof (typeof ENUM_LABELS)[typeof locale]] ?? labels.unavailable; }
function healthLabel(value: string | undefined, labels: Labels, locale: "en" | "ar"): string { return HEALTH_LABELS[locale][value as keyof (typeof HEALTH_LABELS)[typeof locale]] ?? labels.unavailable; }
function statusColor(value: string | undefined): string { const normalized = normalize(value); return normalized === "green" ? "#15803D" : normalized === "amber" ? "#B45309" : normalized === "red" ? "#B91C1C" : "#697586"; }
function percentage(value: number | undefined | null, digits = 1, withSign = false): string { if (!isFiniteNumber(value)) return "â€”"; const prefix = withSign && value > 0 ? "+" : ""; return `${prefix}${value.toFixed(digits).replace(/\.0+$/, "").replace(/(\.\d*?)0+$/, "$1")}%`; }
function formatReportDate(value: string | null | undefined, locale: "en" | "ar", fallback: string): string { if (!value) return fallback; const date = new Date(value); return Number.isNaN(date.getTime()) ? fallback : new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", { dateStyle: "medium", timeZone: "UTC" }).format(date); }
function money(value: number | undefined, currency: string | undefined, locale: "en" | "ar", fallback: string): string { if (!isFiniteNumber(value)) return fallback; try { const formatted = new Intl.NumberFormat("en-US", { style: "currency", currency: currency?.trim() || "USD", minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value); return locale === "ar" ? `\u200E${formatted}\u200E` : formatted; } catch { const formatted = value.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }); return locale === "ar" ? `\u200E${formatted}\u200E` : formatted; } }
function cell(value: unknown, alignment: PdfAlignment, bold = false): PdfCell { return { text: String(value ?? ""), alignment, bold, margin: [6, 5, 6, 5] }; }
function sectionTitle(value: string, alignment: PdfAlignment): PdfContent { return { text: value, style: "sectionTitle", alignment, margin: [0, 14, 0, 7] }; }
function narrative(label: string, value: string | undefined, alignment: PdfAlignment, labels: Labels): PdfContent { return { table: { widths: ["*"], body: [[{ stack: [{ text: label, bold: true, color: "#1E4E79", margin: [0, 0, 0, 5] }, { text: text(value, labels.unavailable), alignment, color: value?.trim() ? "#202833" : "#697586" }], margin: [9, 8, 9, 8] }]] }, layout: "lightBox" }; }
function snapshotTable(title: string, headers: string[], rows: PdfCell[][], alignment: PdfAlignment, isRtl: boolean, labels: Labels): PdfContent { const dataRows = rows.length ? rows : [[{ ...cell(labels.noData, alignment), colSpan: headers.length }, ...Array.from({ length: Math.max(0, headers.length - 1) }, () => ({}))]]; const body: PdfCell[][] = [headers.map((header) => ({ ...cell(header, alignment, true), color: "#FFFFFF", fillColor: "#1E4E79" })), ...dataRows]; return { stack: [sectionTitle(title, alignment), { table: { rtl: isRtl, headerRows: 1, keepWithHeaderRows: 1, widths: headers.map(() => "*"), body }, layout: "lightGrid" }], ...(dataRows.length <= 3 ? { unbreakable: true } : {}) }; }
function narrativeSection(title: string, value: string | undefined, alignment: PdfAlignment, labels: Labels): PdfContent { return { stack: [sectionTitle(title, alignment), narrative(title, value, alignment, labels)], unbreakable: true }; }
function reportHistory(report: ProjectStatusReportDto, history: readonly ProjectStatusReportListDto[] | undefined): ProjectStatusReportListDto[] { const points = (history ?? []).filter((item) => item.reportState === "Published" && isFiniteNumber(item.overallProgressPercent)); if (isFiniteNumber(report.overallProgressPercent) && !points.some((item) => item.id === report.id)) points.push({ id: report.id, projectId: report.projectId, reportDate: report.reportDate, periodFrom: report.periodFrom, periodTo: report.periodTo, statusDate: report.statusDate || report.reportDate, reportState: "Published", overallHealth: report.overallHealth, overallProgressPercent: report.overallProgressPercent, preparedByNameSnapshot: report.preparedByNameSnapshot, publishedAt: report.publishedAt }); return points.sort((a, b) => a.reportDate.localeCompare(b.reportDate)); }

export function buildProjectStatusReportDocument(input: ProjectStatusReportPdfInput): Record<string, unknown> {
  const { report, publishedHistory } = input;
  const locale = input.locale === "ar" ? "ar" : "en";
  const labels = LABELS[locale];
  const performanceLabels = PERFORMANCE_LABELS[locale];
  const isRtl = locale === "ar";
  const alignment: PdfAlignment = isRtl ? "right" : "left";
  const fallback = labels.unavailable;
  const currency = report.currencyCodeSnapshot?.trim();
  const history = reportHistory(report, publishedHistory);
  const healthDimensions: Array<[string, string | undefined]> = [[labels.schedule, report.scheduleHealth], [labels.costBudget, report.costHealth], [labels.scope, report.scopeHealth], [labels.quality, report.qualityHealth], [labels.resources, report.resourceHealth]];
  const healthBody: PdfCell[][] = [healthDimensions.map(([label, value]) => ({ stack: [{ text: label, bold: true, color: "#697586", alignment }, { text: healthLabel(value, labels, locale), bold: true, color: statusColor(value), alignment, margin: [0, 5, 0, 0] }], margin: [7, 8, 7, 8], fillColor: "#F8FAFC" }))];
  const progress = isFiniteNumber(report.overallProgressPercent) ? report.overallProgressPercent : undefined;
  const progressValue = progress === undefined ? fallback : percentage(progress);
  const progressPercent = progress === undefined ? 0 : Math.max(0, Math.min(100, progress));
  const content: PdfContent[] = [
    { text: labels.pmo, color: "#1E4E79", bold: true, fontSize: 10, alignment, margin: [0, 0, 0, 2] },
    { text: labels.report, color: "#202833", bold: true, fontSize: 20, alignment, margin: [0, 0, 0, 12] },
    { table: { rtl: isRtl, widths: ["*", "*", "*", "*"], body: [[cell(`${labels.projectName}: ${text(report.projectNameSnapshot, fallback)}`, alignment), cell(`${labels.projectCode}: ${text(report.projectCodeSnapshot, fallback)}`, alignment), cell(`${labels.projectManager}: ${text(report.projectManagerNameSnapshot, fallback)}`, alignment), cell(`${labels.reportDate}: ${formatReportDate(report.reportDate, locale, fallback)}`, alignment)], [cell(`${labels.statusDate}: ${formatReportDate(report.statusDate, locale, fallback)}`, alignment), cell(`${labels.reportingPeriod}: ${formatReportDate(report.periodFrom, locale, fallback)} - ${formatReportDate(report.periodTo, locale, fallback)}`, alignment), cell(`${labels.publishedBy}: ${text(report.publishedByNameSnapshot, fallback)}`, alignment), cell(`${labels.publishedAt}: ${formatReportDate(report.publishedAt, locale, fallback)}`, alignment)]] }, layout: "lightGrid" },
  ];
  if (hasReportSection(report, "ExecutiveSummary")) {
    content.push(
      { table: { rtl: isRtl, widths: ["*", "*"], body: [[{ stack: [{ text: labels.overallHealth, color: "#697586", bold: true }, { text: healthLabel(report.overallHealth, labels, locale), color: statusColor(report.overallHealth), bold: true, fontSize: 16, margin: [0, 6, 0, 0] }], margin: [10, 10, 10, 10], fillColor: "#F8FAFC" }, { stack: [{ text: labels.overallProgress, color: "#697586", bold: true }, { text: progressValue, color: "#1E4E79", bold: true, fontSize: 16, margin: [0, 6, 0, 0] }], margin: [10, 10, 10, 10], fillColor: "#F8FAFC" }]] }, layout: "lightGrid", margin: [0, 12, 0, 0] },
      sectionTitle(labels.healthOverview, alignment), { table: { rtl: isRtl, widths: ["*", "*", "*", "*", "*"], body: healthBody }, layout: "lightGrid" },
      narrativeSection(labels.executiveSummary, report.executiveSummary, alignment, labels), narrativeSection(labels.keyAchievements, report.keyAchievements, alignment, labels), narrativeSection(labels.challenges, report.challengesConcerns, alignment, labels), narrativeSection(labels.nextPriorities, report.nextPeriodPriorities, alignment, labels),
      sectionTitle(labels.progressOverview, alignment), { table: { rtl: isRtl, widths: [`${progressPercent}%`, `${100 - progressPercent}%`], body: [[{ text: "", fillColor: "#1E4E79", margin: [0, 7, 0, 7] }, { text: "", fillColor: "#DDE3E9", margin: [0, 7, 0, 7] }]] }, layout: "noBorders" }, { text: `${labels.current}: ${progressValue}`, alignment, bold: true, color: "#1E4E79", margin: [0, 0, 0, 7] },
    );
  }
  if (hasReportSection(report, "PerformanceKPIs") && report.performanceSnapshot) {
    const performance = report.performanceSnapshot;
    const performanceCurrency = performance.currency || currency;
    content.push(
      { stack: [
        { text: `${performanceLabels.statusDate}: ${formatReportDate(performance.statusDate, locale, fallback)} Â· ${performanceLabels.quality}: ${text(performance.dataQuality, performanceLabels.unavailable)}`, alignment, color: "#697586", margin: [0, 7, 0, 2] },
        sectionTitle(performanceLabels.title, alignment),
        { table: { rtl: isRtl, widths: ["*", "*", "*", "*"], body: [
        [performanceLabels.bac, performanceLabels.pv, performanceLabels.ev, performanceLabels.ac].map((label) => cell(label, alignment, true)),
        [cell(money(performance.budgetAtCompletion, performanceCurrency, locale, fallback), alignment), cell(money(performance.plannedValue, performanceCurrency, locale, fallback), alignment), cell(money(performance.earnedValue, performanceCurrency, locale, fallback), alignment), cell(money(performance.actualCost, performanceCurrency, locale, fallback), alignment)],
        [performanceLabels.sv, performanceLabels.cv, performanceLabels.spi, performanceLabels.cpi].map((label) => cell(label, alignment, true)),
        [cell(money(performance.scheduleVariance, performanceCurrency, locale, fallback), alignment), cell(money(performance.costVariance, performanceCurrency, locale, fallback), alignment), cell(isFiniteNumber(performance.schedulePerformanceIndex) ? performance.schedulePerformanceIndex.toFixed(2) : fallback, alignment), cell(isFiniteNumber(performance.costPerformanceIndex) ? performance.costPerformanceIndex.toFixed(2) : fallback, alignment)],
        ] }, layout: "lightGrid" },
      ], unbreakable: true },
    );
  }
  if (hasReportSection(report, "PerformanceKPIs") && history.length >= 2) content.push({ stack: [sectionTitle(labels.reportHistory, alignment), { table: { rtl: isRtl, headerRows: 1, widths: ["*", "*", "*"], body: [[cell(labels.reportDate, alignment, true), cell(labels.statusDate, alignment, true), cell(labels.overallProgress, alignment, true)], ...history.map((item) => [cell(formatReportDate(item.reportDate, locale, fallback), alignment), cell(formatReportDate(item.statusDate || item.reportDate, locale, fallback), alignment), cell(percentage(item.overallProgressPercent), alignment)])] }, layout: "lightGrid" }] });
  if (hasReportSection(report, "PlanPerformance") && report.performanceSnapshot) {
    const performanceCurrency = report.performanceSnapshot.currency || currency;
    const plans = report.performanceSnapshot.plans.filter((plan) => plan.includedInOfficialReporting);
    content.push(snapshotTable(performanceLabels.planPerformance, [performanceLabels.plan, performanceLabels.bac, performanceLabels.pv, performanceLabels.ev, performanceLabels.ac, performanceLabels.spi, performanceLabels.cpi], plans.map((plan) => [
      cell(plan.planName, alignment), cell(money(plan.budgetAtCompletion, performanceCurrency, locale, fallback), alignment),
      cell(money(plan.plannedValue, performanceCurrency, locale, fallback), alignment), cell(money(plan.earnedValue, performanceCurrency, locale, fallback), alignment),
      cell(money(plan.actualCost, performanceCurrency, locale, fallback), alignment), cell(isFiniteNumber(plan.schedulePerformanceIndex) ? plan.schedulePerformanceIndex.toFixed(2) : fallback, alignment),
      cell(isFiniteNumber(plan.costPerformanceIndex) ? plan.costPerformanceIndex.toFixed(2) : fallback, alignment),
    ]), alignment, isRtl, labels));
  }
  if (hasReportSection(report, "Milestones") && report.performanceSnapshot) {
    const milestones = report.performanceSnapshot.milestones;
    content.push(snapshotTable(labels.milestones, [performanceLabels.plan, labels.title, labels.baseline, labels.plannedDate, labels.actualDate, labels.variance, labels.status], milestones.map((item) => [
      cell(item.planName, alignment), cell(item.name, alignment), cell(formatReportDate(item.baselineDate, locale, fallback), alignment),
      cell(formatReportDate(item.plannedDate, locale, fallback), alignment), cell(formatReportDate(item.actualCompletionDate, locale, fallback), alignment),
      cell(item.varianceDays == null ? fallback : `${item.varianceDays} ${labels.days}`, alignment), cell(enumLabel(item.status, labels, locale), alignment),
    ]), alignment, isRtl, labels));
  }
  if (hasReportSection(report, "RisksIssues")) {
    content.push(snapshotTable(labels.keyRisks, [labels.title, labels.level, labels.status, labels.owner], (report.risks ?? []).map((item: ProjectStatusReportRiskSnapshotDto) => [cell(text(item.title, fallback), alignment), cell(enumLabel(item.riskLevel, labels, locale), alignment), cell(enumLabel(item.riskStatus, labels, locale), alignment), cell(text(item.ownerNameSnapshot, fallback), alignment)]), alignment, isRtl, labels));
    content.push(snapshotTable(labels.keyIssues, [labels.title, labels.severity, labels.priority, labels.status, labels.assignee], (report.issues ?? []).map((item: ProjectStatusReportIssueSnapshotDto) => [cell(text(item.title, fallback), alignment), cell(enumLabel(item.severity, labels, locale), alignment), cell(enumLabel(item.priority, labels, locale), alignment), cell(enumLabel(item.issueStatus, labels, locale), alignment), cell(text(item.assigneeNameSnapshot, fallback), alignment)]), alignment, isRtl, labels));
  }
  if (hasReportSection(report, "DecisionsRequired")) {
    const pending = (report.changeRequests ?? []).filter((item) => item.changeRequestStatus === "Pending");
    content.push(snapshotTable(labels.decisionsRequired, [labels.number, labels.reason, labels.justification, labels.impact, labels.requestedBy, labels.requestedAt], pending.map((item) => [cell(item.number, alignment), cell(text(item.reason, fallback), alignment), cell(text(item.justification, fallback), alignment), cell(text(item.impact, fallback), alignment), cell(text(item.requestedByNameSnapshot, fallback), alignment), cell(formatReportDate(item.requestedAt, locale, fallback), alignment)]), alignment, isRtl, labels));
  }
  content.push({ table: { rtl: isRtl, widths: ["*", "*", "*", "*"], body: [[cell(`${labels.preparedBy}: ${text(report.preparedByNameSnapshot, fallback)}`, alignment), cell(`${labels.publishedBy}: ${text(report.publishedByNameSnapshot, fallback)}`, alignment), cell(`${labels.publishedAt}: ${formatReportDate(report.publishedAt, locale, fallback)}`, alignment), cell(`${labels.sourceCapturedAt}: ${formatReportDate(report.sourceDataCapturedAt, locale, fallback)}`, alignment)]] }, layout: "lightGrid", margin: [0, 16, 0, 0] });
  return { rtl: isRtl, pageSize: "A4", pageMargins: [32, 32, 32, 42], defaultStyle: { font: isRtl ? "Cairo" : "Roboto", fontSize: 9, color: "#202833" }, styles: { sectionTitle: { fontSize: 12, bold: true, color: "#1E4E79" } }, info: { title: `${labels.report} - ${text(report.projectNameSnapshot, fallback)}`, author: labels.pmo }, content, footer: (currentPage: number, pageCount: number) => ({ text: `${text(report.projectCodeSnapshot, fallback)} | ${formatReportDate(report.reportDate, locale, fallback)} | ${labels.page} ${currentPage} ${labels.of} ${pageCount}`, alignment: "center", fontSize: 8, color: "#697586", margin: [0, 12, 0, 0] }) };
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

const EXCEL_LABELS = {
  en: {
    summary: "Report Summary", project: "Project", code: "Project Code", reportDate: "Report Date", statusDate: "Status Date", period: "Reporting Period", state: "State", health: "Health", progress: "Progress", executiveSummary: "Executive Summary", achievements: "Key Achievements", challenges: "Challenges / Concerns", nextPriorities: "Next Period Priorities", capturedAt: "Source Data Captured At", configVersion: "Reporting Configuration Version", includedSections: "Included Sections", performance: "Project Performance", metric: "Metric", value: "Value", currency: "Currency", quality: "Data Quality", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", spi: "SPI", cpi: "CPI", sv: "SV", cv: "CV", unallocated: "Unallocated Actual Cost", plans: "Plan Performance", milestones: "Milestones", milestone: "Milestone", baselineDate: "Baseline Date", plannedDate: "Current Planned Date", actualDate: "Actual Completion Date", varianceDays: "Variance (days)", plan: "Plan", plannedProgress: "Planned Progress", actualProgress: "Actual Progress", baseline: "Baseline", reasons: "Data Quality Reasons", risks: "Risks", issues: "Issues", decisions: "Decisions Required", title: "Title", status: "Status", owner: "Owner", severity: "Severity", priority: "Priority", assignee: "Assignee", number: "Number", reason: "Reason", justification: "Justification", impact: "Impact", requestedBy: "Requested By", requestedAt: "Requested At", missing: "Unavailable", report: "PROJECT STATUS REPORT",
  },
  ar: {
    summary: "Ù…Ù„Ø®Øµ Ø§Ù„ØªÙ‚Ø±ÙŠØ±", project: "Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", code: "Ø±Ù…Ø² Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", reportDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„ØªÙ‚Ø±ÙŠØ±", statusDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø­Ø§Ù„Ø©", period: "ÙØªØ±Ø© Ø§Ù„ØªÙ‚Ø±ÙŠØ±", state: "Ø§Ù„Ø­Ø§Ù„Ø©", health: "Ø§Ù„ØµØ­Ø©", progress: "Ø§Ù„ØªÙ‚Ø¯Ù…", executiveSummary: "Ø§Ù„Ù…Ù„Ø®Øµ Ø§Ù„ØªÙ†ÙÙŠØ°ÙŠ", achievements: "Ø£Ù‡Ù… Ø§Ù„Ø¥Ù†Ø¬Ø§Ø²Ø§Øª", challenges: "Ø§Ù„ØªØ­Ø¯ÙŠØ§Øª ÙˆØ§Ù„Ù…Ø®Ø§ÙˆÙ", nextPriorities: "Ø£ÙˆÙ„ÙˆÙŠØ§Øª Ø§Ù„ÙØªØ±Ø© Ø§Ù„Ù‚Ø§Ø¯Ù…Ø©", capturedAt: "ÙˆÙ‚Øª Ø§Ù„ØªÙ‚Ø§Ø· Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª", configVersion: "Ø¥ØµØ¯Ø§Ø± Ø¥Ø¹Ø¯Ø§Ø¯ Ø§Ù„ØªÙ‚Ø§Ø±ÙŠØ±", includedSections: "Ø§Ù„Ø£Ù‚Ø³Ø§Ù… Ø§Ù„Ù…Ø¶Ù…Ù†Ø©", performance: "Ø£Ø¯Ø§Ø¡ Ø§Ù„Ù…Ø´Ø±ÙˆØ¹", metric: "Ø§Ù„Ù…Ø¤Ø´Ø±", value: "Ø§Ù„Ù‚ÙŠÙ…Ø©", currency: "Ø§Ù„Ø¹Ù…Ù„Ø©", quality: "Ø¬ÙˆØ¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª", bac: "BAC", pv: "PV", ev: "EV", ac: "AC", spi: "SPI", cpi: "CPI", sv: "SV", cv: "CV", unallocated: "Ø§Ù„ØªÙƒÙ„ÙØ© Ø§Ù„ÙØ¹Ù„ÙŠØ© ØºÙŠØ± Ø§Ù„Ù…Ø®ØµØµØ©", plans: "Ø£Ø¯Ø§Ø¡ Ø§Ù„Ø®Ø·Ø·", milestones: "Ø§Ù„Ù…Ø¹Ø§Ù„Ù…", milestone: "Ø§Ù„Ù…Ø¹Ù„Ù…", baselineDate: "ØªØ§Ø±ÙŠØ® Ø®Ø· Ø§Ù„Ø£Ø³Ø§Ø³", plannedDate: "Ø§Ù„ØªØ§Ø±ÙŠØ® Ø§Ù„Ù…Ø®Ø·Ø· Ø§Ù„Ø­Ø§Ù„ÙŠ", actualDate: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¥Ù†Ø¬Ø§Ø² Ø§Ù„ÙØ¹Ù„ÙŠ", varianceDays: "Ø§Ù„Ø§Ù†Ø­Ø±Ø§Ù (Ø£ÙŠØ§Ù…)", plan: "Ø§Ù„Ø®Ø·Ø©", plannedProgress: "Ø§Ù„ØªÙ‚Ø¯Ù… Ø§Ù„Ù…Ø®Ø·Ø·", actualProgress: "Ø§Ù„ØªÙ‚Ø¯Ù… Ø§Ù„ÙØ¹Ù„ÙŠ", baseline: "Ø®Ø· Ø§Ù„Ø£Ø³Ø§Ø³", reasons: "Ø£Ø³Ø¨Ø§Ø¨ Ø¬ÙˆØ¯Ø© Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª", risks: "Ø§Ù„Ù…Ø®Ø§Ø·Ø±", issues: "Ø§Ù„Ù…Ø´ÙƒÙ„Ø§Øª", decisions: "Ù‚Ø±Ø§Ø±Ø§Øª Ù…Ø·Ù„ÙˆØ¨Ø©", title: "Ø§Ù„Ø¹Ù†ÙˆØ§Ù†", status: "Ø§Ù„Ø­Ø§Ù„Ø©", owner: "Ø§Ù„Ù…Ø§Ù„Ùƒ", severity: "Ø§Ù„Ø­Ø¯Ø©", priority: "Ø§Ù„Ø£ÙˆÙ„ÙˆÙŠØ©", assignee: "Ø§Ù„Ù…ÙƒÙ„Ù", number: "Ø§Ù„Ø±Ù‚Ù…", reason: "Ø§Ù„Ø³Ø¨Ø¨", justification: "Ø§Ù„Ù…Ø¨Ø±Ø±", impact: "Ø§Ù„Ø£Ø«Ø±", requestedBy: "Ù…Ù‚Ø¯Ù… Ø§Ù„Ø·Ù„Ø¨", requestedAt: "ØªØ§Ø±ÙŠØ® Ø§Ù„Ø·Ù„Ø¨", missing: "ØºÙŠØ± Ù…ØªØ§Ø­", report: "ØªÙ‚Ø±ÙŠØ± Ø­Ø§Ù„Ø© Ø§Ù„Ù…Ø´Ø±ÙˆØ¹",
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
  workbook.creator = "NEXUS PMO";
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
    [labels.reportDate, report.reportDate], [labels.statusDate, report.statusDate || report.reportDate], [labels.period, `${report.periodFrom} â€“ ${report.periodTo}`],
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
