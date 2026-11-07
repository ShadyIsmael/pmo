import { brand } from "@lib/brand";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import type { CostUtilizationDaily, CostUtilizationMetric, CostUtilizationReport } from "./api/cost-utilization";

export type CostUtilizationExportFormat = "pdf" | "xlsx";
export type CostUtilizationExportReportType = "summary" | "dailyDetail" | "calendarSummary" | "dayDetails";

export interface CostUtilizationExportInput {
  report: CostUtilizationReport;
  locale: "en" | "ar";
  reportType: CostUtilizationExportReportType;
  reportTypeLabel: string;
  title: string;
  from: string;
  to: string;
  filters: Array<{ label: string; value: string }>;
  includeBreakdowns: boolean;
  translate: (key: string) => string;
}

type ExportCell = string | number | Date | null | undefined;

const costFields = [
  ["plannedLaborCost", "plannedCost"],
  ["actualLaborCost", "actualCost"],
  ["laborCostVariance", "variance"],
  ["overtimeCost", "overtimeCost"],
  ["unusedCapacityCost", "unusedCost"],
  ["totalResourceCost", "totalCost"],
] as const;

function formatDate(value: string, locale: "en" | "ar"): string {
  const datePart = value.slice(0, 10);
  const date = new Date(`${datePart}T12:00:00Z`);
  if (Number.isNaN(date.getTime())) return datePart;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar" : "en", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(date);
}

function number(value: number | null | undefined, locale: "en" | "ar", t: (key: string) => string): string {
  if (value == null || !Number.isFinite(value)) return t("costUtilization.na");
  return formatNumberValue(value, locale, { maximumFractionDigits: 2 });
}

function hours(value: number | null | undefined, input: CostUtilizationExportInput): string {
  const formatted = number(value, input.locale, input.translate);
  return formatted === input.translate("costUtilization.na") ? formatted : `${formatted} ${input.translate("costUtilization.hoursUnit")}`;
}

function money(value: number | null | undefined, currency: string | null | undefined, input: CostUtilizationExportInput): string {
  if (!input.report.canViewCosts || value == null || !Number.isFinite(value) || !currency?.trim())
    return input.translate("costUtilization.na");
  const formatted = formatCurrencyValue(value, currency, input.locale, { maximumFractionDigits: 2, numberingSystem: "latn" });
  return formatted === "-" ? input.translate("costUtilization.na") : formatted;
}

function metricRows(metric: CostUtilizationMetric, input: CostUtilizationExportInput): Array<[string, string]> {
  const t = input.translate;
  return [
    [t("costUtilization.capacity"), hours(metric.capacityHours, input)],
    [t("costUtilization.plannedWork"), hours(metric.plannedHours, input)],
    [t("costUtilization.actualWork"), hours(metric.actualHours, input)],
    [t("costUtilization.plannedUtilization"), metric.plannedUtilization == null ? t("costUtilization.na") : `${number(metric.plannedUtilization, input.locale, t)}%`],
    [t("costUtilization.actualUtilization"), metric.actualUtilization == null ? t("costUtilization.na") : `${number(metric.actualUtilization, input.locale, t)}%`],
    [t("costUtilization.excess"), hours(metric.excessHours, input)],
    [t("costUtilization.overtime"), hours(metric.overtimeHours, input)],
  ];
}

function pdfCell(text: string, alignment: "left" | "right", bold = false) {
  return { text, alignment, bold, margin: [4, 4, 4, 4] };
}

function pdfTable(headers: string[], rows: string[][], input: CostUtilizationExportInput) {
  const alignment = input.locale === "ar" ? "right" : "left";
  const body = [headers, ...rows].map((row, rowIndex) => row.map((value) => pdfCell(value, alignment, rowIndex === 0)));
  return {
    table: { headerRows: 1, widths: headers.map(() => "*"), body },
    layout: "lightHorizontalLines",
    fontSize: 8,
    margin: [0, 0, 0, 12],
  };
}

function pdfSection(title: string, input: CostUtilizationExportInput) {
  return { text: title, style: "sectionTitle", alignment: input.locale === "ar" ? "right" : "left", margin: [0, 10, 0, 6], keepWithNext: true };
}

function groupByCurrency(rows: readonly CostUtilizationDaily[]): Map<string, CostUtilizationDaily[]> {
  const groups = new Map<string, CostUtilizationDaily[]>();
  for (const row of rows) {
    const currency = row.currency?.trim().toUpperCase() ?? "";
    const group = groups.get(currency) ?? [];
    group.push(row);
    groups.set(currency, group);
  }
  return new Map([...groups.entries()].sort(([left], [right]) => left.localeCompare(right)));
}

function activityRows(report: CostUtilizationReport) {
  return report.daily.flatMap((day) => (day.activities ?? []).map((activity) => ({ day, activity })));
}

function costValue(metric: CostUtilizationMetric | CostUtilizationDaily, field: typeof costFields[number][0]): number | null {
  const value = metric[field];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function sumComplete(rows: readonly CostUtilizationDaily[], field: typeof costFields[number][0]): number | null {
  if (rows.length === 0) return null;
  const values = rows.map((row) => costValue(row, field));
  return values.every((value): value is number => value != null) ? values.reduce((sum, value) => sum + value, 0) : null;
}

function calendarOperationalRows(report: CostUtilizationReport) {
  const byDate = new Map<string, Map<string, CostUtilizationDaily>>();
  for (const row of report.daily) {
    const date = row.workDate.slice(0, 10);
    const key = `${date}|${row.resourceId}`;
    const day = byDate.get(date) ?? new Map<string, CostUtilizationDaily>();
    if (!day.has(key)) day.set(key, row);
    byDate.set(date, day);
  }
  return [...byDate.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([date, resources]) => {
    const rows = [...resources.values()];
    return {
      date,
      capacity: rows.reduce((sum, row) => sum + row.capacityHours, 0),
      planned: rows.reduce((sum, row) => sum + row.plannedHours, 0),
      actual: rows.reduce((sum, row) => sum + row.actualHours, 0),
      excess: rows.reduce((sum, row) => sum + row.excessHours, 0),
      overtime: rows.reduce((sum, row) => sum + row.overtimeHours, 0),
    };
  });
}

function calendarCostRows(report: CostUtilizationReport) {
  const groups = new Map<string, CostUtilizationDaily[]>();
  for (const row of report.daily) {
    const date = row.workDate.slice(0, 10);
    const currency = row.currency?.trim().toUpperCase() ?? "";
    const key = `${date}|${currency}`;
    const group = groups.get(key) ?? [];
    group.push(row);
    groups.set(key, group);
  }
  return [...groups.entries()].sort(([left], [right]) => left.localeCompare(right)).map(([key, rows]) => {
    const [date, currency] = key.split("|");
    return { date, currency, rows };
  });
}

function pdfHeader(input: CostUtilizationExportInput) {
  const alignment = input.locale === "ar" ? "right" : "left";
  const filters = input.filters.length > 0
    ? input.filters.map((filter) => `${filter.label}: ${filter.value}`).join(" | ")
    : input.translate("costUtilization.allFilters");
  return [
    { text: input.title, style: "title", alignment, margin: [0, 0, 0, 4] },
    { text: input.reportTypeLabel, style: "subtitle", alignment, margin: [0, 0, 0, 4] },
    { text: `${input.translate("costUtilization.reportPeriod")}: ${formatDate(input.from, input.locale)} – ${formatDate(input.to, input.locale)}`, alignment, margin: [0, 0, 0, 4] },
    { text: `${input.translate("costUtilization.reportFilters")}: ${filters}`, alignment, color: "#64748B", fontSize: 9, margin: [0, 0, 0, 10] },
  ];
}

export function buildCostUtilizationPdfDefinition(input: CostUtilizationExportInput): Record<string, unknown> {
  const { report, translate: t } = input;
  const alignment = input.locale === "ar" ? "right" : "left";
  const content: unknown[] = [...pdfHeader(input)];
  if (report.resources.length === 0 && report.daily.length === 0) {
    content.push({ text: t("costUtilization.noData"), alignment, color: "#64748B", margin: [0, 8, 0, 12] });
  }

  if (input.reportType === "summary") {
    content.push(pdfSection(t("costUtilization.reportTotals"), input));
    content.push(pdfTable([t("costUtilization.metric"), t("costUtilization.value")], metricRows(report.operational, input), input));
    if (report.canViewCosts && report.byCurrency.length > 0) {
      content.push(pdfSection(t("costUtilization.currencyCosts"), input));
      const costHeaders = [t("costUtilization.currency"), ...costFields.map(([, label]) => t(`costUtilization.${label}`))];
      const costRows = report.byCurrency.map((metric) => [metric.currency ?? t("costUtilization.na"), ...costFields.map(([field]) => money(metric[field], metric.currency, input))]);
      content.push(pdfTable(costHeaders, costRows, input));
    }
    if (input.includeBreakdowns && report.resources.length > 0) {
      content.push(pdfSection(t("costUtilization.resourceBreakdown"), input));
      const headers = [t("costUtilization.resource"), t("costUtilization.type"), t("costUtilization.department"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.currency")];
      if (report.canViewCosts) headers.push(t("costUtilization.actualCost"), t("costUtilization.overtimeCost"), t("costUtilization.totalCost"));
      const rows = report.resources.map((resource) => [
        resource.resourceName,
        resource.resourceType,
        resource.departmentName ?? t("costUtilization.na"),
        hours(resource.metrics.plannedHours, input),
        hours(resource.metrics.actualHours, input),
        resource.metrics.currency ?? t("costUtilization.na"),
        ...(report.canViewCosts ? [money(resource.metrics.actualLaborCost, resource.metrics.currency, input), money(resource.metrics.overtimeCost, resource.metrics.currency, input), money(resource.metrics.totalResourceCost, resource.metrics.currency, input)] : []),
      ]);
      content.push(pdfTable(headers, rows, input));
    }
    if (input.includeBreakdowns) {
      const activities = activityRows(report);
      if (activities.length > 0) {
        content.push(pdfSection(t("costUtilization.activities"), input));
        content.push(pdfTable(
          [t("costUtilization.from"), t("costUtilization.activity"), t("costUtilization.project"), t("costUtilization.resource"), t("costUtilization.workItems"), t("costUtilization.plannedWork"), t("costUtilization.actualWork")],
          activities.map(({ day, activity }) => [formatDate(day.workDate, input.locale), activity.activityName ?? t("costUtilization.activityNameUnavailable"), activity.projectName ?? t("costUtilization.na"), day.resourceName, activity.workItemTitles.join(", ") || t("costUtilization.na"), hours(activity.plannedHours, input), hours(activity.actualHours, input)]),
          input,
        ));
      }
    }
  }

  if (input.reportType === "dailyDetail" || input.reportType === "dayDetails") {
    content.push(pdfSection(input.reportType === "dayDetails" ? t("costUtilization.dayDetails") : t("costUtilization.daily"), input));
    const headers = [t("costUtilization.from"), t("costUtilization.resource"), t("costUtilization.capacity"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.excess"), t("costUtilization.overtime")];
    if (report.canViewCosts) headers.push(t("costUtilization.currency"), t("costUtilization.plannedCost"), t("costUtilization.actualCost"), t("costUtilization.variance"), t("costUtilization.overtimeCost"), t("costUtilization.unusedCost"), t("costUtilization.totalCost"));
    const rows = [...groupByCurrency(report.daily).entries()].flatMap(([currency, group]) => group.map((row) => [
      formatDate(row.workDate, input.locale),
      row.resourceName,
      hours(row.capacityHours, input),
      hours(row.plannedHours, input),
      hours(row.actualHours, input),
      hours(row.excessHours, input),
      hours(row.overtimeHours, input),
      ...(report.canViewCosts ? [currency || t("costUtilization.na"), money(row.plannedLaborCost, currency, input), money(row.actualLaborCost, currency, input), money(row.laborCostVariance, currency, input), money(row.overtimeCost, currency, input), money(row.unusedCapacityCost, currency, input), money(row.totalResourceCost, currency, input)] : []),
    ]));
    content.push(pdfTable(headers, rows, input));
    if (input.includeBreakdowns) {
      const activities = activityRows(report);
      content.push(pdfSection(t("costUtilization.activities"), input));
      content.push(pdfTable(
        [t("costUtilization.from"), t("costUtilization.activity"), t("costUtilization.project"), t("costUtilization.resource"), t("costUtilization.workItems"), t("costUtilization.plannedWork"), t("costUtilization.actualWork")],
        activities.map(({ day, activity }) => [formatDate(day.workDate, input.locale), activity.activityName ?? t("costUtilization.activityNameUnavailable"), activity.projectName ?? t("costUtilization.na"), day.resourceName, activity.workItemTitles.join(", ") || t("costUtilization.na"), hours(activity.plannedHours, input), hours(activity.actualHours, input)]),
        input,
      ));
    }
  }

  if (input.reportType === "calendarSummary") {
    const operational = calendarOperationalRows(report);
    content.push(pdfSection(t("costUtilization.calendarHours"), input));
    content.push(pdfTable(
      [t("costUtilization.from"), t("costUtilization.capacity"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.excess"), t("costUtilization.overtime")],
      operational.map((row) => [formatDate(row.date, input.locale), hours(row.capacity, input), hours(row.planned, input), hours(row.actual, input), hours(row.excess, input), hours(row.overtime, input)]),
      input,
    ));
    if (report.canViewCosts) {
      content.push(pdfSection(t("costUtilization.calendarCosts"), input));
      const rows = calendarCostRows(report).map(({ date, currency, rows: dayRows }) => [
        formatDate(date, input.locale), currency || t("costUtilization.na"),
        ...costFields.map(([field]) => money(sumComplete(dayRows, field), currency, input)),
      ]);
      content.push(pdfTable([t("costUtilization.from"), t("costUtilization.currency"), ...costFields.map(([, label]) => t(`costUtilization.${label}`))], rows, input));
    }
    if (input.includeBreakdowns) {
      content.push(pdfSection(t("costUtilization.resourceBreakdown"), input));
      const dailyRows = [...groupByCurrency(report.daily).entries()].flatMap(([currency, group]) => group.map((row) => [formatDate(row.workDate, input.locale), row.resourceName, currency || t("costUtilization.na"), hours(row.plannedHours, input), hours(row.actualHours, input), ...(report.canViewCosts ? [money(row.actualLaborCost, currency, input)] : [])]));
      content.push(pdfTable([t("costUtilization.from"), t("costUtilization.resource"), t("costUtilization.currency"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), ...(report.canViewCosts ? [t("costUtilization.actualCost")] : [])], dailyRows, input));
      const activities = activityRows(report);
      if (activities.length > 0) {
        content.push(pdfSection(t("costUtilization.activities"), input));
        content.push(pdfTable(
          [t("costUtilization.from"), t("costUtilization.activity"), t("costUtilization.project"), t("costUtilization.resource"), t("costUtilization.workItems"), t("costUtilization.plannedWork"), t("costUtilization.actualWork")],
          activities.map(({ day, activity }) => [formatDate(day.workDate, input.locale), activity.activityName ?? t("costUtilization.activityNameUnavailable"), activity.projectName ?? t("costUtilization.na"), day.resourceName, activity.workItemTitles.join(", ") || t("costUtilization.na"), hours(activity.plannedHours, input), hours(activity.actualHours, input)]),
          input,
        ));
      }
    }
  }

  return {
    rtl: input.locale === "ar",
    pageSize: "A4",
    pageOrientation: "landscape",
    pageMargins: [30, 34, 30, 42],
    defaultStyle: { font: input.locale === "ar" ? "Cairo" : "Roboto", fontSize: 9, color: "#202833", lineHeight: 1.2 },
    styles: {
      title: { fontSize: 19, bold: true, color: "#164E63" },
      subtitle: { fontSize: 12, bold: true, color: "#475569" },
      sectionTitle: { fontSize: 13, bold: true, color: "#164E63" },
    },
    info: { title: input.title, author: brand.product.fullName, subject: input.reportTypeLabel },
    content,
    footer: (currentPage: number, pageCount: number) => ({
      text: `${brand.product.fullName} | ${formatDate(input.from, input.locale)} – ${formatDate(input.to, input.locale)} | ${t("costUtilization.page")} ${currentPage} ${t("costUtilization.of")} ${pageCount}`,
      alignment: "center",
      fontSize: 8,
      color: "#64748B",
      margin: [0, 10, 0, 0],
    }),
  };
}

function workbookRows(input: CostUtilizationExportInput): Array<Array<ExportCell>> {
  const { report, translate: t } = input;
  const rows: Array<Array<ExportCell>> = [];
  const addMetric = (section: string, metric: string, value: number | null | undefined, unit = "") => rows.push([section, metric, "", value ?? null, unit]);
  const operational = report.operational;
  const totalsSection = t("costUtilization.reportTotals");
  addMetric(totalsSection, t("costUtilization.capacity"), operational.capacityHours, t("costUtilization.hoursUnit"));
  addMetric(totalsSection, t("costUtilization.plannedWork"), operational.plannedHours, t("costUtilization.hoursUnit"));
  addMetric(totalsSection, t("costUtilization.actualWork"), operational.actualHours, t("costUtilization.hoursUnit"));
  addMetric(totalsSection, t("costUtilization.plannedUtilization"), operational.plannedUtilization, "%");
  addMetric(totalsSection, t("costUtilization.actualUtilization"), operational.actualUtilization, "%");
  addMetric(totalsSection, t("costUtilization.excess"), operational.excessHours, t("costUtilization.hoursUnit"));
  addMetric(totalsSection, t("costUtilization.overtime"), operational.overtimeHours, t("costUtilization.hoursUnit"));
  if (report.canViewCosts) {
    const costSection = t("costUtilization.currencyCosts");
    for (const metric of report.byCurrency) {
      for (const [field, labelKey] of costFields)
        rows.push([costSection, t(`costUtilization.${labelKey}`), metric.currency ?? t("costUtilization.na"), metric[field] ?? null, ""]);
    }
  }
  return rows;
}

function createWorkbookSheet(
  workbook: import("exceljs").Workbook,
  name: string,
  headers: string[],
  rows: Array<Array<ExportCell>>,
  input: CostUtilizationExportInput,
  numericColumns: number[] = [],
) {
  const { translate: t } = input;
  const worksheet = workbook.addWorksheet(name.slice(0, 31), { views: [{ state: "frozen", ySplit: 4, rightToLeft: input.locale === "ar" }] });
  worksheet.mergeCells(1, 1, 1, headers.length);
  const title = worksheet.getCell(1, 1);
  title.value = input.title;
  title.font = { bold: true, size: 16, color: { argb: "FF164E63" } };
  title.alignment = { vertical: "middle", horizontal: input.locale === "ar" ? "right" : "left" };
  worksheet.getRow(1).height = 26;
  worksheet.mergeCells(2, 1, 2, headers.length);
  worksheet.getCell(2, 1).value = `${t("costUtilization.reportPeriod")}: ${input.from} – ${input.to} | ${input.reportTypeLabel}`;
  worksheet.getCell(2, 1).font = { color: { argb: "FF475569" }, size: 10 };
  worksheet.getCell(2, 1).alignment = { wrapText: true, horizontal: input.locale === "ar" ? "right" : "left" };
  worksheet.mergeCells(3, 1, 3, headers.length);
  worksheet.getCell(3, 1).value = `${t("costUtilization.reportFilters")}: ${input.filters.map((filter) => `${filter.label}: ${filter.value}`).join(" | ") || t("costUtilization.allFilters")}`;
  worksheet.getCell(3, 1).font = { color: { argb: "FF64748B" }, size: 9 };
  worksheet.getCell(3, 1).alignment = { wrapText: true, horizontal: input.locale === "ar" ? "right" : "left" };
  worksheet.addRow([]);
  const headerRow = worksheet.addRow(headers);
  headerRow.font = { bold: true, color: { argb: "FFFFFFFF" }, size: 10 };
  headerRow.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FF0F4C5C" } };
  headerRow.alignment = { vertical: "middle", horizontal: input.locale === "ar" ? "right" : "left", wrapText: true };
  headerRow.height = 22;
  for (const row of rows) worksheet.addRow(row);
  if (rows.length === 0) {
    const emptyRow = worksheet.addRow([t("costUtilization.noData")]);
    if (headers.length > 1) worksheet.mergeCells(emptyRow.number, 1, emptyRow.number, headers.length);
    emptyRow.font = { italic: true, color: { argb: "FF64748B" } };
  }
  headers.forEach((header, index) => {
    const column = worksheet.getColumn(index + 1);
    let width = Math.max(12, Math.min(38, header.length + 3));
    column.eachCell({ includeEmpty: false }, (cell) => { width = Math.max(width, Math.min(42, String(cell.value ?? "").length + 2)); });
    column.width = width;
    column.alignment = { vertical: "middle", horizontal: input.locale === "ar" ? "right" : "left", wrapText: true };
  });
  numericColumns.forEach((columnIndex) => {
    worksheet.getColumn(columnIndex).numFmt = "#,##0.00;[Red]-#,##0.00";
  });
  worksheet.eachRow((row, rowNumber) => {
    if (rowNumber > headerRow.number && rowNumber % 2 === 0) row.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF1F5F9" } };
  });
  if (rows.length > 0) worksheet.autoFilter = { from: { row: headerRow.number, column: 1 }, to: { row: headerRow.number + rows.length, column: headers.length } };
  worksheet.views = [{ state: "frozen", ySplit: headerRow.number, rightToLeft: input.locale === "ar" }];
  return worksheet;
}

function dailyDetailRows(input: CostUtilizationExportInput): Array<Array<ExportCell>> {
  const { report, locale, translate: t } = input;
  return [...groupByCurrency(report.daily).entries()].flatMap(([currency, rows]) => rows.map((row) => [
    row.workDate.slice(0, 10), row.resourceName, currency || t("costUtilization.na"), row.capacityHours, row.plannedHours, row.actualHours,
    row.excessHours, row.overtimeHours, row.plannedUtilization ?? null, row.actualUtilization ?? null,
    ...(report.canViewCosts ? [row.plannedLaborCost ?? null, row.actualLaborCost ?? null, row.laborCostVariance ?? null, row.overtimeCost ?? null, row.unusedCapacityCost ?? null, row.totalResourceCost ?? null] : []),
    locale,
  ]));
}

export async function buildCostUtilizationWorkbook(input: CostUtilizationExportInput): Promise<import("exceljs").Workbook> {
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  workbook.creator = brand.product.fullName;
  workbook.subject = `${input.title} | ${input.from} - ${input.to}`;
  workbook.created = new Date();
  createWorkbookSheet(
    workbook,
    input.translate("costUtilization.reportSummary"),
    [input.translate("costUtilization.section"), input.translate("costUtilization.metric"), input.translate("costUtilization.currency"), input.translate("costUtilization.value"), input.translate("costUtilization.unit")],
    workbookRows(input),
    input,
    [4],
  );

  const t = input.translate;
  if (input.reportType === "summary" && input.includeBreakdowns) {
    const resourceHeaders = [t("costUtilization.resource"), t("costUtilization.type"), t("costUtilization.department"), t("costUtilization.vendor"), t("costUtilization.currency"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.actualUtilization")];
    if (input.report.canViewCosts) resourceHeaders.push(t("costUtilization.actualCost"), t("costUtilization.overtimeCost"), t("costUtilization.totalCost"));
    const resourceRows = input.report.resources.map((resource) => [
      resource.resourceName, resource.resourceType, resource.departmentName ?? t("costUtilization.na"), resource.vendorName ?? t("costUtilization.na"), resource.metrics.currency ?? t("costUtilization.na"), resource.metrics.plannedHours, resource.metrics.actualHours, resource.metrics.actualUtilization ?? null,
      ...(input.report.canViewCosts ? [resource.metrics.actualLaborCost ?? null, resource.metrics.overtimeCost ?? null, resource.metrics.totalResourceCost ?? null] : []),
    ]);
    createWorkbookSheet(workbook, t("costUtilization.resourceBreakdown"), resourceHeaders, resourceRows, input, [6, 7, 8, ...(input.report.canViewCosts ? [9, 10, 11] : [])]);
  }

  if (input.reportType === "dailyDetail" || input.reportType === "dayDetails") {
    const headers = [t("costUtilization.from"), t("costUtilization.resource"), t("costUtilization.currency"), t("costUtilization.capacity"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.excess"), t("costUtilization.overtime"), t("costUtilization.plannedUtilization"), t("costUtilization.actualUtilization")];
    if (input.report.canViewCosts) headers.push(...costFields.map(([, label]) => t(`costUtilization.${label}`)));
    const rows = dailyDetailRows(input).map((row) => row.slice(0, headers.length));
    createWorkbookSheet(workbook, input.reportType === "dayDetails" ? t("costUtilization.dayDetails") : t("costUtilization.daily"), headers, rows, input, [4, 5, 6, 7, 8, 9, 10, ...(input.report.canViewCosts ? [11, 12, 13, 14, 15, 16] : [])]);
  }

  if (input.reportType === "calendarSummary") {
    const operational = calendarOperationalRows(input.report);
    createWorkbookSheet(workbook, t("costUtilization.calendarHours"),
      [t("costUtilization.from"), t("costUtilization.capacity"), t("costUtilization.plannedWork"), t("costUtilization.actualWork"), t("costUtilization.excess"), t("costUtilization.overtime")],
      operational.map((row) => [row.date, row.capacity, row.planned, row.actual, row.excess, row.overtime]), input, [2, 3, 4, 5, 6]);
    if (input.report.canViewCosts) {
      const costs = calendarCostRows(input.report);
      const costHeaders = [t("costUtilization.from"), t("costUtilization.currency"), ...costFields.map(([, label]) => t(`costUtilization.${label}`))];
      const rows = costs.map(({ date, currency, rows: dayRows }) => [date, currency || t("costUtilization.na"), ...costFields.map(([field]) => sumComplete(dayRows, field))]);
      createWorkbookSheet(workbook, t("costUtilization.calendarCosts"), costHeaders, rows, input, [3, 4, 5, 6, 7, 8]);
    }
  }

  if (input.includeBreakdowns) {
    const activities = activityRows(input.report);
    const activityHeaders = [t("costUtilization.from"), t("costUtilization.activity"), t("costUtilization.project"), t("costUtilization.resource"), t("costUtilization.workItems"), t("costUtilization.plannedWork"), t("costUtilization.actualWork")];
    const rows = activities.map(({ day, activity }) => [day.workDate.slice(0, 10), activity.activityName ?? t("costUtilization.activityNameUnavailable"), activity.projectName ?? t("costUtilization.na"), day.resourceName, activity.workItemTitles.join(", "), activity.plannedHours, activity.actualHours]);
    createWorkbookSheet(workbook, t("costUtilization.activities"), activityHeaders, rows, input, [6, 7]);
  }

  if (input.reportType === "calendarSummary" && input.includeBreakdowns) {
    const rows = dailyDetailRows(input);
    const headers = [t("costUtilization.from"), t("costUtilization.resource"), t("costUtilization.currency"), t("costUtilization.plannedWork"), t("costUtilization.actualWork")];
    if (input.report.canViewCosts) headers.push(t("costUtilization.actualCost"));
    createWorkbookSheet(workbook, t("costUtilization.resourceBreakdown"), headers, rows.map((row) => [row[0], row[1], row[2], row[4], row[5], ...(input.report.canViewCosts ? [row[11]] : [])]), input, [4, 5, ...(input.report.canViewCosts ? [6] : [])]);
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

function exportFilename(input: CostUtilizationExportInput, extension: string): string {
  return `FOPX_Cost_Utilization_${input.from}_${input.to}.${extension}`;
}

export async function exportCostUtilizationPdf(input: CostUtilizationExportInput): Promise<void> {
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const vfsModule = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(vfsModule.default);
  pdfMake.addFonts({
    Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" },
    Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" },
  });
  const blob = await pdfMake.createPdf(buildCostUtilizationPdfDefinition(input)).getBlob();
  downloadBlob(blob, exportFilename(input, "pdf"));
}

export async function exportCostUtilizationExcel(input: CostUtilizationExportInput): Promise<void> {
  const workbook = await buildCostUtilizationWorkbook(input);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  downloadBlob(blob, exportFilename(input, "xlsx"));
}
