import { describe, expect, it } from "vitest";
import { buildCostUtilizationPdfDefinition, buildCostUtilizationWorkbook, type CostUtilizationExportInput } from "../features/resources/cost-utilization-export";
import type { CostUtilizationMetric, CostUtilizationReport } from "../features/resources/api/cost-utilization";

function metric(currency: string, actualLaborCost: number): CostUtilizationMetric {
  return {
    currency,
    capacityHours: 8,
    plannedHours: 5,
    actualHours: 4,
    regularHours: 4,
    planningGapHours: 3,
    executionGapHours: 1,
    unusedCapacityHours: 4,
    excessHours: 0,
    overtimeHours: 0,
    plannedUtilization: 62.5,
    actualUtilization: 50,
    plannedLaborCost: 120,
    actualLaborCost,
    laborCostVariance: actualLaborCost - 120,
    overtimeCost: 0,
    unusedCapacityCost: 40,
    committedCapacityCost: 160,
    totalResourceCost: actualLaborCost + 40,
    costProfileMissing: false,
    costCoverageComplete: true,
    uncostedActualHours: 0,
    uncostedPlannedHours: 0,
  };
}

const sampleReport: CostUtilizationReport = {
  from: "2026-10-01T00:00:00",
  to: "2026-10-02T00:00:00",
  canViewCosts: true,
  operational: { ...metric("", 0), currency: undefined },
  byCurrency: [metric("EGP", 350), metric("USD", 220)],
  resources: [
    { resourceId: "resource-egp", resourceName: "Mona Resource", resourceType: "Staff", departmentName: "Engineering", vendorName: undefined, metrics: metric("EGP", 350) },
    { resourceId: "resource-usd", resourceName: "Sam Resource", resourceType: "Staff", departmentName: "Delivery", vendorName: undefined, metrics: metric("USD", 220) },
  ],
  projects: [],
  vendors: [],
  roles: [],
  daily: [
    { ...metric("EGP", 350), resourceId: "resource-egp", resourceName: "Mona Resource", workDate: "2026-10-01T00:00:00", activities: [{ activityName: "Prepare plan", workItemTitles: ["Plan review"], projectName: "Project North", plannedHours: 5, actualHours: 4 }] },
    { ...metric("USD", 220), resourceId: "resource-usd", resourceName: "Sam Resource", workDate: "2026-10-02T00:00:00", activities: [] },
  ],
  warnings: [],
};

function makeInput(reportType: CostUtilizationExportInput["reportType"] = "summary", locale: "en" | "ar" = "en", report = sampleReport): CostUtilizationExportInput {
  return {
    report,
    locale,
    reportType,
    reportTypeLabel: reportType,
    title: "Cost & Utilization — report",
    from: "2026-10-01",
    to: "2026-10-02",
    filters: [{ label: "Project", value: "Project North" }],
    includeBreakdowns: true,
    translate: (key) => key.startsWith("costUtilization.") ? key.slice("costUtilization.".length) : key,
  };
}

describe("Cost Utilization exports", () => {
  it("creates structured Excel worksheets and keeps each currency in its own rows", async () => {
    const workbook = await buildCostUtilizationWorkbook(makeInput("summary"));
    const summary = workbook.getWorksheet("reportSummary");
    expect(summary).toBeDefined();

    const currencyTotals: string[] = [];
    summary!.eachRow((row) => {
      if (row.getCell(1).value === "currencyCosts") currencyTotals.push(String(row.getCell(3).value));
    });
    expect(currencyTotals).toEqual(["EGP", "EGP", "EGP", "EGP", "EGP", "EGP", "USD", "USD", "USD", "USD", "USD", "USD"]);
    expect(workbook.getWorksheet("resourceBreakdown")).toBeDefined();
    expect(workbook.getWorksheet("activities")).toBeDefined();

    const bytes = await workbook.xlsx.writeBuffer();
    const ExcelJS = (await import("exceljs")).default;
    const reopened = new ExcelJS.Workbook();
    await reopened.xlsx.load(bytes);
    expect(reopened.getWorksheet("reportSummary")?.getCell("A1").value).toBe("Cost & Utilization — report");
  });

  it("builds calendar sheets with daily hours separate from currency-specific cost rows", async () => {
    const workbook = await buildCostUtilizationWorkbook(makeInput("calendarSummary"));
    expect(workbook.getWorksheet("calendarHours")?.rowCount).toBeGreaterThan(5);
    const costs = workbook.getWorksheet("calendarCosts");
    expect(costs).toBeDefined();
    const currencyCodes: string[] = [];
    costs!.eachRow((row) => {
      const currency = row.getCell(2).value;
      if (currency === "EGP" || currency === "USD") currencyCodes.push(currency);
    });
    expect(currencyCodes).toEqual(["EGP", "USD"]);
    expect(workbook.getWorksheet("activities")).toBeDefined();
  });

  it("omits monetary totals when the authorized report response hides costs", async () => {
    const report = { ...sampleReport, canViewCosts: false, byCurrency: [] };
    const workbook = await buildCostUtilizationWorkbook(makeInput("dailyDetail", "en", report));
    const daily = workbook.getWorksheet("daily");
    expect(daily).toBeDefined();
    expect(daily!.getRow(5).values).not.toContain("actualCost");
    expect(daily!.getRow(5).values).toContain("currency");
    expect(daily!.getRow(6).cellCount).toBe(10);
    const pdf = JSON.stringify(buildCostUtilizationPdfDefinition(makeInput("dailyDetail", "en", report)));
    expect(pdf).not.toContain("actualCost");
    expect(pdf).not.toContain("350");
    expect(pdf).not.toContain("220");
    expect(workbook.getWorksheet("activities")).toBeDefined();
  });

  it("builds landscape, RTL PDF definitions with filters and separate currency summaries", () => {
    const doc = buildCostUtilizationPdfDefinition(makeInput("summary", "ar"));
    expect(doc.pageOrientation).toBe("landscape");
    expect(doc.rtl).toBe(true);
    const serialized = JSON.stringify(doc);
    expect(serialized).toContain("EGP");
    expect(serialized).toContain("USD");
    expect(serialized).toContain("Project North");
    expect(serialized).not.toContain("EGP, USD");
  });

  it("generates a multi-page capable PDF blob from the report definition", async () => {
    const pdfMake = (await import("pdfmake-rtl/build/pdfmake.min.js")).default;
    const vfs = (await import("pdfmake-rtl/build/vfs_fonts.js")).default;
    pdfMake.addVirtualFileSystem(vfs);
    pdfMake.addFonts({ Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" }, Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" } });
    const englishBlob = await pdfMake.createPdf(buildCostUtilizationPdfDefinition(makeInput("dailyDetail"))).getBlob();
    const arabicBlob = await pdfMake.createPdf(buildCostUtilizationPdfDefinition(makeInput("calendarSummary", "ar"))).getBlob();
    expect(englishBlob.size).toBeGreaterThan(0);
    expect(arabicBlob.size).toBeGreaterThan(0);
  });

  it("keeps empty exports usable and labels the empty result", async () => {
    const emptyReport = {
      ...sampleReport,
      resources: [],
      daily: [],
      byCurrency: [],
    };
    const input = makeInput("dailyDetail", "en", emptyReport);
    const workbook = await buildCostUtilizationWorkbook(input);
    expect(workbook.getWorksheet("daily")?.getRow(6).getCell(1).value).toBe("noData");
    expect(JSON.stringify(buildCostUtilizationPdfDefinition(input))).toContain("noData");
  });
});
