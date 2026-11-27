import { describe, expect, it } from "vitest";
import {
  buildFinancialReportingPdfDefinition,
  buildFinancialReportingWorkbook,
  type FinancialReportingExportInput,
  type ProjectFinancialReportingExportInput,
  type RollupFinancialReportingExportInput,
} from "../features/finance/financial-reporting-export";
import type { ProjectFinancialReporting } from "../features/projects/api/project-financials";
import type { ProgramPortfolioFinancialRollup } from "../features/delivery/api/delivery";

const translate = (key: string, params?: Record<string, string | number>) => {
  const base = key.startsWith("financialReportingExport.") ? key.slice("financialReportingExport.".length) : key;
  return params && "count" in params ? `${base}(${String(params.count)})` : base;
};

const generatedAt = "2026-11-23T12:00:00Z";

function projectReport(overrides: Partial<ProjectFinancialReporting> = {}): ProjectFinancialReporting {
  return {
    projectId: "p1", projectCode: "PRJ-1", projectName: "Project Atlas", currency: "USD",
    lifetimeCeiling: 1_000_000, annualApprovedBudget: 500_000, lifetimeReserved: 100_000,
    outstandingCommitments: 50_000, grossActualPaid: 200_000, confirmedRefundsAndCorrections: -10_000,
    netActualPaid: 190_000, availableLifetimeBudget: 700_000, annualBudgetUtilizationPercentage: 40,
    budgetLineUtilizations: [
      { budgetLineId: "bl1", reference: "BL-1", description: "Works", authorizedLimit: 1000, reservedAmount: 250, utilizationPercentage: 25, hasCurrencyMismatch: false },
    ],
    dataQuality: {
      hasUnresolvedCurrency: false, hasCurrencyMismatch: false, projectCurrencyUnavailable: false,
      hasMissingFinancialReferences: true, hasIncompleteSettlementHistory: false, hasMissingAnnualBudgetAllocations: false,
      details: ["A budget line is missing a financial reference."], hasAnyGap: true, overallStatus: "Partial",
    },
    ...overrides,
  } as ProjectFinancialReporting;
}

function currencyGroup(currency: string, approved: number, lifetime: number, count: number) {
  return {
    currency, projectCount: count, approvedAnnualBudget: approved, newApprovedFunding: 0,
    approvedFreeCarryForward: 0, approvedReservedCarryForward: 0, approvedAdjustments: 0,
    effectiveActiveAllocations: approved, availableAnnualBudget: approved, totalUnspentBudget: approved,
    approvedReversalReleases: 0, totalPaymentRequests: 0, draftRequests: 0, pendingApprovalRequests: 0,
    approvedRequests: 0, rejectedRequests: 0, approvedRequestAmount: 0, remainingPayableCommitments: 0,
    actualSettlementsInFiscalYear: approved, grossActualPaid: approved, netActualPaid: approved,
    lifetimeCeiling: lifetime, lifetimeReserved: 0, outstandingCommitments: 0, executedRefundsCorrections: 0,
    availableLifetimeBudget: lifetime, budgetLineUtilization: null, notSettledRequests: 0,
    partiallySettledRequests: 0, fullySettledRequests: 0, freeCarryForward: 0, reservedCarryForward: 0, nonCarriedBalance: 0,
  };
}

function projectRow(name: string, currency: string, approved: number | null, isDirect: boolean, status = "Available") {
  return {
    projectId: name, projectPublicId: name, projectName: name, programId: isDirect ? null : "prog-1",
    programName: isDirect ? null : "Programme Alpha", isDirectProject: isDirect, currency, isAvailable: status === "Available",
    dataStatus: status, approvedAnnualBudget: approved, newApprovedFunding: null, approvedFreeCarryForward: null,
    approvedReservedCarryForward: null, approvedAdjustments: null, effectiveActiveAllocations: null, availableAnnualBudget: null,
    totalUnspentBudget: null, approvedReversalReleases: null, approvedRequestAmount: null, remainingPayableCommitments: null,
    actualSettlementsInFiscalYear: null, grossActualPaid: null, netActualPaid: null, lifetimeCeiling: null, lifetimeReserved: null,
    outstandingCommitments: null, executedRefundsCorrections: null, availableLifetimeBudget: null, budgetLineUtilization: null,
    totalPaymentRequests: 0, draftRequests: 0, pendingApprovalRequests: 0, approvedRequests: 0, rejectedRequests: 0,
    notSettledRequests: 0, partiallySettledRequests: 0, fullySettledRequests: 0, closingStatus: "Closed",
    freeCarryForward: null, reservedCarryForward: null, nonCarriedBalance: null,
  };
}

function rollup(): ProgramPortfolioFinancialRollup {
  return {
    entityType: "Portfolio", entityId: "pf-1", fiscalYear: 2026, fiscalYearLabel: "FY 2026",
    fiscalYearStartDate: "2026-01-01", fiscalYearEndDate: "2026-12-31", tenantTimeZone: "UTC",
    hierarchyBasis: "Direct+Programs", coverage: "Partial", includedProjectCount: 2,
    projectsMissingApprovedAnnualBudget: 1, projectsWithFinancialDataGaps: 0, unresolvedLegacyPaymentRequests: 2,
    hasMixedCurrencies: true, reconciliationStatus: "Provisional",
    closingCoverage: { projectFinancialYears: 1, open: 1, underReview: 0, pendingApproval: 0, closed: 0, projectsWithUnresolvedFinancialData: 0, status: "Open" },
    currencyGroups: [currencyGroup("USD", 500_000, 1_000_000, 1), currencyGroup("EUR", 300_000, 600_000, 1)],
    programBreakdowns: [],
    projects: [projectRow("Project One", "USD", 500_000, false), projectRow("Project Two", "EUR", null, true, "MissingApprovedAnnualBudget")],
    projectsPage: 1, projectsPageSize: 25, projectsTotalCount: 2,
  } as unknown as ProgramPortfolioFinancialRollup;
}

function projectInput(overrides: Partial<ProjectFinancialReporting> = {}): ProjectFinancialReportingExportInput {
  return { level: "Project", report: projectReport(overrides), locale: "en", generatedAt, translate };
}

function rollupInput(locale: "en" | "ar" = "en"): RollupFinancialReportingExportInput {
  return { level: "Portfolio", entityName: "Test Portfolio", report: rollup(), locale, generatedAt, translate };
}

async function reopen(workbook: import("exceljs").Workbook) {
  const ExcelJS = (await import("exceljs")).default;
  const reopened = new ExcelJS.Workbook();
  await reopened.xlsx.load(await workbook.xlsx.writeBuffer());
  return reopened;
}

function sheetText(workbook: import("exceljs").Workbook, name: string): string {
  const sheet = workbook.getWorksheet(name);
  if (!sheet) return "";
  const parts: string[] = [];
  sheet.eachRow((row) => row.eachCell((cell) => parts.push(String(cell.value ?? ""))));
  return parts.join("|");
}

describe("Financial reporting exports", () => {
  it("builds a project workbook with a summary sheet, formatted KPI values and a budget-line sheet", async () => {
    const workbook = await buildFinancialReportingWorkbook(projectInput());
    expect(workbook.getWorksheet("summary")).toBeTruthy();
    expect(workbook.getWorksheet("budgetLines")).toBeTruthy();
    expect(workbook.getWorksheet("dataQuality")).toBeTruthy();

    const reopened = await reopen(workbook);
    expect(String(reopened.getWorksheet("summary")!.getCell("A1").value)).toContain("Project Atlas");

    const summary = workbook.getWorksheet("summary")!;
    let ceilingCell = "";
    summary.eachRow((row) => {
      if (row.getCell(1).value === "lifetimeCeiling") ceilingCell = String(row.getCell(2).value);
    });
    expect(ceilingCell).toContain("1,000,000");
  });

  it("renders null amounts as N/A rather than zero in the PDF definition", () => {
    const input = projectInput({ grossActualPaid: null, netActualPaid: null, lifetimeCeiling: null, annualBudgetUtilizationPercentage: null });
    const json = JSON.stringify(buildFinancialReportingPdfDefinition(input));
    expect(json).toContain("na");
    expect(json).not.toContain("$0");
  });

  it("keeps each currency on its own row and never sums across currencies in the rollup workbook", async () => {
    const workbook = await buildFinancialReportingWorkbook(rollupInput());
    const breakdown = workbook.getWorksheet("currencyBreakdown")!;
    expect(breakdown).toBeTruthy();
    const text = sheetText(workbook, "currencyBreakdown");
    expect(text).toContain("USD");
    expect(text).toContain("EUR");
    expect(text).toContain("500,000");
    expect(text).toContain("300,000");
    // 500,000 + 300,000 must not appear as a single mixed total.
    expect(text).not.toContain("800,000");
    expect(workbook.getWorksheet("projectBreakdown")).toBeTruthy();
  });

  it("surfaces rollup data-quality warnings (coverage, reconciliation, missing budgets, legacy requests)", async () => {
    const workbook = await buildFinancialReportingWorkbook(rollupInput());
    const quality = workbook.getWorksheet("dataQuality");
    expect(quality).toBeTruthy();
    const text = sheetText(workbook, "dataQuality");
    expect(text).toContain("coverage_Partial");
    expect(text).toContain("reconciliationProvisional");
    expect(text).toContain("missingAnnualBudget(1)");
    expect(text).toContain("legacyRequests(2)");
  });

  it("builds landscape PDF definitions, reflects the report level and toggles RTL for Arabic", () => {
    const portfolio = buildFinancialReportingPdfDefinition(rollupInput());
    expect(portfolio.pageOrientation).toBe("landscape");
    expect(portfolio.rtl).toBe(false);
    const portfolioJson = JSON.stringify(portfolio);
    expect(portfolioJson).toContain("currencyBreakdown");
    expect(portfolioJson).toContain("USD");
    expect(portfolioJson).toContain("EUR");
    expect(portfolioJson).toContain("levelPortfolio");

    const projectJson = JSON.stringify(buildFinancialReportingPdfDefinition(projectInput()));
    expect(projectJson).toContain("levelProject");

    const arabic = buildFinancialReportingPdfDefinition(rollupInput("ar"));
    expect(arabic.rtl).toBe(true);
  });

  it("includes the generation timestamp in the workbook and PDF", async () => {
    const workbook = await buildFinancialReportingWorkbook(projectInput());
    const text = sheetText(workbook, "summary");
    expect(text).toContain("generatedAt");
    const json = JSON.stringify(buildFinancialReportingPdfDefinition(projectInput()));
    expect(json).toContain("generatedAt");
  });
});