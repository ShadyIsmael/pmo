import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { WorkflowDefinitionPageFull, WorkflowsPageFull } from "../features/workflow/WorkflowPagesFull";

const fixtures = vi.hoisted(() => ({
  list: vi.fn(),
  definition: vi.fn(),
  create: vi.fn(),
  createDraft: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: ["Workflow.Manage"] } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ effectiveReadOnly: false, canMutate: true }) }));
vi.mock("../features/workflow/api/workflowsFull", () => ({
  useFullWorkflowDefinition: fixtures.definition,
  useFullWorkflowDefinitions: fixtures.list,
  useFullWorkflowMetadata: vi.fn(),
  useFullWorkflowMutations: () => ({ create: { mutateAsync: fixtures.create, isPending: false }, createDraft: { mutateAsync: fixtures.createDraft, isPending: false } }),
  useFullWorkflowPreview: vi.fn(),
  useFullWorkflowVersion: vi.fn(),
}));
vi.mock("../features/workflow/WorkflowFlowDesignerFull", () => ({ WorkflowFlowDesignerFull: () => null }));

const definitions = [
  { id: "workflow-active", name: "Delivery", code: "DELIVERY", domain: "Task", entityType: "WorkItem", isActive: true, activePublishedVersionNumber: 3, versionCount: 3, updatedAt: "2026-08-19T12:00:00.000Z" },
  { id: "workflow-inactive", name: "Legacy Risk", code: "LEGACY-RISK", domain: "Risk", entityType: "Risk", isActive: false, versionCount: 1, updatedAt: "2026-08-10T12:00:00.000Z" },
];

const definition = {
  id: "workflow-detail",
  name: "Delivery",
  code: "DELIVERY",
  description: "Golden-master workflow description",
  domain: "Task",
  entityType: "WorkItem",
  isActive: false,
  versions: [{ id: "version-2", versionNumber: 2, lifecycleStatus: "Published", createdAt: "2026-08-01T12:00:00.000Z", publishedAt: "2026-08-03T12:00:00.000Z", stateCount: 4, transitionCount: 3 }],
};

function renderPage(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<MemoryRouter initialEntries={["/workflows"]}><I18nProvider><WorkflowsPageFull /></I18nProvider></MemoryRouter>);
}

function renderDefinition(activePublishedVersionId?: string, locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  fixtures.definition.mockReturnValue({ data: { ...definition, activePublishedVersionId }, isLoading: false, isError: false });
  return render(<MemoryRouter initialEntries={["/workflows/workflow-detail"]}><I18nProvider><CurrentPath /><Routes><Route path="/workflows/:id" element={<WorkflowDefinitionPageFull />} /><Route path="/workflows/:id/versions/:versionId" element={<div>Draft version route</div>} /></Routes></I18nProvider></MemoryRouter>);
}

function CurrentPath() {
  const location = useLocation();
  return <output data-testid="current-path">{location.pathname}</output>;
}

describe("workflow list parity", () => {
  beforeEach(() => {
    fixtures.list.mockReset().mockReturnValue({ data: definitions, isLoading: false, isError: false });
    fixtures.definition.mockReset();
    fixtures.create.mockReset();
    fixtures.createDraft.mockReset();
  });

  it("filters by active state through the API and displays the last-updated column", () => {
    renderPage();

    expect(screen.getByRole("columnheader", { name: "Updated" })).toBeInTheDocument();
    const expectedDate = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", numberingSystem: "latn" }).format(new Date(definitions[0].updatedAt));
    expect(screen.getByText(expectedDate)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Inactive" }));
    expect(fixtures.list).toHaveBeenLastCalledWith(expect.objectContaining({ isActive: false }));

    fireEvent.click(screen.getByRole("button", { name: "Active" }));
    expect(fixtures.list).toHaveBeenLastCalledWith(expect.objectContaining({ isActive: true }));

    fireEvent.click(screen.getAllByRole("button", { name: "All" })[0]);
    expect(fixtures.list).toHaveBeenLastCalledWith(expect.objectContaining({ isActive: undefined }));
  });

  it("localizes the filter controls and last-updated heading in Arabic while keeping Western digits", () => {
    renderPage("ar");

    expect(screen.getByRole("columnheader", { name: "آخر تحديث" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "نشط" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "غير نشط" })).toBeInTheDocument();
    expect(screen.getAllByText(/2026/)).toHaveLength(2);
  });

  it.each(["Risk", "Finance"] as const)("lets the API derive the canonical entity type for the %s domain", async (domain) => {
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "New workflow" }));
    fireEvent.change(screen.getByRole("combobox"), { target: { value: domain } });
    fireEvent.change(screen.getAllByRole("textbox")[1], { target: { value: "Domain workflow" } });
    fireEvent.change(screen.getAllByRole("textbox")[2], { target: { value: "DOMAIN_WORKFLOW" } });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith(expect.objectContaining({
      domain,
      entityType: undefined,
    })));
  });

  it("matches source workflow details and only offers draft creation from a published workflow", () => {
    renderDefinition();

    expect(screen.getByText("Golden-master workflow description")).toBeInTheDocument();
    expect(screen.getByText("Inactive")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Updated" })).toBeInTheDocument();
    expect(screen.getByText(new Intl.DateTimeFormat("en-US", { dateStyle: "medium", numberingSystem: "latn" }).format(new Date("2026-08-03T12:00:00.000Z")))).toBeInTheDocument();
    expect(screen.getByText("Published")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New draft" })).not.toBeInTheDocument();
  });

  it("offers draft creation when an active published version exists", () => {
    renderDefinition("version-2");
    expect(screen.getByRole("button", { name: "New draft" })).toBeInTheDocument();
  });

  it("navigates to the created draft through the React router without a full reload", async () => {
    fixtures.createDraft.mockResolvedValueOnce({ id: "version-3" });
    renderDefinition("version-2");

    fireEvent.click(screen.getByRole("button", { name: "New draft" }));

    expect(await screen.findByText("Draft version route")).toBeInTheDocument();
    expect(screen.getByTestId("current-path")).toHaveTextContent("/workflows/workflow-detail/versions/version-3");
    expect(fixtures.createDraft).toHaveBeenCalledWith({ id: "workflow-detail", body: { changeLog: "Draft created from management UI" } });
  });

  it.each([
    ["en", "Publishing makes this version immutable. Existing instances remain on their current version."],
    ["ar", "النشر يجعل هذا الإصدار غير قابل للتعديل. ستظل الحالات الحالية للعناصر الحالية مرتبطة بإصداراتها."],
  ] as const)("shows the published-version guidance in %s", (locale, guidance) => {
    renderDefinition("version-2", locale);
    expect(screen.getByText(guidance)).toBeInTheDocument();
  });
});
