import * as React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectSheet } from "../features/projects/components/CreateProjectSheet";
import type { ProjectDto } from "../features/projects/api/projects";

const mocks = vi.hoisted(() => ({
  permissions: [
    "Project.View",
    "Project.Edit",
    "ProjectStatus.Change",
    "ProjectSettings.View",
    "ProjectSettings.Edit",
  ] as string[],
  canMutate: true,
  effectiveReadOnly: false,
  updateProject: vi.fn(),
  lifecycleData: undefined as unknown,
  lifecycleLoading: false,
  lifecycleError: false,
  lifecycleCalls: [] as Array<Record<string, unknown>>,
}));

vi.mock("../features/projects/api/projects", () => ({
  useCreateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateProject: () => ({ mutateAsync: mocks.updateProject, isPending: false }),
}));

vi.mock("../features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: (options: Record<string, unknown>) => {
    mocks.lifecycleCalls.push(options);
    return {
      data: mocks.lifecycleData,
      isLoading: mocks.lifecycleLoading,
      isError: mocks.lifecycleError,
      refetch: vi.fn(),
    };
  },
  projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string; code?: string } | undefined, locale: string) =>
    option ? (locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code) : "",
}));

vi.mock("../features/projects/lib/project-lookups", () => ({
  toUserSelectOptions: (items: Array<{ id: string; fullName?: string; name?: string }>) => items.map(item => ({ value: item.id, label: item.fullName ?? item.name ?? item.id })),
  useDepartmentLookup: () => ({ data: [], error: null }),
  usePortfolioLookup: () => ({ data: { items: [] }, error: null }),
  useProjectCurrencyLookup: () => ({ data: [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }], error: null }),
  useProjectWorkflowUserLookup: () => ({ data: [], error: null }),
  useProjectManagerCandidateLookup: () => ({ data: [], error: null, refetch: vi.fn() }),
  useProgramLookup: () => ({ data: { items: [] }, error: null }),
}));

vi.mock("../features/projects/lib/project-labels", () => ({
  useProjectEnumLabels: () => ({
    statusLabel: (value: string) => value,
    priorityLabel: (value: string) => value,
    healthLabel: (value: string) => value,
  }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "actor-1", permissions: mocks.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }),
}));

vi.mock("@components/ui/Select", () => ({
  Select: ({ value, onValueChange, children }: { value?: string; onValueChange: (value: string) => void; children: React.ReactNode }) => (
    <select
      aria-label={String(value ?? "").startsWith("status-") ? "Project status" : undefined}
      value={value ?? ""}
      onChange={(event) => onValueChange(event.target.value)}
    >
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectValue: () => null,
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children: React.ReactNode }) => <option value={value}>{children}</option>,
}));

vi.mock("@components/ui/SearchableSelect", () => ({
  SearchableSelect: () => null,
}));

function project(): ProjectDto {
  return {
    id: "project-id",
    publicId: "project-public-id",
    tenantId: "tenant-id",
    rowVersion: "row-version",
    name: "Delivery project",
    code: "PRJ-1",
    description: "",
    statusId: "status-active",
    status: "Active",
    statusNameEn: "Active",
    statusNameAr: "نشط",
    priority: "Medium",
    phaseId: "phase-delivery",
    phase: "delivery",
    phaseNameEn: "Delivery",
    phaseNameAr: "التسليم",
    currency: "USD",
    progress: 0,
    health: "Unknown",
    ragStatus: "Unknown",
    confidenceLevel: "Green",
    createdAt: "2026-01-01T00:00:00Z",
    createdBy: "creator",
    isDeleted: false,
    members: [],
  } as ProjectDto;
}

function statuses() {
  return [
    { id: "status-active", code: "active", nameEn: "Active", nameAr: "نشط", displayOrder: 20, isDefault: false, isActive: true },
    { id: "status-custom", code: "bespoke", nameEn: "Bespoke Delivery", nameAr: "تسليم مخصص", displayOrder: 10, isDefault: false, isActive: true },
    { id: "status-other", code: "other", nameEn: "Other Active", nameAr: "أخرى", displayOrder: 30, isDefault: false, isActive: true },
    { id: "status-inactive", code: "retired", nameEn: "Retired Status", nameAr: "حالة متقاعدة", displayOrder: 40, isDefault: false, isActive: false },
  ];
}

function renderSheet() {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider><ProjectSheet open onOpenChange={vi.fn()} project={project()} embedded /></I18nProvider>);
}

describe("Edit Project change status", () => {
  beforeEach(() => {
    mocks.permissions = ["Project.View", "Project.Edit", "ProjectStatus.Change", "ProjectSettings.View", "ProjectSettings.Edit"];
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.lifecycleLoading = false;
    mocks.lifecycleError = false;
    mocks.lifecycleData = { phases: [], statuses: statuses(), statusTransitions: null };
    mocks.lifecycleCalls = [];
    mocks.updateProject.mockReset().mockResolvedValue(project());
  });

  it("loads statuses from the project-scoped Reference Data lookup and preselects the current status", async () => {
    renderSheet();
    const statusSelect = await screen.findByLabelText("Project status") as HTMLSelectElement;

    expect(mocks.lifecycleCalls.length).toBeGreaterThan(0);
    expect(mocks.lifecycleCalls.every((call) => call.projectId === "project-public-id" && call.enabled === true)).toBe(true);
    expect(statusSelect.value).toBe("status-active");
    expect(screen.getByRole("option", { name: "Bespoke Delivery" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Retired Status" })).not.toBeInTheDocument();
  });

  it("saves the canonical status id without writing legacy status text or changing the phase", async () => {
    renderSheet();
    const statusSelect = await screen.findByLabelText("Project status") as HTMLSelectElement;
    fireEvent.change(statusSelect, { target: { value: "status-custom" } });
    fireEvent.click(screen.getByRole("button", { name: "Update Project" }));

    await waitFor(() => expect(mocks.updateProject).toHaveBeenCalledTimes(1));
    const payload = mocks.updateProject.mock.calls[0][0];
    expect(payload).toEqual(expect.objectContaining({ statusId: "status-custom" }));
    expect(payload).not.toHaveProperty("status");
    expect(payload).not.toHaveProperty("phase");
    expect(payload).not.toHaveProperty("phaseId");
  });

  it("limits options to configured transitions and requires a reason when configured", async () => {
    mocks.lifecycleData = {
      phases: [],
      statuses: statuses(),
      statusTransitions: [{ targetStatusId: "status-custom", requiresReason: true }],
    };
    renderSheet();
    const statusSelect = await screen.findByLabelText("Project status") as HTMLSelectElement;
    expect(screen.queryByRole("option", { name: "Other Active" })).not.toBeInTheDocument();

    fireEvent.change(statusSelect, { target: { value: "status-custom" } });
    fireEvent.click(screen.getByRole("button", { name: "Update Project" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("A reason is required for this status transition.");
    expect(mocks.updateProject).not.toHaveBeenCalled();
  });

  it("does not expose an editable status to a user without ProjectStatus.Change", async () => {
    mocks.permissions = ["Project.View", "Project.Edit"];
    renderSheet();

    await waitFor(() => expect(screen.queryByRole("combobox", { name: "Project status" })).not.toBeInTheDocument());
    expect(screen.getByLabelText("Project status")).toHaveAttribute("readonly");
    expect(screen.getByLabelText("Project status")).toHaveValue("Active");
  });
});
