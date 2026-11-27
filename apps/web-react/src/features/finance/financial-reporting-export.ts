import { brand } from "@lib/brand";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import type { ProjectFinancialReporting } from "../projects/api/project-financials";
import type { ProgramPortfolioFinancialRollup } from "../delivery/api/delivery";

export type FinancialReportingExportFormat = "pdf" | "xlsx";
export type FinancialReportingExportLevel = "Project" | "Program" | "Portfolio";

type Translate = (key: string, params?: Record<string, string | number>) => string;
type ExportCell = string | number | Date | null | undefined;

export interface FinancialReportingExportBase {
  locale: "en" | "ar";
  generatedAt: string;
  translate: Translate;
}

export interface ProjectFinancialReportingExportInput extends FinancialReportingExportBase {
  level: "Project";
  report: ProjectFinancialReporting;
}

export interface RollupFinancialReportingExportInput extends FinancialReportingExportBase {
  level: "Program" | "Portfolio";
  entityName: string;
  report: ProgramPortfolioFinancialRollup;
}

export type FinancialReportingExportInput =
  | ProjectFinancialReportingExportInput
  | RollupFinancialReportingExportInput;

const na = (input: FinancialReportingExportBase) => input.translate("financialReportingExport.na");

function formatDateValue(value: string, locale: "en" | "ar"): string {
  const date = new Date(value.length <= 10 ? `${value}T12:00:00Z` : value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(date);
}

function money(value: number | null | undefined, currency: string | null | undefined, input: FinancialReportingExportBase): string {
  if (value == null || !Number.isFinite(value) || !currency?.trim()) return na(input);
  const formatted = formatCurrencyValue(value, currency, input.locale, { maximumFractionDigits: 2, numberingSystem: "latn" });
  return formatted === "—" ? na(input) : formatted;
}

function percent(value: number | null | undefined, input: FinancialReportingExportBase): string {
  if (value == null || !Number.isFinite(value)) return na(input);
  return `${formatNumberValue(value, input.locale, { maximumFractionDigits: 1 })}%`;
}

function count(value: number | null | undefined, input: FinancialReportingExportBase): string {
  if (value == null || !Number.isFinite(value)) return na(input);
  return formatNumberValue(value, input.locale, { maximumFractionDigits: 0 });
}

function reportTitle(input: FinancialReportingExportInput): string {
  const t = input.translate;
  if (input.level === "Project") return `${t("financialReportingExport.title")} — ${t("financialReportingExport.levelProject")}: ${input.report.projectName}`;
  return `${t("financialReportingExport.title")} — ${t(`financialReportingExport.level${input.level}`)}: ${input.entityName}`;
}

function periodLabel(input: FinancialReportingExportInput): string {
  const t = input.translate;
  if (input.level === "Project") return t("financialReportingExport.lifetimeScope");
  const { fiscalYear, fiscalYearStartDate, fiscalYearEndDate } = input.report;
  return `${t("financialReportingExport.fiscalYear")}: ${fiscalYear} (${formatDateValue(fiscalYearStartDate, input.locale)} – ${formatDateValue(fiscalYearEndDate, input.locale)})`;
}

interface KpiRow {
  label: string;
  value: string;
}

function projectKpiRows(report: ProjectFinancialReporting, currency: string, input: FinancialReportingExportBase): KpiRow[] {
  const t = input.translate;
  return [
    { label: t("financialReportingExport.lifetimeCeiling"), value: money(report.lifetimeCeiling, currency, input) },
    { label: t("financialReportingExport.approvedAnnualBudget"), value: money(report.annualApprovedBudget, currency, input) },
    { label: t("financialReportingExport.lifetimeReserved"), value: money(report.lifetimeReserved, currency, input) },
    { label: t("financialReportingExport.outstandingCommitments"), value: money(report.outstandingCommitments, currency, input) },
    { label: t("financialReportingExport.grossActualPaid"), value: money(report.grossActualPaid, currency, input) },
    { label: t("financialReportingExport.confirmedRefundsCorrections"), value: money(report.confirmedRefundsAndCorrections, currency, input) },
    { label: t("financialReportingExport.netActualPaid"), value: money(report.netActualPaid, currency, input) },
    { label: t("financialReportingExport.availableLifetimeBudget"), value: money(report.availableLifetimeBudget, currency, input) },
    { label: t("financialReportingExport.annualBudgetUtilization"), value: percent(report.annualBudgetUtilizationPercentage, input) },
  ];
}

function projectDataQualityWarnings(report: ProjectFinancialReporting, input: FinancialReportingExportBase): string[] {
  const quality = report.dataQuality;
  if (!quality || !quality.hasAnyGap) return [];
  return quality.details.length ? quality.details : [input.translate("financialReportingExport.dataQualityUnresolved")];
}

function rollupDataQualityWarnings(report: ProgramPortfolioFinancialRollup, input: FinancialReportingExportBase): string[] {
  const t = input.translate;
  const warnings: string[] = [];
  if (report.coverage !== "Complete") warnings.push(t(`financialReportingExport.coverage_${report.coverage}`));
  if (report.reconciliationStatus !== "Reconciled") warnings.push(t("financialReportingExport.reconciliationProvisional"));
  if (report.projectsMissingApprovedAnnualBudget > 0) warnings.push(t("financialReportingExport.missingAnnualBudget", { count: report.projectsMissingApprovedAnnualBudget }));
  if (report.projectsWithFinancialDataGaps > 0) warnings.push(t("financialReportingExport.financialDataGaps", { count: report.projectsWithFinancialDataGaps }));
  if (report.unresolvedLegacyPaymentRequests > 0) warnings.push(t("financialReportingExport.legacyRequests", { count: report.unresolvedLegacyPaymentRequests }));
  if (report.hasMixedCurrencies) warnings.push(t("financialReportingExport.mixedCurrencies"));
  return warnings;
}

interface CurrencySummaryRow {
  currency: string;
  cells: string[];
}

function rollupCurrencyHeaders(input: RollupFinancialReportingExportInput): string[] {
  const t = input.translate;
  return [
    t("financialReportingExport.currency"),
    t("financialReportingExport.projectCount"),
    t("financialReportingExport.lifetimeCeiling"),
    t("financialReportingExport.availableLifetimeBudget"),
    t("financialReportingExport.lifetimeReserved"),
    t("financialReportingExport.outstandingCommitments"),
    t("financialReportingExport.approvedAnnualBudget"),
    t("financialReportingExport.effectiveActiveAllocations"),
    t("financialReportingExport.actualSettlementsInFiscalYear"),
    t("financialReportingExport.remainingPayableCommitments"),
  ];
}

function rollupCurrencyRows(input: RollupFinancialReportingExportInput): CurrencySummaryRow[] {
  return input.report.currencyGroups.map((group) => ({
    currency: group.currency,
    cells: [
      count(group.projectCount, input),
      money(group.lifetimeCeiling, group.currency, input),
      money(group.availableLifetimeBudget, group.currency, input),
      money(group.lifetimeReserved, group.currency, input),
      money(group.outstandingCommitments, group.currency, input),
      money(group.approvedAnnualBudget, group.currency, input),
      money(group.effectiveActiveAllocations, group.currency, input),
      money(group.actualSettlementsInFiscalYear, group.currency, input),
      money(group.remainingPayableCommitments, group.currency, input),
    ],
  }));
}

function projectBreakdownRows(input: RollupFinancialReportingExportInput): string[][] {
  const t = input.translate;
  return input.report.projects.map((project) => [
    project.projectName,
    project.isDirectProject ? t("financialReportingExport.directProject") : project.programName ?? t("financialReportingExport.programProject"),
    project.currency ?? na(input),
    money(project.lifetimeCeiling, project.currency, input),
    money(project.availableLifetimeBudget, project.currency, input),
    money(project.approvedAnnualBudget, project.currency, input),
    money(project.actualSettlementsInFiscalYear, project.currency, input),
    money(project.remainingPayableCommitments, project.currency, input),
    t(project.isAvailable ? "financialReportingExport.available" : `financialReportingExport.status_${project.dataStatus}`),
  ]);
}

/* ----------------------------- PDF ----------------------------- */

function pdfCell(text: string, alignment: "left" | "right", bold = false) {
  return { text, alignment, bold, margin: [4, 4, 4, 4] as [number, number, number, number] };
}

function pdfTable(headers: string[], rows: string[][], input: FinancialReportingExportBase) {
  const alignment = input.locale === "ar" ? "right" : "left";
  const body = [headers, ...rows].map((row, rowIndex) => row.map((value) => pdfCell(value, alignment, rowIndex === 0)));
  return {
    table: { headerRows: 1, widths: headers.map(() => "*"), body } as Record<string, unknown>,
    layout: "lightHorizontalLines",
    fontSize: 8,
    margin: [0, 0, 0, 12] as [number, number, number, number],
  };
}

function pdfSection(title: string, input: FinancialReportingExportBase) {
  return { text: title, style: "sectionTitle", alignment: input.locale === "ar" ? "right" : "left", margin: [0, 10, 0, 6] as [number, number, number, number], keepWithNext: true };
}

function pdfHeader(input: FinancialReportingExportInput) {
  const t = input.translate;
  const alignment = input.locale === "ar" ? "right" : "left";
  return [
    { text: reportTitle(input), style: "title", alignment, margin: [0, 0, 0, 4] as [number, number, number, number] },
    { text: periodLabel(input), alignment, margin: [0, 0, 0, 4] as [number, number, number, number] },
    { text: `${t("financialReportingExport.generatedAt")}: ${formatDateValue(input.generatedAt, input.locale)}`, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 10] as [number, number, number, number] },
  ];
}

function pdfWarnings(warnings: string[], input: FinancialReportingExportBase): unknown[] {
  if (!warnings.length) return [];
  const t = input.translate;
  const content: unknown[] = [pdfSection(t("financialReportingExport.dataQuality"), input)];
  for (const warning of warnings) {
    content.push({ text: `• ${warning}`, alignment: input.locale === "ar" ? "right" : "left", color: "#B45309", fontSize: 9, margin: [0, 0, 0, 2] as [number, number, number, number] });
  }
  return content;
}

export function buildFinancialReportingPdfDefinition(input: FinancialReportingExportInput): Record<string, unknown> {
  const t = input.translate;
  const content: unknown[] = [...pdfHeader(input)];

  if (input.level === "Project") {
    const { report } = input;
    const currency = report.currency;
    content.push(pdfSection(t("financialReportingExport.summary"), input));
    content.push(pdfTable([t("financialReportingExport.metric"), t("financialReportingExport.value")], projectKpiRows(report, currency, input).map((row) => [row.label, row.value]), input));

    content.push(...pdfWarnings(projectDataQualityWarnings(report, input), input));

    if (report.budgetLineUtilizations.length > 0) {
      content.push(pdfSection(t("financialReportingExport.budgetLines"), input));
      content.push(pdfTable(
        [t("financialReportingExport.budgetLine"), t("financialReportingExport.authorized"), t("financialReportingExport.reserved"), t("financialReportingExport.available"), t("financialReportingExport.utilization")],
        report.budgetLineUtilizations.map((line) => [
          line.reference || line.description,
          money(line.authorizedLimit, currency, input),
          money(line.reservedAmount, currency, input),
          money(line.authorizedLimit - line.reservedAmount, currency, input),
          percent(line.utilizationPercentage, input),
        ]),
        input,
      ));
    }
  } else {
    const warnings = rollupDataQualityWarnings(input.report, input);
    content.push(pdfSection(t("financialReportingExport.currencyBreakdown"), input));
    const hasGroups = input.report.currencyGroups.length > 0;
    content.push(pdfTable(
      rollupCurrencyHeaders(input),
      hasGroups ? rollupCurrencyRows(input).map((row) => [row.currency, ...row.cells]) : [[na(input)]],
      input,
    ));

    content.push(...pdfWarnings(warnings, input));

    if (input.report.projects.length > 0) {
      content.push(pdfSection(t("financialReportingExport.projectBreakdown"), input));
      content.push(pdfTable(
        [t("financialReportingExport.project"), t("financialReportingExport.relationship"), t("financialReportingExport.currency"), t("financialReportingExport.lifetimeCeiling"), t("financialReportingExport.availableLifetimeBudget"), t("financialReportingExport.approvedAnnualBudget"), t("financialReportingExport.actualSettlementsInFiscalYear"), t("financialReportingExport.remainingPayableCommitments"), t("financialReportingExport.status")],
        projectBreakdownRows(input),
        input,
      ));
    }
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
    info: { title: reportTitle(input), author: brand.product.fullName, subject: t("financialReportingExport.title") },
    content,
    footer: (currentPage: number, pageCount: number) => ({
      text: `${brand.product.fullName} | ${t("financialReportingExport.page")} ${currentPage} ${t("financialReportingExport.of")} ${pageCount}`,
      alignment: "center",
      fontSize: 8,
      color: "#64748B",
      margin: [0, 10, 0, 0] as [number, number, number, number],
    }),
  };
}

/* ----------------------------- Excel ----------------------------- */

function createWorkbookSheet(
  workbook: import("exceljs").Workbook,
  name: string,
  title: string,
  subtitle: string,
  headers: string[],
  rows: Array<Array<ExportCell>>,
  input: FinancialReportingExportBase,
) {
  const worksheet = workbook.addWorksheet(name.slice(0, 31), { views: [{ state: "frozen", ySplit: 4, rightToLeft: input.locale === "ar" }] });
  const horizontal = input.locale === "ar" ? "right" : "left";
  worksheet.mergeCells(1, 1, 1, headers.length);
  const titleCell = worksheet.getCell(1, 1);
  titleCell.value = title;
  titleCell.font = { bold: true, size: 15, color: { argb: "FF164E63" } };
  titleCell.alignment = { vertical: "middle", horizontal };
  worksheet.getRow(1).height = 24;
  worksheet.mergeCells(2, 1, 2, headers.length);
  worksheet.getCell(2, 1).value = subtitle;
  worksheet.getCell(2, 1).font = { color: { argb: "FF475569" }, size: 10 };
  worksheet.getCell(2, 1).alignment = { wrapText: true, horizontal };
  worksheet.mergeCells(3, 1, 3, headers.length);
  worksheet.getCell(3, 1).value = `${input.translate("financialReportingExport.generatedAt")}: ${formatDateValue(input.generatedAt, input.locale)}`;
  worksheet.getCell(3, 1).font = { color: { argb: "FF64748B" }, size: 9 };
  worksheet.getCell(3, 1).alignment = { horizontal };
  worksheet.addRow([]);
  const headerRow = worksheet.addRow(headers);
  headerRow.font = { bold: true, color: { argb: "FFFFFFFF" }, size: 10 };
  headerRow.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF0F4C5C" } };
  headerRow.alignment = { vertical: "middle", horizontal, wrapText: true };
  headerRow.height = 22;
  for (const row of rows) worksheet.addRow(row.length ? row : [na(input)]);
  headers.forEach((header, index) => {
    const column = worksheet.getColumn(index + 1);
    let width = Math.max(12, Math.min(40, header.length + 3));
    column.eachCell({ includeEmpty: false }, (cell) => { width = Math.max(width, Math.min(44, String(cell.value ?? "").length + 2)); });
    column.width = width;
    column.alignment = { vertical: "middle", horizontal, wrapText: true };
  });
  if (rows.length > 0) worksheet.autoFilter = { from: { row: headerRow.number, column: 1 }, to: { row: headerRow.number + rows.length, column: headers.length } };
  worksheet.views = [{ state: "frozen", ySplit: headerRow.number, rightToLeft: input.locale === "ar" }];
  return worksheet;
}

export async function buildFinancialReportingWorkbook(input: FinancialReportingExportInput): Promise<import("exceljs").Workbook> {
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  workbook.creator = brand.product.fullName;
  workbook.subject = reportTitle(input);
  workbook.created = new Date();
  const t = input.translate;

  if (input.level === "Project") {
    const { report } = input;
    const currency = report.currency;
    createWorkbookSheet(
      workbook,
      t("financialReportingExport.summary"),
      reportTitle(input),
      `${periodLabel(input)} | ${currency}`,
      [t("financialReportingExport.metric"), t("financialReportingExport.value")],
      projectKpiRows(report, currency, input).map((row) => [row.label, row.value]),
      input,
    );

    if (report.budgetLineUtilizations.length > 0) {
      createWorkbookSheet(
        workbook,
        t("financialReportingExport.budgetLines"),
        t("financialReportingExport.budgetLines"),
        reportTitle(input),
        [t("financialReportingExport.budgetLine"), t("financialReportingExport.authorized"), t("financialReportingExport.reserved"), t("financialReportingExport.available"), t("financialReportingExport.utilization")],
        report.budgetLineUtilizations.map((line) => [
          line.reference || line.description,
          money(line.authorizedLimit, currency, input),
          money(line.reservedAmount, currency, input),
          money(line.authorizedLimit - line.reservedAmount, currency, input),
          percent(line.utilizationPercentage, input),
        ]),
        input,
      );
    }
  } else {
    createWorkbookSheet(
      workbook,
      t("financialReportingExport.currencyBreakdown"),
      reportTitle(input),
      periodLabel(input),
      rollupCurrencyHeaders(input),
      input.report.currencyGroups.length
        ? rollupCurrencyRows(input).map((row) => [row.currency, ...row.cells])
        : [[na(input)]],
      input,
    );

    if (input.report.projects.length > 0) {
      createWorkbookSheet(
        workbook,
        t("financialReportingExport.projectBreakdown"),
        t("financialReportingExport.projectBreakdown"),
        reportTitle(input),
        [t("financialReportingExport.project"), t("financialReportingExport.relationship"), t("financialReportingExport.currency"), t("financialReportingExport.lifetimeCeiling"), t("financialReportingExport.availableLifetimeBudget"), t("financialReportingExport.approvedAnnualBudget"), t("financialReportingExport.actualSettlementsInFiscalYear"), t("financialReportingExport.remainingPayableCommitments"), t("financialReportingExport.status")],
        projectBreakdownRows(input),
        input,
      );
    }
  }

  const warnings = input.level === "Project" ? projectDataQualityWarnings(input.report, input) : rollupDataQualityWarnings(input.report, input);
  if (warnings.length > 0) {
    createWorkbookSheet(
      workbook,
      t("financialReportingExport.dataQuality"),
      t("financialReportingExport.dataQuality"),
      reportTitle(input),
      [t("financialReportingExport.warning")],
      warnings.map((warning) => [warning]),
      input,
    );
  }

  return workbook;
}

/* ----------------------------- Download ----------------------------- */

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

function exportFilename(input: FinancialReportingExportInput, extension: string): string {
  const level = input.level.toUpperCase();
  const subject = input.level === "Project" ? input.report.projectCode || input.report.projectName : input.entityName;
  const safe = subject.replace(/[^\p{L}\p{N}]+/gu, "_").slice(0, 48);
  const period = input.level === "Project" ? "lifetime" : String(input.report.fiscalYear);
  return `FOPX_Financial_${level}_${safe}_${period}.${extension}`;
}

export async function exportFinancialReportingPdf(input: FinancialReportingExportInput): Promise<void> {
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(vfsModule.default);
  pdfMake.addFonts({
    Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" },
    Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" },
  });
  const blob = await pdfMake.createPdf(buildFinancialReportingPdfDefinition(input)).getBlob();
  downloadBlob(blob, exportFilename(input, "pdf"));
}

export async function exportFinancialReportingExcel(input: FinancialReportingExportInput): Promise<void> {
  const workbook = await buildFinancialReportingWorkbook(input);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  downloadBlob(blob, exportFilename(input, "xlsx"));
}