import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { buildProjectStatusReportDocument, buildProjectStatusReportWorkbook, exportProjectStatusReportPdf, projectStatusReportExcelFilename, projectStatusReportPdfFilename } from "@lib/export/project-status-report-export";
import type { ProjectStatusReportDto } from "@features/projects/api/projects";

const report: ProjectStatusReportDto = {
  id: "report-1",
  projectId: "project-1",
  reportDate: "2026-09-30",
  periodFrom: "2026-09-01",
  periodTo: "2026-09-30",
  statusDate: "2026-09-30",
  reportState: "Published",
  projectNameSnapshot: "Core Banking Modernization",
  projectCodeSnapshot: "PRJ-1001",
  projectManagerNameSnapshot: "Shady Ismael",
  overallHealth: "Amber",
  overallProgressPercent: 72.5,
  scheduleHealth: "Amber",
  costHealth: "Green",
  scopeHealth: "Green",
  qualityHealth: "Red",
  resourceHealth: "Amber",
  executiveSummary: "The reporting period closed with controlled delivery progress.",
  publishedByNameSnapshot: "Alex Admin",
  sourceDataCapturedAt: "2026-09-30T14:00:00Z",
  reportingConfigurationVersionSnapshot: 3,
  preparedByNameSnapshot: "Shady Ismael",
  currencyCodeSnapshot: "USD",
  budgetAmountSnapshot: 100000,
  actualCostAmountSnapshot: 72000,
  costVarianceAmountSnapshot: -3000,
  scheduleVarianceDaysSnapshot: 2,
  scheduleVariancePercentSnapshot: 4.5,
  baselineNameSnapshot: "Approved Baseline",
  baselineVersionSnapshot: 2,
  risks: [{ sourceRiskId: "risk-1", rank: 1, title: "Vendor dependency", riskLevel: "High", riskStatus: "Open", ownerNameSnapshot: "Alex Admin" }],
  issues: [{ sourceIssueId: "issue-1", rank: 1, title: "Environment delay", severity: "Medium", priority: "High", issueStatus: "InProgress", assigneeNameSnapshot: "Shady Ismael" }],
  changeRequests: [{ sourceChangeRequestId: "change-1", rank: 1, number: 4, changeType: "Baseline", reason: "Schedule adjustment", changeRequestStatus: "Pending", requestedBy: "user-1", requestedByNameSnapshot: "Alex Admin", requestedAt: "2026-09-20" }],
  performanceSnapshot: {
    projectId: "project-1", statusDate: "2026-09-30", perspective: "Consolidated", dataQuality: "Partial", reportingConfigurationVersion: 3, currency: "USD",
    overallProgressPercent: 72.5, plannedProgressPercent: 75, budgetAtCompletion: 100000, plannedValue: 75000, earnedValue: 72500, actualCost: 72000,
    unallocatedProjectActualCost: 1200, scheduleVariance: -2500, costVariance: 500, schedulePerformanceIndex: 0.97, costPerformanceIndex: 1.01,
    dataQualityReasons: ["INCOMPLETE_COST_ATTRIBUTION"], plans: [{ planId: "plan-1", planName: "Core Implementation", planType: "Execution", classification: "Execution", planStatus: "Active", includedInOfficialReporting: true, baselineName: "Approved Baseline", plannedProgressPercent: 75, actualProgressPercent: 72.5, budgetAtCompletion: 100000, plannedValue: 75000, earnedValue: 72500, actualCost: 72000, schedulePerformanceIndex: 0.97, costPerformanceIndex: 1.01, dataQualityReasons: [] }],
    availablePlans: [], milestones: [{ planId: "plan-1", planName: "Core Implementation", name: "Pilot", baselineDate: "2026-09-20", plannedDate: "2026-09-22", actualCompletionDate: "2026-09-23", varianceDays: 3, status: "Completed" }], criticalPathMode: "ByPlan",
  },
};

describe("project status report PDF document", () => {
  it("builds the English document from the API snapshot", () => {
    const document = buildProjectStatusReportDocument({ report, locale: "en" });
    const serialized = JSON.stringify(document);
    expect(document.rtl).toBe(false);
    expect(serialized).toContain("Core Banking Modernization");
    expect(serialized).toContain("Plan Performance");
    expect(serialized).toContain("Vendor dependency");
    expect(serialized).toContain("Schedule adjustment");
  });

  it("builds an Arabic RTL document and a safe filename", () => {
    const document = buildProjectStatusReportDocument({ report, locale: "ar" });
    expect(document.rtl).toBe(true);
    const serialized = JSON.stringify(document);
    expect(serialized).toContain("تقرير حالة المشروع");
    expect(serialized).toContain("أداء الخطط");
    expect(serialized).toContain("$100,000.00");
    const reportDate = serialized.match(/تاريخ التقرير: ([^"]+)/)?.[1];
    expect(reportDate).toContain("30");
    expect(reportDate).toContain("2026");
    expect(reportDate).not.toMatch(/[٠-٩]/);
    expect(projectStatusReportPdfFilename(report)).toBe("ProjectStatus_PRJ-1001_2026-09-30.pdf");
  });

  it("exports the immutable API snapshot to a multi-sheet Excel workbook", async () => {
    const workbook = await buildProjectStatusReportWorkbook({ report, locale: "en" });
    expect(workbook.worksheets.map((sheet) => sheet.name)).toEqual(["Report Summary", "Project Performance", "Plan Performance", "Milestones", "Risks", "Issues", "Decisions Required"]);
    expect(workbook.getWorksheet("Project Performance")?.getCell("B5").value).toBe(100000);
    expect(workbook.getWorksheet("Plan Performance")?.getCell("A2").value).toBe("Core Implementation");
    expect(workbook.getWorksheet("Milestones")?.getCell("B2").value).toBe("Pilot");
    expect(workbook.getWorksheet("Risks")?.getCell("A2").value).toBe("Vendor dependency");
    expect(workbook.getWorksheet("Decisions Required")?.getCell("B2").value).toBe("Schedule adjustment");
    expect(projectStatusReportExcelFilename(report)).toBe("ProjectStatus_PRJ-1001_2026-09-30.xlsx");

    const bytes = await workbook.xlsx.writeBuffer();
    const ExcelJS = (await import("exceljs")).default;
    const reopened = new ExcelJS.Workbook();
    await reopened.xlsx.load(bytes);
    expect(reopened.worksheets.map((sheet) => sheet.name)).toEqual(workbook.worksheets.map((sheet) => sheet.name));
    expect(reopened.getWorksheet("Project Performance")?.getCell("B5").value).toBe(100000);
    expect(reopened.getWorksheet("Plan Performance")?.getCell("A2").value).toBe("Core Implementation");
    expect(reopened.getWorksheet("Risks")?.getCell("A2").value).toBe("Vendor dependency");
    await saveExportTestFile("ProjectStatus_EN.xlsx", Buffer.from(bytes));
  });

  it("exports only configured sections and limits decisions to pending items", async () => {
    const configured = {
      ...report,
      includedSectionsSnapshot: ["ExecutiveSummary", "DecisionsRequired"],
      changeRequests: [
        ...report.changeRequests,
        { sourceChangeRequestId: "change-2", rank: 2, number: 5, changeType: "Administrative", reason: "Already approved", changeRequestStatus: "Approved", requestedBy: "user-2", requestedAt: "2026-09-22" },
      ],
    };
    const document = JSON.stringify(buildProjectStatusReportDocument({ report: configured, locale: "en" }));
    expect(document).toContain("The reporting period closed with controlled delivery progress.");
    expect(document).toContain("Schedule adjustment");
    expect(document).not.toContain("Vendor dependency");
    expect(document).not.toContain("Core Implementation");

    const workbook = await buildProjectStatusReportWorkbook({ report: configured, locale: "en" });
    expect(workbook.worksheets.map((sheet) => sheet.name)).toEqual(["Report Summary", "Decisions Required"]);
    expect(workbook.getWorksheet("Decisions Required")?.rowCount).toBe(2);
  });

  it("generates a real, reopenable PDF file from a published report snapshot", async () => {
    const blobs: Blob[] = [];
    const createObjectUrl = vi.fn((blob: Blob) => {
      blobs.push(blob);
      return "blob:status-report-export-test";
    });
    vi.stubGlobal("URL", { createObjectURL: createObjectUrl, revokeObjectURL: vi.fn() });
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);

    try {
      await exportProjectStatusReportPdf({ report, locale: "en" });
      expect(createObjectUrl).toHaveBeenCalledOnce();
      expect(blobs).toHaveLength(1);
      const bytes = await readBlobBytes(blobs[0]);
      expect(bytes.subarray(0, 5).toString("ascii")).toBe("%PDF-");
      expect(bytes.length).toBeGreaterThan(5_000);
      await saveExportTestFile("ProjectStatus_EN.pdf", bytes);

      await exportProjectStatusReportPdf({ report, locale: "ar" });
      expect(blobs).toHaveLength(2);
      const arabicPdfBytes = await readBlobBytes(blobs[1]);
      expect(arabicPdfBytes.subarray(0, 5).toString("ascii")).toBe("%PDF-");
      expect(arabicPdfBytes.length).toBeGreaterThan(5_000);
      await saveExportTestFile("ProjectStatus_AR.pdf", arabicPdfBytes);

      const arabic = await buildProjectStatusReportWorkbook({ report, locale: "ar" });
      expect(arabic.worksheets.map((sheet) => sheet.name)).toContain("ملخص التقرير");
      expect(arabic.getWorksheet("أداء المشروع")?.views[0]?.rightToLeft).toBe(true);
      const arabicBytes = await arabic.xlsx.writeBuffer();
      const ExcelJS = (await import("exceljs")).default;
      const reopenedArabic = new ExcelJS.Workbook();
      await reopenedArabic.xlsx.load(arabicBytes);
      expect(reopenedArabic.getWorksheet("ملخص التقرير")?.getCell("A2").value).toBe("تقرير حالة المشروع");
      expect(reopenedArabic.getWorksheet("أداء المشروع")?.views[0]?.rightToLeft).toBe(true);
      await saveExportTestFile("ProjectStatus_AR.xlsx", Buffer.from(arabicBytes));
    } finally {
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    }
  });
});

async function saveExportTestFile(fileName: string, content: Buffer): Promise<void> {
  const outputDirectory = process.env.NEXUS_REPORT_EXPORT_TEST_OUTPUT_DIR;
  if (!outputDirectory) return;
  await mkdir(outputDirectory, { recursive: true });
  await writeFile(join(outputDirectory, fileName), content);
}

function readBlobBytes(blob: Blob): Promise<Buffer> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error("Failed to read exported file."));
    reader.onload = () => {
      if (!(reader.result instanceof ArrayBuffer)) {
        reject(new Error("Exported file could not be read as binary data."));
        return;
      }
      resolve(Buffer.from(reader.result));
    };
    reader.readAsArrayBuffer(blob);
  });
}
