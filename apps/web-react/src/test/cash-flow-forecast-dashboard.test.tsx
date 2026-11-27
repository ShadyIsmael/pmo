import { fireEvent, render, screen } from "@testing-library/react";
import type { ComponentProps } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CashFlowForecastDashboard } from "../features/projects/CashFlowForecastDashboard";
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
        "projects.financialsPermissionRequired": "Financial view permission required",
        "projects.cashFlow.title": "Cash Flow Calendar",
        "projects.cashFlow.fiscalYear": "Fiscal year",
        "projects.cashFlow.fiscalYearUnavailable": "Fiscal year unavailable",
        "projects.cashFlow.currency": "Currency",
        "projects.cashFlow.allCurrencies": "All currencies",
        "projects.cashFlow.amountUnavailable": "Amount unavailable",
        "projects.cashFlow.currencyUnavailable": "Currency unavailable",
        "projects.cashFlow.openSourceRecord": "Open source record",
        "projects.cashFlow.expectedDate": "Expected date",
        "projects.cashFlow.dataQualityNotice": "Some cash flow data has limitations.",
        "projects.cashFlow.category.plannedResidual": "Planned residual",
        "projects.cashFlow.category.committedUnpaid": "Committed unpaid",
        "projects.cashFlow.category.actualGross": "Actual gross",
        "projects.cashFlow.category.executedAdjustments": "Executed adjustments",
        "projects.cashFlow.category.actualNet": "Actual net",
        "projects.cashFlow.quality.partial-history": "Settlement history is incomplete.",
        "projects.cashFlowForecast.title": "Cash Flow Forecast",
        "projects.cashFlowForecast.description": "Monthly cash requirements and actual payments.",
        "projects.cashFlowForecast.unrecognizedValue": "Unrecognized value",
        "projects.cashFlowForecast.status.Available": "Available",
        "projects.cashFlowForecast.status.Partial": "Partial",
        "projects.cashFlowForecast.expectedOutflowChart": "Monthly expected outflow and actual payments",
        "projects.cashFlowForecast.separateSeriesNotice": "Each lifecycle measure is shown separately.",
        "projects.cashFlowForecast.noMonthlyData": "No monthly cash flow is available.",
        "projects.cashFlowForecast.futureRequirements": "Future cash requirements",
        "projects.cashFlowForecast.futureRequirementsCurrentState": "Current scheduled amounts, by category.",
        "projects.cashFlowForecast.monthlyComparison": "Monthly forecast and actual comparison",
        "projects.cashFlowForecast.currentStateComparisonNotice": "Past planned values reflect current source state, not a saved forecast for that month.",
        "projects.cashFlowForecast.period": "Period",
        "projects.cashFlowForecast.overduePayments": "Overdue unpaid items",
        "projects.cashFlowForecast.overdueUnpaidNotice": "Expected dates are compared with today.",
        "projects.cashFlowForecast.noOverduePayments": "No overdue unpaid items.",
        "projects.cashFlowForecast.paymentTiming": "Payment timing variance",
        "projects.cashFlowForecast.timingIsNotAmountVariance": "Timing differences are measured in days, not money.",
        "projects.cashFlowForecast.noTimingHistory": "No settlement timing history is available.",
        "projects.cashFlowForecast.timingVarianceDays": `${params?.days ?? ""} days`,
        "projects.cashFlowForecast.late": "Late",
        "projects.cashFlowForecast.early": "Early",
        "projects.cashFlowForecast.onTime": "On time",
        "projects.cashFlowForecast.actualDate": "Actual date",
        "projects.cashFlowForecast.historicalSnapshotUnavailable": "Historical forecast snapshots are unavailable.",
        "projects.cashFlowForecast.currentStateNotice": "Past comparisons show current source-state values and actual payments, not historical forecast accuracy.",
        "projects.cashFlowForecast.loadFailed": "Forecast data could not be loaded.",
        "projects.cashFlowForecast.dataUnavailable": "Forecast data is unavailable.",
      };
      return labels[key] ?? key;
    },
    formatCurrency: (value: number, currency?: string) => `${currency} ${value.toFixed(2)}`,
    formatNumber: (value: number, options?: Intl.NumberFormatOptions) => new Intl.NumberFormat(mocks.locale, options).format(value),
  }),
}));

function bucket(
  periodStart: string,
  overrides: Partial<ProjectCashFlowDto["currencyGroups"][number]["buckets"][number]> = {},
) {
  return {
    periodStart,
    plannedResidual: 0,
    committedUnpaid: 0,
    actualGross: 0,
    executedAdjustments: 0,
    actualNet: 0,
    items: [],
    ...overrides,
  };
}

const response: ProjectCashFlowDto = {
  projectId: "project-1",
  periodFrom: "2026-07-01",
  periodTo: "2027-06-30",
  granularity: "Month",
  dataQualityStatus: "Partial",
  dataQualityIssues: [{ code: "partial-history", severity: "Warning" }],
  currencyGroups: [
    {
      currency: "EGP",
      dataQualityStatus: "Partial",
      buckets: [
        bucket("2026-11-01", {
          plannedResidual: 100,
          committedUnpaid: 50,
          actualGross: 80,
          executedAdjustments: -10,
          actualNet: 70,
          items: [
            {
              measure: "CommittedUnpaid",
              sourceType: "ProjectPaymentRequest",
              sourceId: "overdue-request-1",
              paymentRequestId: "overdue-request-1",
              description: "Overdue supplier request",
              status: "Approved",
              eventDate: "2026-11-02",
              expectedDate: "2026-11-02",
              amount: 50,
              currency: "EGP",
              qualityCodes: [],
            },
            {
              measure: "ActualGross",
              sourceType: "PaymentSettlement",
              sourceId: "settlement-1",
              paymentRequestId: "request-1",
              description: "Supplier settlement",
              status: "Confirmed",
              eventDate: "2026-11-05",
              expectedDate: "2026-11-02",
              timingVarianceDays: 3,
              amount: 80,
              currency: "EGP",
              qualityCodes: [],
            },
          ],
        }),
        bucket("2026-12-01", { plannedResidual: 200, committedUnpaid: 100 }),
      ],
      undated: { plannedResidual: null, committedUnpaid: null, actualGross: null, executedAdjustments: null, actualNet: null, items: [] },
    },
    {
      currency: "USD",
      dataQualityStatus: "Available",
      buckets: [bucket("2026-11-01", { plannedResidual: 300 })],
      undated: { plannedResidual: null, committedUnpaid: null, actualGross: null, executedAdjustments: null, actualNet: null, items: [] },
    },
  ],
};

const fiscalYearOptions = [{ value: "FY-2026", label: "FY 2026/27", from: "2026-07-01", to: "2027-06-30" }];

function renderDashboard(props: Partial<ComponentProps<typeof CashFlowForecastDashboard>> = {}) {
  return render(<CashFlowForecastDashboard projectId="project-1" fiscalYearOptions={fiscalYearOptions} {...props} />);
}

describe("Cash Flow Forecast Dashboard", () => {
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

  it("requests the configured fiscal year as monthly buckets and keeps currencies separate", () => {
    renderDashboard();

    expect(mocks.useProjectCashFlow).toHaveBeenLastCalledWith("project-1", {
      from: "2026-07-01",
      to: "2027-06-30",
      granularity: "Month",
    }, true);
    expect(screen.getByRole("heading", { name: "EGP" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "USD" })).toBeInTheDocument();
    expect(screen.getAllByText("EGP 200.00").length).toBeGreaterThan(0);
    expect(screen.getByText("USD 300.00")).toBeInTheDocument();
    expect(screen.queryByText("EGP 600.00")).not.toBeInTheDocument();
  });

  it("compares distinct measures without inventing historical forecast accuracy", () => {
    renderDashboard();

    expect(screen.getByText("Historical forecast snapshots are unavailable.")).toBeInTheDocument();
    expect(screen.getByText(/Past comparisons show current source-state values/)).toBeInTheDocument();
    expect(screen.getAllByText("Planned residual").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Committed unpaid").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Actual gross").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Actual net").length).toBeGreaterThan(0);
    expect(screen.queryByText(/variance: EGP/)).not.toBeInTheDocument();
  });

  it("shows overdue items and day-based timing variance separately", () => {
    renderDashboard();

    expect(screen.getByText("Overdue supplier request")).toBeInTheDocument();
    expect(screen.getAllByText("Supplier settlement").length).toBeGreaterThan(0);
    expect(screen.getByText(/\+3 days/)).toBeInTheDocument();
    expect(screen.getByText("Late")).toBeInTheDocument();
    expect(screen.getAllByText("Timing differences are measured in days, not money.").length).toBeGreaterThan(0);
  });

  it("filters currencies and retains RTL layout in Arabic", () => {
    mocks.locale = "ar";
    const { container } = renderDashboard();
    fireEvent.change(screen.getByLabelText("Currency"), { target: { value: "USD" } });

    expect(screen.getByRole("heading", { name: "USD" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "EGP" })).not.toBeInTheDocument();
    expect(container.querySelector("section[dir='rtl']")).toBeInTheDocument();
  });

  it("shows access, loading, error, and unavailable states", () => {
    const disabled = renderDashboard({ enabled: false });
    expect(screen.getByText("Financial view permission required")).toBeInTheDocument();
    disabled.unmount();

    mocks.useProjectCashFlow.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    const loading = renderDashboard();
    expect(screen.getAllByText("Loading").length).toBeGreaterThan(0);
    loading.unmount();

    mocks.useProjectCashFlow.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: vi.fn() });
    const error = renderDashboard();
    expect(screen.getByRole("alert")).toHaveTextContent("Forecast data could not be loaded.");
    error.unmount();

    mocks.useProjectCashFlow.mockReturnValue({
      data: { ...response, dataQualityStatus: "Unavailable", currencyGroups: [] },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    renderDashboard();
    expect(screen.getByText("Forecast data is unavailable.")).toBeInTheDocument();
  });
});
