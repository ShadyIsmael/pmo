import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ComponentProps } from "react";
import { CashFlowCalendar } from "../features/projects/CashFlowCalendar";
import type { ProjectCashFlowDto } from "../features/projects/api/project-cash-flow";

const mocks = vi.hoisted(() => ({
  useProjectCashFlow: vi.fn(),
  locale: "en",
}));

vi.mock("../features/projects/api/project-cash-flow", async importOriginal => ({
  ...await importOriginal<typeof import("../features/projects/api/project-cash-flow")>(),
  useProjectCashFlow: mocks.useProjectCashFlow,
}));

vi.mock("@i18n/messages", () => ({
  useI18n: () => ({
    locale: mocks.locale,
    t: (key: string, params?: Record<string, string | number>) => {
      const labels: Record<string, string> = {
        "common.loading": "Loading",
        "common.retry": "Retry",
        "common.close": "Close",
        "projects.financialsPermissionRequired": "Financial view permission required",
        "projects.cashFlow.title": "Cash Flow Calendar",
        "projects.cashFlow.calendarDescription": "Planned, committed, and actual cash flows by date.",
        "projects.cashFlow.fiscalYear": "Fiscal year",
        "projects.cashFlow.allFiscalYears": "All fiscal years",
        "projects.cashFlow.fiscalYearUnavailable": "Fiscal year unavailable",
        "projects.cashFlow.currency": "Currency",
        "projects.cashFlow.allCurrencies": "All currencies",
        "projects.cashFlow.previousMonth": "Previous month",
        "projects.cashFlow.nextMonth": "Next month",
        "projects.cashFlow.category.plannedResidual": "Planned residual",
        "projects.cashFlow.category.committedUnpaid": "Committed unpaid",
        "projects.cashFlow.category.actualGross": "Actual gross",
        "projects.cashFlow.category.executedAdjustments": "Executed adjustments",
        "projects.cashFlow.category.actualNet": "Actual net",
        "projects.cashFlow.short.planned": "Planned",
        "projects.cashFlow.short.committed": "Committed",
        "projects.cashFlow.short.actual": "Actual",
        "projects.cashFlow.amountUnavailable": "Amount unavailable",
        "projects.cashFlow.currencyUnavailable": "Currency unavailable",
        "projects.cashFlow.unrecognizedValue": "Unrecognized value",
        "projects.cashFlow.overdue": "Overdue",
        "projects.cashFlow.amount": "Amount",
        "projects.cashFlow.expectedDate": "Expected date",
        "projects.cashFlow.installment": "Installment",
        "projects.cashFlow.paymentRequest": "Payment request",
        "projects.cashFlow.paymentObligation": "Payment obligation",
        "projects.cashFlow.timingVariance": "Timing variance",
        "projects.cashFlow.timingVarianceDays": `${params?.days ?? ""} days`,
        "projects.cashFlow.itemQuality": "Item data quality",
        "projects.cashFlow.openSourceRecord": "Open source record",
        "projects.cashFlow.loadFailed": "Cash flow could not be loaded.",
        "projects.cashFlow.dataUnavailable": "Cash flow data is unavailable.",
        "projects.cashFlow.dataQualityNotice": "Some cash flow data has limitations.",
        "projects.cashFlow.quality.partial-history": "Settlement history is incomplete.",
        "projects.cashFlow.empty": "No cash flow is available for this period.",
        "projects.cashFlow.noFlows": "No cash flow",
        "projects.cashFlow.monthCalendar": "Monthly cash flow calendar",
        "projects.cashFlow.dayDetails": "Cash flow details for the selected date.",
        "projects.cashFlow.noItemsForDay": "No cash flow items for this date.",
        "projects.cashFlow.undatedTitle": "Items without a date",
        "projects.cashFlow.undatedDescription": "These items are excluded from dated totals.",
        "projects.cashFlow.status.Confirmed": "Confirmed",
        "projects.cashFlow.status.Approved": "Approved",
        "projects.cashFlow.category.PlannedResidual": "Planned residual",
        "projects.cashFlow.category.CommittedUnpaid": "Committed unpaid",
        "projects.cashFlow.category.ActualGross": "Actual gross",
        "projects.cashFlow.category.ExecutedAdjustment": "Executed adjustment",
      };
      return labels[key] ?? key;
    },
    formatCurrency: (value: number, currency?: string) => `${currency} ${value.toFixed(2)}`,
    formatNumber: (value: number) => new Intl.NumberFormat(mocks.locale).format(value),
  }),
}));

function bucket(overrides: Partial<ProjectCashFlowDto["currencyGroups"][number]["buckets"][number]> = {}) {
  return {
    periodStart: "2026-11-05",
    plannedResidual: 100,
    committedUnpaid: 50,
    actualGross: 80,
    executedAdjustments: -10,
    actualNet: 70,
    items: [],
    ...overrides,
  };
}

function totals(items: ProjectCashFlowDto["currencyGroups"][number]["undated"]["items"] = []) {
  return { plannedResidual: null, committedUnpaid: null, actualGross: null, executedAdjustments: null, actualNet: null, items };
}

const paymentItem = {
  measure: "ActualGross" as const,
  sourceType: "PaymentSettlement",
  sourceId: "settlement-egp-1",
  installmentId: "installment-1",
  paymentRequestId: "request-1",
  description: "November instalment payment",
  status: "Confirmed",
  eventDate: "2026-11-05",
  expectedDate: "2026-11-02",
  timingVarianceDays: 3,
  amount: 80,
  currency: "EGP",
  qualityCodes: [],
};

const overdueRequest = {
  measure: "CommittedUnpaid" as const,
  sourceType: "ProjectPaymentRequest",
  sourceId: "request-overdue-1",
  paymentRequestId: "request-overdue-1",
  description: "Overdue supplier request",
  status: "Approved",
  eventDate: "2026-11-05",
  expectedDate: "2026-11-05",
  amount: 50,
  currency: "EGP",
  qualityCodes: [],
};

const response: ProjectCashFlowDto = {
  projectId: "project-1",
  periodFrom: "2026-11-01",
  periodTo: "2026-11-30",
  granularity: "Day",
  dataQualityStatus: "Partial",
  dataQualityIssues: [{ code: "partial-history", severity: "Warning", sourceId: "settlement-egp-1" }],
  currencyGroups: [
    {
      currency: "EGP",
      dataQualityStatus: "Partial",
      buckets: [bucket({ items: [paymentItem, overdueRequest] })],
      undated: totals([{
        measure: "PlannedResidual",
        sourceType: "PlannedPaymentInstallment",
        sourceId: "undated-installment",
        description: "Undated payment line",
        status: "Approved",
        amount: 25,
        currency: "EGP",
        qualityCodes: ["missing-date"],
      }]),
    },
    {
      currency: "USD",
      dataQualityStatus: "Available",
      buckets: [bucket({
        periodStart: "2026-11-05",
        plannedResidual: 200,
        committedUnpaid: 0,
        actualGross: 0,
        executedAdjustments: 0,
        actualNet: 0,
      })],
      undated: totals(),
    },
  ],
};

function renderCalendar(props: Partial<ComponentProps<typeof CashFlowCalendar>> = {}) {
  return render(<CashFlowCalendar projectId="project-1" {...props} />);
}

describe("Project Cash Flow Calendar", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 10, 15));
    mocks.locale = "en";
    mocks.useProjectCashFlow.mockReset().mockReturnValue({
      data: response,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
  });

  it("requests the visible month and presents each currency separately", () => {
    renderCalendar();

    expect(mocks.useProjectCashFlow).toHaveBeenCalledWith("project-1", {
      from: "2026-11-01",
      to: "2026-11-30",
      granularity: "Day",
    }, true);
    expect(screen.getAllByRole("heading", { name: "EGP" }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("heading", { name: "USD" }).length).toBeGreaterThan(0);
    expect(screen.getAllByText("EGP 100.00").length).toBeGreaterThan(0);
    expect(screen.getAllByText("USD 200.00").length).toBeGreaterThan(0);
    expect(screen.queryByText("EGP 300.00")).not.toBeInTheDocument();
  });

  it("filters displayed currency groups without combining their totals", () => {
    renderCalendar();
    fireEvent.change(screen.getByLabelText("Currency"), { target: { value: "EGP" } });

    expect(screen.getAllByRole("heading", { name: "EGP" }).length).toBeGreaterThan(0);
    expect(screen.queryByRole("heading", { name: "USD" })).not.toBeInTheDocument();
    expect(screen.getByText("EGP 100.00")).toBeInTheDocument();
    expect(screen.queryByText("USD 200.00")).not.toBeInTheDocument();
  });

  it("opens selected-day details, shows linked identifiers and calls the supplied source action", () => {
    const onOpenItem = vi.fn();
    renderCalendar({ onOpenItem });
    const day = screen.getByRole("button", { name: /November 5, 2026.*Planned residual: EGP 100.00/ });
    fireEvent.click(day);

    expect(screen.getByText("November instalment payment")).toBeInTheDocument();
    expect(screen.getByText("installment-1")).toBeInTheDocument();
    expect(screen.getByText("request-1")).toBeInTheDocument();
    expect(screen.getByText("3 days")).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "Open source record" })[0]);
    expect(onOpenItem).toHaveBeenCalledWith(paymentItem);
  });

  it("only opens source records for items linked to a payment request", () => {
    const onOpenItem = vi.fn();
    const singleItemResponse = (item: ProjectCashFlowDto["currencyGroups"][number]["buckets"][number]["items"][number]) => ({
      ...response,
      currencyGroups: [{
        ...response.currencyGroups[0],
        buckets: [bucket({ items: [item] })],
        undated: totals(),
      }],
    });

    mocks.useProjectCashFlow.mockReturnValue({
      data: singleItemResponse(paymentItem),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    const linked = renderCalendar({ onOpenItem });
    fireEvent.click(screen.getByRole("button", { name: /November 5, 2026/ }));
    fireEvent.click(screen.getByRole("button", { name: "Open source record" }));
    expect(onOpenItem).toHaveBeenCalledWith(paymentItem);
    linked.unmount();

    const { paymentRequestId: linkedRequestId, ...unlinkedItem } = paymentItem;
    expect(linkedRequestId).toBe("request-1");
    mocks.useProjectCashFlow.mockReturnValue({
      data: singleItemResponse(unlinkedItem),
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    renderCalendar({ onOpenItem });
    fireEvent.click(screen.getByRole("button", { name: /November 5, 2026/ }));
    expect(screen.queryByRole("button", { name: "Open source record" })).not.toBeInTheDocument();
  });

  it("keeps undated items out of date buckets and surfaces data-quality reasons", () => {
    renderCalendar();

    expect(screen.getByText("Settlement history is incomplete.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Items without a date" })).toBeInTheDocument();
    expect(screen.getByText("Undated payment line")).toBeInTheDocument();
    expect(screen.getAllByText("Overdue").length).toBeGreaterThan(0);
  });

  it("uses the configured fiscal-year range and opens with RTL in Arabic", () => {
    mocks.locale = "ar";
    const { container } = renderCalendar({
      fiscalYearOptions: [{ value: "FY-2026", label: "FY 2026/27", from: "2026-07-01", to: "2027-06-30" }],
    });
    fireEvent.change(screen.getByLabelText("Fiscal year"), { target: { value: "FY-2026" } });

    expect(mocks.useProjectCashFlow).toHaveBeenLastCalledWith("project-1", {
      from: "2026-11-01",
      to: "2026-11-30",
      granularity: "Day",
    }, true);
    expect(container.querySelector("section[dir='rtl']")).toBeInTheDocument();
  });

  it("shows loading and error states distinctly", () => {
    mocks.useProjectCashFlow.mockReturnValueOnce({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    const loading = renderCalendar();
    expect(screen.getByRole("status")).toHaveTextContent("Loading");
    loading.unmount();

    const refetch = vi.fn();
    mocks.useProjectCashFlow.mockReturnValueOnce({ data: undefined, isLoading: false, isError: true, refetch });
    renderCalendar();
    expect(screen.getByRole("alert")).toHaveTextContent("Cash flow could not be loaded.");
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(refetch).toHaveBeenCalledOnce();
  });

  it("shows empty and unavailable states distinctly", () => {
    mocks.useProjectCashFlow.mockReturnValueOnce({
      data: { ...response, dataQualityStatus: "Available", dataQualityIssues: [], currencyGroups: [] },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    const empty = renderCalendar();
    expect(screen.getByRole("status")).toHaveTextContent("No cash flow is available for this period.");
    empty.unmount();

    mocks.useProjectCashFlow.mockReturnValueOnce({
      data: { ...response, dataQualityStatus: "Unavailable", currencyGroups: [] },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    renderCalendar();
    expect(screen.getByRole("status")).toHaveTextContent("Cash flow data is unavailable.");
  });

  it("does not query or display financial values when access is disabled", () => {
    renderCalendar({ enabled: false });

    expect(mocks.useProjectCashFlow).toHaveBeenLastCalledWith("project-1", expect.any(Object), false);
    expect(screen.getByRole("status")).toHaveTextContent("Financial view permission required");
    expect(screen.queryByText("EGP 100.00")).not.toBeInTheDocument();
  });
});
