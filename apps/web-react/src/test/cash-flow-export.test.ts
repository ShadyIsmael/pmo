import { describe, expect, it } from "vitest";
import { buildCashFlowPdfDefinition, buildCashFlowWorkbook, type CashFlowExportInput } from "../features/finance/cash-flow-export";

const translate = (key: string) => key;

const projectInput: CashFlowExportInput = {
  level: "Project",
  projectId: "project-1",
  entityName: "Project One",
  locale: "en",
  generatedAt: "2026-11-23T12:00:00Z",
  translate,
  report: {
    projectId: "project-1",
    periodFrom: "2026-01-01",
    periodTo: "2026-03-31",
    granularity: "Month",
    dataQualityStatus: "Partial",
    dataQualityIssues: [{
      code: "UNKNOWN_EXPECTED_DATE",
      severity: "Partial",
      sourceType: "PlannedInstallment",
      sourceId: "installment-1",
      installmentId: "installment-1",
      paymentRequestId: null,
      paymentObligationId: null,
      currency: "USD",
    }],
    currencyGroups: [{
      currency: "USD",
      dataQualityStatus: "Partial",
      buckets: [{
        periodStart: "2026-02-01",
        plannedResidual: 125,
        committedUnpaid: 35,
        actualGross: 90,
        executedAdjustments: -5,
        actualNet: 85,
        items: [{
          measure: "ActualGross",
          sourceType: "PaymentSettlement",
          sourceId: "settlement-1",
          installmentId: "installment-1",
          paymentRequestId: "request-1",
          paymentObligationId: null,
          originalSettlementId: null,
          description: "Supplier payment",
          status: "Settled",
          eventDate: "2026-02-10",
          expectedDate: "2026-02-05",
          timingVarianceDays: 5,
          amount: 90,
          currency: "USD",
          qualityCodes: ["TIMING_DELAY"],
        }],
      }],
      undated: {
        plannedResidual: 10,
        committedUnpaid: 0,
        actualGross: 0,
        executedAdjustments: 0,
        actualNet: 0,
        items: [{
          measure: "PlannedResidual",
          sourceType: "PlannedInstallment",
          sourceId: "installment-undated",
          installmentId: "installment-undated",
          paymentRequestId: null,
          paymentObligationId: null,
          originalSettlementId: null,
          description: "Date missing",
          status: "Scheduled",
          eventDate: null,
          expectedDate: null,
          timingVarianceDays: null,
          amount: 10,
          currency: "USD",
          qualityCodes: ["UNKNOWN_EXPECTED_DATE"],
        }],
      },
    }],
  },
};

describe("Cash Flow exports", () => {
  it("builds Project PDF data with range, separate lifecycle measures, timing variance and quality notices", () => {
    const definition = buildCashFlowPdfDefinition(projectInput);
    const serialized = JSON.stringify(definition);
    expect(serialized).toContain("Jan 1, 2026");
    expect(serialized).toContain("Mar 31, 2026");
    expect(serialized).toContain("cashFlow.plannedResidual");
    expect(serialized).toContain("cashFlow.committedUnpaid");
    expect(serialized).toContain("cashFlow.actualGross");
    expect(serialized).toContain("cashFlow.executedAdjustments");
    expect(serialized).toContain("cashFlow.actualNet");
    expect(serialized).toContain("5");
    expect(serialized).toContain("UNKNOWN_EXPECTED_DATE");
    expect(serialized).toContain("TIMING_DELAY");
    expect(serialized).toContain("cashFlow.undated");
  });

  it("builds a Project workbook with timeline, source detail and data quality sheets", async () => {
    const workbook = await buildCashFlowWorkbook(projectInput);
    expect(workbook.worksheets.map(sheet => sheet.name)).toEqual(expect.arrayContaining([
      "cashFlowExport.summary",
      "cashFlowExport.timeline",
      "cashFlowExport.projectBreakdown",
      "cashFlowExport.sourceDetails",
      "cashFlowExport.dataQuality",
    ]));
    const rows = workbook.worksheets.flatMap(sheet => sheet.getSheetValues().flat().filter(value => value != null).map(String));
    expect(rows).toContain("USD");
    expect(rows).toContain("UNKNOWN_EXPECTED_DATE");
    expect(rows).toContain("TIMING_DELAY");
    expect(rows).toContain("5");
    expect(rows.some(value => value.includes("Jan 1, 2026"))).toBe(true);
    expect(rows.some(value => value.includes("Mar 31, 2026"))).toBe(true);
  });
});
