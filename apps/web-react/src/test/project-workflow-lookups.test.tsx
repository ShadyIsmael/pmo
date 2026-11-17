import type * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useMeetingProjectOptions } from "@api/meetingsFull";
import { usePaymentProjectLookup } from "@features/projects/api/projects";
import { useEditableProgramPortfolios } from "@features/delivery/api/delivery";
import { useBaseCalendars, useResourceReferenceData, useResourceVendorLookup } from "@features/resources/api/resources";
import {
  useDepartmentLookup,
  useMeetingWorkflowUserLookup,
  usePortfolioLookup,
  usePortfolioWorkflowManagerLookup,
  useProjectCurrencyLookup,
  useProjectWorkflowUserLookup,
  useProgramLookup,
  useProgramWorkflowManagerLookup,
  useResourceUnitLookup,
  useResourceWorkflowUserLookup,
  useRiskWorkflowUserLookup,
  useWorkItemWorkflowUserLookup,
  useWorkflowCurrencyLookup,
} from "../features/projects/lib/project-lookups";

vi.mock("@api/client", () => ({
  api: {
    get: vi.fn(),
  },
}));

function createWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("Project workflow user lookups", () => {
  afterEach(() => {
    vi.mocked(api.get).mockReset();
  });

  it("requests the minimal workflow projection on first load with an empty query cache", async () => {
    const user = { id: "user-1", fullName: "Project Manager" };
    vi.mocked(api.get).mockResolvedValueOnce([user]);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useProjectWorkflowUserLookup(true), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.data).toEqual([user]));
    expect(api.get).toHaveBeenCalledWith(endpoints.users.projectWorkflowLookup);
  });

  it("requests Payment project references from the dedicated endpoint on first load", async () => {
    const project = { id: "project-1", publicId: "project-public-1", name: "Project", code: "PRJ-1", currency: "EUR" };
    vi.mocked(api.get).mockResolvedValueOnce([project]);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => usePaymentProjectLookup(true), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.data).toEqual([project]));
    expect(api.get).toHaveBeenCalledWith(endpoints.projects.paymentObligationLookup);
  });

  it("requests Program edit Portfolio references from the operation-scoped endpoint on first load", async () => {
    const portfolio = { id: "portfolio-1", name: "Technology", code: "PORT-01", currency: "EUR", status: "Active" };
    vi.mocked(api.get).mockResolvedValueOnce({ items: [portfolio] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useEditableProgramPortfolios(true), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.data).toEqual({ items: [portfolio] }));
    expect(api.get).toHaveBeenCalledWith(endpoints.programs.editablePortfolios);
  });

  it("requests Resource form references through workflow lookup endpoints on first load", async () => {
    const calendars = [{ id: "calendar-1", name: "Standard", isDefault: true, isConfigured: true, workingDaysPerWeek: 5 }];
    const referenceData = { resourceTypes: [], seniorityLevels: [], compensationTypes: [] };
    const vendors = [{ id: "vendor-1", name: "Vendor One", code: "VND-01", isActive: true }];
    vi.mocked(api.get).mockImplementation(async (path) => {
      if (path === endpoints.resources.baseCalendars) return calendars;
      if (path === endpoints.resources.referenceData) return referenceData;
      if (path === endpoints.vendors.resourceLookup) return vendors;
      return [];
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => ({
      calendars: useBaseCalendars(true),
      referenceData: useResourceReferenceData(true),
      vendors: useResourceVendorLookup(true, "resource-1"),
    }), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.calendars.data).toEqual(calendars));
    await waitFor(() => expect(result.current.referenceData.data).toEqual(referenceData));
    await waitFor(() => expect(result.current.vendors.data).toEqual({ items: vendors }));
    expect(api.get).toHaveBeenCalledWith(endpoints.resources.baseCalendars);
    expect(api.get).toHaveBeenCalledWith(endpoints.resources.referenceData);
    expect(api.get).toHaveBeenCalledWith(endpoints.vendors.resourceLookup, { resourceId: "resource-1" });
  });

  it("loads every Project dropdown from its purpose-scoped lookup endpoint on first render", async () => {
    vi.mocked(api.get).mockImplementation(async (path) => {
      if (path === endpoints.portfolios.projectLookup || path === endpoints.programs.projectLookup) return { items: [] };
      return [];
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => ({
      portfolios: usePortfolioLookup(true),
      programs: useProgramLookup(true),
      departments: useDepartmentLookup(true),
      currencies: useProjectCurrencyLookup(true),
    }), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.currencies.data).toEqual([]));
    await waitFor(() => expect(result.current.departments.data).toEqual([]));
    await waitFor(() => expect(result.current.portfolios.data).toEqual({ items: [] }));
    await waitFor(() => expect(result.current.programs.data).toEqual({ items: [] }));
    expect(api.get).toHaveBeenCalledWith(endpoints.portfolios.projectLookup, { pageSize: 200 });
    expect(api.get).toHaveBeenCalledWith(endpoints.programs.projectLookup, { pageSize: 200 });
    expect(api.get).toHaveBeenCalledWith(endpoints.organization.projectUnitLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.currencies.projectLookup);
  });

  it("loads other workflow dropdowns from their minimal-purpose endpoints on first render", async () => {
    vi.mocked(api.get).mockResolvedValue([]);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => ({
      meetings: useMeetingWorkflowUserLookup(true),
      meetingProjects: useMeetingProjectOptions({}, true),
      workItems: useWorkItemWorkflowUserLookup(true),
      risks: useRiskWorkflowUserLookup(true, "project-1"),
      resources: useResourceWorkflowUserLookup(true),
      portfolioManagers: usePortfolioWorkflowManagerLookup(true),
      programManagers: useProgramWorkflowManagerLookup(true),
      currencies: useWorkflowCurrencyLookup(true),
      units: useResourceUnitLookup(true),
    }), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.meetings.data).toEqual([]));
    await waitFor(() => expect(result.current.meetingProjects.data).toEqual([]));
    await waitFor(() => expect(result.current.units.data).toEqual([]));
    expect(api.get).toHaveBeenCalledWith(endpoints.users.meetingWorkflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.meetings.projects, {});
    expect(api.get).toHaveBeenCalledWith(endpoints.users.workItemWorkflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.users.riskWorkflowLookup, { projectId: "project-1" });
    expect(api.get).toHaveBeenCalledWith(endpoints.users.resourceWorkflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.users.portfolioManagerWorkflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.users.programManagerWorkflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.currencies.workflowLookup);
    expect(api.get).toHaveBeenCalledWith(endpoints.organization.resourceUnitLookup);
  });

  it("requests Resource edit candidates with the current Resource id", async () => {
    const users = [{ id: "user-1", fullName: "Alex Admin", email: "alex.admin@example.com" }];
    vi.mocked(api.get).mockResolvedValueOnce(users);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useResourceWorkflowUserLookup(true, "resource-1"), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.data).toEqual(users));
    expect(api.get).toHaveBeenCalledWith(endpoints.users.resourceWorkflowLookup, { resourceId: "resource-1" });
  });

  it("surfaces API authorization failures instead of replacing them with empty data", async () => {
    vi.mocked(api.get).mockRejectedValueOnce(new Error("Forbidden"));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useProjectWorkflowUserLookup(true), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.data).toBeUndefined();
    expect(result.current.error).toEqual(new Error("Forbidden"));
  });
});
