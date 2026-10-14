import { describe, expect, it } from "vitest";
import { buildProjectStatusReportDocument, projectStatusReportPdfFilename } from "@lib/export/project-status-report-export";
import type { ProjectStatusReportDto } from "@features/projects/api/projects";

const report: ProjectStatusReportDto = {
  id: "report-1",
  projectId: "project-1",
  reportDate: "2026-09-30",
  periodFrom: "2026-09-01",
  periodTo: "2026-09-30",
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
};

describe("project status report PDF document", () => {
  it("builds the English document from the API snapshot", () => {
    const document = buildProjectStatusReportDocument({ report, locale: "en" });
    const serialized = JSON.stringify(document);
    expect(document.rtl).toBe(false);
    expect(serialized).toContain("Core Banking Modernization");
    expect(serialized).toContain("Vendor dependency");
    expect(serialized).toContain("Schedule adjustment");
  });

  it("builds an Arabic RTL document and a safe filename", () => {
    const document = buildProjectStatusReportDocument({ report, locale: "ar" });
    expect(document.rtl).toBe(true);
    expect(JSON.stringify(document)).toContain("تقرير حالة المشروع");
    expect(projectStatusReportPdfFilename(report)).toBe("ProjectStatus_PRJ-1001_2026-09-30.pdf");
  });
});
