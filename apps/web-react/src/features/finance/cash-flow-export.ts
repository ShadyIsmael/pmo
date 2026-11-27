import { brand } from "@lib/brand";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import type {
  ProgramPortfolioCashFlow,
  ProgramPortfolioCashFlowCurrencyGroup,
  ProgramPortfolioCashFlowItem,
  ProjectCashFlow,
} from "../delivery/api/program-portfolio-cash-flow";

export type CashFlowExportFormat = "pdf" | "xlsx";
type Translate = (key: string, params?: Record<string, string | number>) => string;

interface CashFlowExportInputBase {
  level: "Project" | "Program" | "Portfolio";
  entityName: string;
  locale: "en" | "ar";
  generatedAt: string;
  translate: Translate;
}

export type CashFlowExportInput = CashFlowExportInputBase & (
  | { level: "Project"; projectId: string; report: ProjectCashFlow }
  | { level: "Program" | "Portfolio"; report: ProgramPortfolioCashFlow }
);

type PreparedCashFlowExportInput = CashFlowExportInputBase & { report: ProgramPortfolioCashFlow };

function prepareExportInput(input: CashFlowExportInput): PreparedCashFlowExportInput {
  if (input.level !== "Project") return input;
  const report = input.report;
  const currencyGroups: ProgramPortfolioCashFlowCurrencyGroup[] = report.currencyGroups.map(group => {
    const wrapLine = (line: ProjectCashFlow["currencyGroups"][number]["buckets"][number]["items"][number]): ProgramPortfolioCashFlowItem => ({
      projectId: report.projectId,
      projectPublicId: report.projectId,
      projectName: input.entityName,
      programId: null,
      programName: null,
      isDirectProject: true,
      line,
    });
    const issueCodes = report.dataQualityIssues
      .filter(issue => !issue.currency || issue.currency === group.currency)
      .map(issue => issue.code);
    return {
      currency: group.currency,
      contributingProjectCount: 1,
      dataQualityStatus: group.dataQualityStatus,
      buckets: group.buckets.map(bucket => ({
        ...bucket,
        dataQualityStatus: group.dataQualityStatus,
        qualityCodes: [...new Set(bucket.items.flatMap(item => item.qualityCodes))].sort(),
        items: bucket.items.map(wrapLine),
      })),
      undated: {
        ...group.undated,
        dataQualityStatus: group.dataQualityStatus,
        qualityCodes: [...new Set(group.undated.items.flatMap(item => item.qualityCodes))].sort(),
        items: group.undated.items.map(wrapLine),
      },
      qualityCodes: [...new Set([
        ...issueCodes,
        ...group.buckets.flatMap(bucket => bucket.items.flatMap(item => item.qualityCodes)),
        ...group.undated.items.flatMap(item => item.qualityCodes),
      ])].sort(),
    };
  });
  const knownCurrencies = new Set(currencyGroups.map(group => group.currency).filter((currency): currency is string => currency !== null));
  const hasMixedCurrencies = knownCurrencies.size > 1 || (knownCurrencies.size > 0 && currencyGroups.some(group => group.currency === null));
  return {
    ...input,
    report: {
      entityType: "Program",
      entityId: report.projectId,
      periodFrom: report.periodFrom,
      periodTo: report.periodTo,
      granularity: report.granularity,
      dataQualityStatus: report.dataQualityStatus,
      partialCoverage: false,
      includedProjectCount: 1,
      projectsWithUnavailableData: report.dataQualityStatus === "Unavailable" ? 1 : 0,
      hasMixedCurrencies,
      currencyGroups,
      projects: [{
        projectId: report.projectId,
        projectPublicId: report.projectId,
        projectName: input.entityName,
        currency: currencyGroups.length === 1 ? currencyGroups[0].currency : null,
        programId: null,
        programName: null,
        isDirectProject: true,
        cashFlow: report,
      }],
      dataQualityIssues: report.dataQualityIssues.map(issue => ({
        projectId: report.projectId,
        projectName: input.entityName,
        issue,
      })),
    },
  };
}

type MetricKey = "plannedResidual" | "committedUnpaid" | "actualGross" | "executedAdjustments" | "actualNet";
const metricKeys: MetricKey[] = ["plannedResidual", "committedUnpaid", "actualGross", "executedAdjustments", "actualNet"];

function formatDate(value: string | null | undefined, locale: "en" | "ar"): string {
  if (!value) return "";
  const date = new Date(value.length <= 10 ? `${value}T12:00:00Z` : value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(date);
}

function formatTimestamp(value: string, locale: "en" | "ar"): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function money(value: number | null | undefined, currency: string | null, input: PreparedCashFlowExportInput): string {
  if (value == null || !Number.isFinite(value) || !currency) return input.translate("cashFlowExport.na");
  const formatted = formatCurrencyValue(value, currency, input.locale, { maximumFractionDigits: 2, numberingSystem: "latn" });
  return formatted === "-" ? input.translate("cashFlowExport.na") : formatted;
}

function number(value: number | null | undefined, input: PreparedCashFlowExportInput): string {
  if (value == null || !Number.isFinite(value)) return input.translate("cashFlowExport.na");
  return formatNumberValue(value, input.locale, { maximumFractionDigits: 0 });
}

function status(input: PreparedCashFlowExportInput, value: string): string {
  return input.translate(`cashFlow.status.${value.toLowerCase()}`);
}

function reportTitle(input: PreparedCashFlowExportInput): string {
  return `${input.translate("cashFlowExport.title")} · ${input.translate(`cashFlowExport.level.${input.level.toLowerCase()}`)} · ${input.entityName}`;
}

function periodLabel(input: PreparedCashFlowExportInput): string {
  return `${formatDate(input.report.periodFrom, input.locale)} – ${formatDate(input.report.periodTo, input.locale)} · ${input.translate(`cashFlow.granularity.${input.report.granularity.toLowerCase()}`)}`;
}

function groupTotal(group: ProgramPortfolioCashFlowCurrencyGroup, metric: MetricKey): number | null {
  if (group.dataQualityStatus !== "Available") return null;
  const values = group.buckets.map(bucket => bucket[metric]);
  if (values.some(value => value == null || !Number.isFinite(value))) return null;
  return values.reduce<number>((sum, value) => sum + (value ?? 0), 0);
}

function currencyLabel(currency: string | null, input: PreparedCashFlowExportInput): string {
  return currency ?? input.translate("cashFlow.unknownCurrency");
}

function relationship(item: { isDirectProject: boolean; programName: string | null }, input: PreparedCashFlowExportInput): string {
  return item.isDirectProject ? input.translate("cashFlow.directProject") : item.programName ?? input.translate("cashFlow.programUnavailable");
}

function flattenItems(input: PreparedCashFlowExportInput): Array<{
  currency: string | null;
  period: string;
  item: ProgramPortfolioCashFlowItem;
}> {
  return input.report.currencyGroups.flatMap(group => [
    ...group.buckets.flatMap(bucket => bucket.items.map(item => ({ currency: group.currency, period: bucket.periodStart, item }))),
    ...group.undated.items.map(item => ({ currency: group.currency, period: "", item })),
  ]);
}

function qualityRows(input: PreparedCashFlowExportInput): string[][] {
  const sourceIssues = input.report.dataQualityIssues.map(row => [
    row.projectName,
    row.issue.code,
    status(input, row.issue.severity),
    row.issue.currency ?? input.translate("cashFlow.unknownCurrency"),
    row.issue.sourceType ?? input.translate("cashFlowExport.na"),
    row.issue.sourceId ?? input.translate("cashFlowExport.na"),
  ]);
  const aggregateCodes = input.report.currencyGroups.flatMap(group => [
    ...group.qualityCodes.map(code => [
      input.translate("cashFlowExport.currencyGroup"), code, status(input, group.dataQualityStatus),
      currencyLabel(group.currency, input), input.translate("cashFlowExport.aggregated"), input.translate("cashFlowExport.na"),
    ]),
    ...group.buckets.flatMap(bucket => bucket.qualityCodes.map(code => [
      input.translate("cashFlow.period"), code, status(input, bucket.dataQualityStatus),
      currencyLabel(group.currency, input), formatDate(bucket.periodStart, input.locale), input.translate("cashFlowExport.na"),
    ])),
    ...group.undated.qualityCodes.map(code => [
      input.translate("cashFlow.undated"), code, status(input, group.undated.dataQualityStatus),
      currencyLabel(group.currency, input), input.translate("cashFlowExport.aggregated"), input.translate("cashFlowExport.na"),
    ]),
  ]);
  return [...sourceIssues, ...aggregateCodes];
}

function pdfTable(headers: string[], rows: string[][], input: PreparedCashFlowExportInput): Record<string, unknown> {
  return {
    table: { headerRows: 1, widths: headers.map(() => "*"), body: [headers, ...(rows.length ? rows : [[input.translate("cashFlowExport.noRows"), ...headers.slice(1).map(() => "")]])] },
    layout: "lightHorizontalLines",
    fontSize: 8,
    margin: [0, 0, 0, 10],
  };
}

function pdfSection(title: string): Record<string, unknown> {
  return { text: title, style: "sectionTitle", margin: [0, 12, 0, 5] };
}

function metricHeaders(input: PreparedCashFlowExportInput): string[] {
  return [
    input.translate("cashFlow.period"),
    ...metricKeys.map(key => input.translate(`cashFlow.${key}`)),
    input.translate("cashFlow.status.label"),
    input.translate("cashFlow.qualityCodes"),
  ];
}

function bucketRows(group: ProgramPortfolioCashFlowCurrencyGroup, input: PreparedCashFlowExportInput): string[][] {
  return group.buckets.map(bucket => [
    formatDate(bucket.periodStart, input.locale),
    ...metricKeys.map(key => money(bucket[key], group.currency, input)),
    status(input, bucket.dataQualityStatus),
    bucket.qualityCodes.join(", ") || input.translate("cashFlowExport.na"),
  ]);
}

function pdfHeader(input: PreparedCashFlowExportInput): unknown[] {
  return [
    { text: reportTitle(input), style: "title", alignment: input.locale === "ar" ? "right" : "left" },
    { text: periodLabel(input), style: "subtitle", alignment: input.locale === "ar" ? "right" : "left", margin: [0, 4, 0, 10] },
    { text: `${input.translate("cashFlowExport.generatedAt")}: ${formatTimestamp(input.generatedAt, input.locale)}`, fontSize: 8, color: "#64748B", margin: [0, 0, 0, 10] },
  ];
}

export function buildCashFlowPdfDefinition(rawInput: CashFlowExportInput): Record<string, unknown> {
  const input = prepareExportInput(rawInput);
  const t = input.translate;
  const content: unknown[] = pdfHeader(input);
  if (input.report.partialCoverage || input.report.dataQualityStatus !== "Available") {
    content.push({
      text: t(input.report.partialCoverage ? "cashFlow.partialCoverage" : `cashFlow.status.${input.report.dataQualityStatus.toLowerCase()}`),
      color: "#B45309",
      fontSize: 9,
      margin: [0, 0, 0, 8],
      alignment: input.locale === "ar" ? "right" : "left",
    });
  }

  content.push(pdfSection(t("cashFlowExport.summary")));
  content.push(pdfTable(
    [t("cashFlow.currency"), t("cashFlow.projectCount"), t("cashFlow.status.label"), ...metricKeys.map(key => t(`cashFlow.${key}`)), t("cashFlow.qualityCodes")],
    input.report.currencyGroups.map(group => [
      currencyLabel(group.currency, input),
      number(group.contributingProjectCount, input),
      status(input, group.dataQualityStatus),
      ...metricKeys.map(key => money(groupTotal(group, key), group.currency, input)),
      group.qualityCodes.join(", ") || t("cashFlowExport.na"),
    ]),
    input,
  ));

  for (const group of input.report.currencyGroups) {
    content.push(pdfSection(`${t("cashFlowExport.timeline")} · ${currencyLabel(group.currency, input)}`));
    content.push(pdfTable(metricHeaders(input), bucketRows(group, input), input));
    content.push(pdfSection(t("cashFlow.undated")));
    content.push(pdfTable(
      [t("cashFlow.status.label"), ...metricKeys.map(key => t(`cashFlow.${key}`)), t("cashFlow.qualityCodes")],
      [[status(input, group.undated.dataQualityStatus), ...metricKeys.map(key => money(group.undated[key], group.currency, input)), group.undated.qualityCodes.join(", ") || t("cashFlowExport.na")]],
      input,
    ));
  }

  content.push(pdfSection(t("cashFlowExport.projectBreakdown")));
  content.push(pdfTable(
    [t("cashFlow.project"), t("cashFlow.relationship"), t("cashFlow.currency"), t("cashFlow.status.label")],
    input.report.projects.map(project => [
      project.projectName,
      relationship(project, input),
      currencyLabel(project.currency, input),
      status(input, project.cashFlow.dataQualityStatus),
    ]),
    input,
  ));

  const itemRows = flattenItems(input).map(({ currency, period, item }) => [
    item.projectName,
    relationship(item, input),
    currencyLabel(currency, input),
    period ? formatDate(period, input.locale) : t("cashFlow.undated"),
    t(`cashFlow.measure.${item.line.measure.toLowerCase()}`),
    item.line.sourceType,
    item.line.sourceId,
    item.line.description ?? t("cashFlowExport.na"),
    item.line.status ?? t("cashFlowExport.na"),
    item.line.eventDate ? formatDate(item.line.eventDate, input.locale) : t("cashFlowExport.na"),
    item.line.expectedDate ? formatDate(item.line.expectedDate, input.locale) : t("cashFlowExport.na"),
    item.line.timingVarianceDays == null ? t("cashFlowExport.na") : number(item.line.timingVarianceDays, input),
    currencyLabel(item.line.currency ?? currency, input),
    money(item.line.amount, item.line.currency ?? currency, input),
    item.line.qualityCodes.join(", "),
  ]);
  if (itemRows.length) {
    content.push(pdfSection(t("cashFlowExport.sourceDetails")));
    content.push(pdfTable(
      [t("cashFlow.project"), t("cashFlow.relationship"), t("cashFlow.currency"), t("cashFlow.period"), t("cashFlow.measure.label"), t("cashFlowExport.source"), t("cashFlowExport.sourceId"), t("cashFlow.description"), t("cashFlow.status.label"), t("cashFlow.eventDate"), t("cashFlow.expectedDate"), t("cashFlow.timingVarianceDays"), t("cashFlow.currency"), t("cashFlow.amount"), t("cashFlow.qualityCodes")],
      itemRows,
      input,
    ));
  }

  const collectedQualityRows = qualityRows(input);
  if (collectedQualityRows.length) {
    content.push(pdfSection(t("cashFlowExport.dataQuality")));
    content.push(pdfTable(
      [t("cashFlow.project"), t("cashFlow.qualityCode"), t("cashFlow.status.label"), t("cashFlow.currency"), t("cashFlowExport.source"), t("cashFlowExport.sourceId")],
      collectedQualityRows,
      input,
    ));
  }

  return {
    rtl: input.locale === "ar",
    pageSize: "A4",
    pageOrientation: "landscape",
    pageMargins: [30, 34, 30, 42],
    defaultStyle: { font: input.locale === "ar" ? "Cairo" : "Roboto", fontSize: 9, color: "#202833", lineHeight: 1.2 },
    styles: {
      title: { fontSize: 18, bold: true, color: "#164E63" },
      subtitle: { fontSize: 12, bold: true, color: "#475569" },
      sectionTitle: { fontSize: 13, bold: true, color: "#164E63" },
    },
    info: { title: reportTitle(input), author: brand.product.fullName, subject: t("cashFlowExport.title") },
    content,
    footer: (currentPage: number, pageCount: number) => ({
      text: `${brand.product.fullName} | ${t("financialReportingExport.page")} ${currentPage} ${t("financialReportingExport.of")} ${pageCount}`,
      alignment: "center",
      fontSize: 8,
      color: "#64748B",
      margin: [0, 10, 0, 0],
    }),
  };
}

function worksheet(
  workbook: import("exceljs").Workbook,
  name: string,
  title: string,
  headers: string[],
  rows: Array<Array<string | number | null>>,
  input: PreparedCashFlowExportInput,
): void {
  const sheet = workbook.addWorksheet(name.slice(0, 31), { views: [{ state: "frozen", ySplit: 5, rightToLeft: input.locale === "ar" }] });
  sheet.addRow([brand.product.fullName]);
  sheet.addRow([title]);
  sheet.addRow([periodLabel(input)]);
  const overallStatus = status(input, input.report.dataQualityStatus);
  sheet.addRow([`${input.translate("cashFlow.status.label")}: ${overallStatus}${input.report.partialCoverage ? ` · ${input.translate("cashFlow.partialCoverage")}` : ""}`]);
  sheet.addRow([`${input.translate("cashFlowExport.generatedAt")}: ${formatTimestamp(input.generatedAt, input.locale)}`]);
  const header = sheet.addRow(headers);
  header.font = { bold: true, color: { argb: "FFFFFFFF" } };
  header.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF164E63" } };
  rows.forEach(row => sheet.addRow(row));
  sheet.columns = headers.map((_, index) => ({ key: `c${index}`, width: index === 0 ? 28 : 20 }));
  if (rows.length) sheet.autoFilter = { from: { row: 6, column: 1 }, to: { row: 5 + rows.length + 1, column: headers.length } };
}

export async function buildCashFlowWorkbook(input: CashFlowExportInput): Promise<import("exceljs").Workbook> {
  const prepared = prepareExportInput(input);
  return buildPreparedCashFlowWorkbook(prepared);
}

async function buildPreparedCashFlowWorkbook(input: PreparedCashFlowExportInput): Promise<import("exceljs").Workbook> {
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  workbook.creator = brand.product.fullName;
  workbook.subject = reportTitle(input);
  workbook.created = new Date(input.generatedAt);
  const t = input.translate;

  worksheet(workbook, t("cashFlowExport.summary"), reportTitle(input),
    [t("cashFlow.currency"), t("cashFlow.projectCount"), t("cashFlow.status.label"), ...metricKeys.map(key => t(`cashFlow.${key}`)), t("cashFlow.qualityCodes")],
    input.report.currencyGroups.map(group => [
      currencyLabel(group.currency, input),
      number(group.contributingProjectCount, input),
      status(input, group.dataQualityStatus),
      ...metricKeys.map(key => money(groupTotal(group, key), group.currency, input)),
      group.qualityCodes.join(", ") || t("cashFlowExport.na"),
    ]), input);

  worksheet(workbook, t("cashFlowExport.timeline"), t("cashFlowExport.timeline"),
    [t("cashFlow.currency"), ...metricHeaders(input)],
    input.report.currencyGroups.flatMap(group => [
      ...group.buckets.map(bucket => [
        currencyLabel(group.currency, input),
        formatDate(bucket.periodStart, input.locale),
        ...metricKeys.map(key => money(bucket[key], group.currency, input)),
        status(input, bucket.dataQualityStatus),
        bucket.qualityCodes.join(", ") || t("cashFlowExport.na"),
      ]),
      [
        currencyLabel(group.currency, input),
        t("cashFlow.undated"),
        ...metricKeys.map(key => money(group.undated[key], group.currency, input)),
        status(input, group.undated.dataQualityStatus),
        group.undated.qualityCodes.join(", ") || t("cashFlowExport.na"),
      ],
    ]), input);

  worksheet(workbook, t("cashFlowExport.projectBreakdown"), t("cashFlowExport.projectBreakdown"),
    [t("cashFlow.project"), t("cashFlow.relationship"), t("cashFlow.currency"), t("cashFlow.status.label"), t("cashFlow.qualityCodes")],
    input.report.projects.map(project => [
      project.projectName,
      relationship(project, input),
      currencyLabel(project.currency, input),
      status(input, project.cashFlow.dataQualityStatus),
      [...new Set(project.cashFlow.dataQualityIssues.map(issue => issue.code))].join(", "),
    ]), input);

  const detailHeaders = [t("cashFlow.project"), t("cashFlow.relationship"), t("cashFlow.currency"), t("cashFlow.period"), t("cashFlow.measure.label"), t("cashFlowExport.source"), t("cashFlowExport.sourceId"), t("cashFlow.description"), t("cashFlow.status.label"), t("cashFlow.eventDate"), t("cashFlow.expectedDate"), t("cashFlow.timingVarianceDays"), t("cashFlow.currency"), t("cashFlow.amount"), t("cashFlow.qualityCodes")];
  worksheet(workbook, t("cashFlowExport.sourceDetails"), t("cashFlowExport.sourceDetails"), detailHeaders,
    flattenItems(input).map(({ currency, period, item }) => [
      item.projectName,
      relationship(item, input),
      currencyLabel(currency, input),
      period ? formatDate(period, input.locale) : t("cashFlow.undated"),
      t(`cashFlow.measure.${item.line.measure.toLowerCase()}`),
      item.line.sourceType,
      item.line.sourceId,
      item.line.description ?? t("cashFlowExport.na"),
      item.line.status ?? t("cashFlowExport.na"),
      formatDate(item.line.eventDate, input.locale),
      formatDate(item.line.expectedDate, input.locale),
      item.line.timingVarianceDays == null ? t("cashFlowExport.na") : number(item.line.timingVarianceDays, input),
      currencyLabel(item.line.currency ?? currency, input),
      money(item.line.amount, item.line.currency ?? currency, input),
      item.line.qualityCodes.join(", "),
    ]), input);

  const collectedQualityRows = qualityRows(input);
  if (collectedQualityRows.length) {
    worksheet(workbook, t("cashFlowExport.dataQuality"), t("cashFlowExport.dataQuality"),
      [t("cashFlow.project"), t("cashFlow.qualityCode"), t("cashFlow.status.label"), t("cashFlow.currency"), t("cashFlowExport.source"), t("cashFlowExport.sourceId")],
      collectedQualityRows, input);
  }

  return workbook;
}

function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function filename(input: PreparedCashFlowExportInput, extension: string): string {
  const safe = input.entityName.replace(/[^\p{L}\p{N}]+/gu, "_").slice(0, 48);
  const period = `${input.report.periodFrom ?? "from"}_${input.report.periodTo ?? "to"}`;
  return `FOPX_CashFlow_${input.level.toUpperCase()}_${safe}_${period}.${extension}`;
}

export async function exportCashFlowPdf(input: CashFlowExportInput): Promise<void> {
  const prepared = prepareExportInput(input);
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(vfsModule.default);
  pdfMake.addFonts({
    Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" },
    Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" },
  });
  const blob = await pdfMake.createPdf(buildCashFlowPdfDefinition(input)).getBlob();
  downloadBlob(blob, filename(prepared, "pdf"));
}

export async function exportCashFlowExcel(input: CashFlowExportInput): Promise<void> {
  const prepared = prepareExportInput(input);
  const workbook = await buildCashFlowWorkbook(input);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  downloadBlob(blob, filename(prepared, "xlsx"));
}
