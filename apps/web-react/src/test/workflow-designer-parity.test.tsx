import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { WorkflowVersionPageFull } from "../features/workflow/WorkflowPagesFull";
import { buildWorkflowEdges } from "../features/workflow/WorkflowFlowDesignerFull";

const mocks = vi.hoisted(() => ({
  permissions: ["Workflow.Manage", "Workflow.Publish"],
  version: vi.fn(),
  definition: vi.fn(),
  metadata: vi.fn(),
  preview: vi.fn(),
  createGuard: vi.fn(),
  createAction: vi.fn(),
  validate: vi.fn(),
  publish: vi.fn(),
  createState: vi.fn(),
  updateState: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ effectiveReadOnly: false, canMutate: true }) }));
vi.mock("../features/workflow/WorkflowFlowDesignerFull", async (importOriginal) => ({
  ...await importOriginal<typeof import("../features/workflow/WorkflowFlowDesignerFull")>(),
  WorkflowFlowDesignerFull: () => <div data-testid="workflow-designer">Flow designer</div>,
}));
vi.mock("../features/workflow/api/workflowsFull", () => ({
  useFullWorkflowVersion: mocks.version,
  useFullWorkflowDefinition: mocks.definition,
  useFullWorkflowMetadata: mocks.metadata,
  useFullWorkflowPreview: mocks.preview,
  useFullWorkflowMutations: () => ({
    validate: { mutateAsync: mocks.validate, isPending: false },
    publish: { mutateAsync: mocks.publish, isPending: false },
    createGuard: { mutateAsync: mocks.createGuard, isPending: false },
    createAction: { mutateAsync: mocks.createAction, isPending: false },
    createState: { mutateAsync: mocks.createState, isPending: false },
    updateState: { mutateAsync: mocks.updateState, isPending: false },
    deleteState: { mutateAsync: vi.fn(), isPending: false },
    createTransition: { mutateAsync: vi.fn(), isPending: false },
    updateTransition: { mutateAsync: vi.fn(), isPending: false },
    deleteTransition: { mutateAsync: vi.fn(), isPending: false },
  }),
}));

const states = [
  { id: "state-open", code: "OPEN", nameEn: "Open", nameAr: "مفتوحة", description: "Waiting for review.", displayOrder: 1, isInitial: true, isFinal: false, isActive: true },
  { id: "state-done", code: "DONE", nameEn: "Done", nameAr: "منتهية", description: "Work is complete.", displayOrder: 2, isInitial: false, isFinal: true, isActive: true },
];

const transition = {
  id: "transition-submit",
  code: "SUBMIT",
  nameEn: "Submit",
  nameAr: "إرسال",
  fromStateId: "state-open",
  toStateId: "state-done",
  fromStateCode: "OPEN",
  toStateCode: "DONE",
  displayOrder: 1,
  isActive: true,
  guards: [],
  actions: [],
};

function renderVersion(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <MemoryRouter initialEntries={["/workflows/workflow-1/versions/version-1"]}>
      <I18nProvider>
        <Routes>
          <Route path="/workflows/:id/versions/:versionId" element={<WorkflowVersionPageFull />} />
        </Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("workflow designer parity", () => {
  beforeEach(() => {
    mocks.permissions = ["Workflow.Manage", "Workflow.Publish"];
    mocks.version.mockReset().mockReturnValue({
      data: { id: "version-1", versionNumber: 2, lifecycleStatus: "Draft", rowVersion: [1], states, transitions: [transition] },
      isLoading: false,
      isError: false,
    });
    mocks.definition.mockReset().mockReturnValue({ data: { id: "workflow-1", name: "Delivery", entityType: "WorkItem" }, isLoading: false });
    mocks.metadata.mockReset().mockReturnValue({
      data: {
        guardTypes: ["RequireFact", "RequirePermission", "RequireRole", "RequireComment", "CustomGuard"],
        actionTypes: ["SetStatus"],
        entityTypes: [{ entityType: "WorkItem", facts: [{ code: "Priority", nameEn: "Priority", nameAr: "الأولوية", valueType: "number" }] }],
      },
      isLoading: false,
    });
    mocks.preview.mockReset().mockReturnValue({ data: [] });
    mocks.createGuard.mockReset().mockResolvedValue({});
    mocks.createAction.mockReset().mockResolvedValue({});
    mocks.validate.mockReset().mockResolvedValue({ errors: [] });
    mocks.publish.mockReset().mockResolvedValue({});
    mocks.createState.mockReset().mockResolvedValue({});
    mocks.updateState.mockReset().mockResolvedValue({});
  });

  it("labels flow edges with the canonical transition code in both locales", () => {
    expect(buildWorkflowEdges([transition])[0]).toMatchObject({
      id: "transition-submit",
      source: "state-open",
      target: "state-done",
      label: "SUBMIT",
    });
  });

  it.each([
    { lifecycleStatus: "Draft", locale: "en" as const, expected: "A published version is immutable. This creates an editable copy." },
    { lifecycleStatus: "Draft", locale: "ar" as const, expected: "الإصدار المنشور غير قابل للتعديل. سيتم إنشاء نسخة قابلة للتحرير." },
    { lifecycleStatus: "Published", locale: "ar" as const, expected: "منشور" },
    { lifecycleStatus: "Retired", locale: "en" as const, expected: "Retired" },
  ])("localizes the $lifecycleStatus version-header description in $locale", ({ lifecycleStatus, locale, expected }) => {
    mocks.version.mockReturnValue({
      data: { id: "version-1", versionNumber: 2, lifecycleStatus, rowVersion: [1], states, transitions: [transition] },
      isLoading: false,
      isError: false,
    });

    renderVersion(locale);

    expect(screen.getByText(expected)).toBeInTheDocument();
  });

  it("keeps the flow designer visible while switching between states, transitions, and preview", () => {
    renderVersion();
    expect(screen.getByTestId("workflow-designer")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Graph" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    expect(screen.getByTestId("workflow-designer")).toBeInTheDocument();
    expect(screen.getByText("SUBMIT")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Preview" }));
    expect(screen.getByTestId("workflow-designer")).toBeInTheDocument();
  });

  it("uses compact, centered table spacing for workflow states and transitions", () => {
    renderVersion();

    expect(screen.getByRole("table")).toHaveClass("text-xs", "[&_th]:h-10", "[&_th]:text-center", "[&_td]:py-2.5");
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    expect(screen.getByRole("table")).toHaveClass("text-xs", "[&_th]:h-10", "[&_th]:text-center", "[&_td]:py-2.5");
  });

  it("shows localized transition names, destinations, and empty-state text in the preview", () => {
    mocks.preview.mockReturnValue({
      data: [{
        code: "OPEN",
        nameEn: "Open",
        nameAr: "مفتوحة",
        isInitial: true,
        isFinal: false,
        transitions: [{ code: "SUBMIT", nameEn: "Submit", nameAr: "إرسال", toStateCode: "DONE", toStateNameEn: "Done", toStateNameAr: "منتهية" }],
      }, {
        code: "DONE",
        nameEn: "Done",
        nameAr: "منتهية",
        isInitial: false,
        isFinal: true,
        transitions: [],
      }],
    });

    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Preview" }));

    expect(screen.getByText("Structured preview of the current draft.")).toBeInTheDocument();
    expect(screen.getByText("Submit")).toBeInTheDocument();
    expect(screen.getByText("→ Done")).toBeInTheDocument();
    expect(screen.getByText("No transitions configured.")).toBeInTheDocument();
  });

  it("localizes workflow preview transition names and destinations in Arabic", () => {
    mocks.preview.mockReturnValue({
      data: [{
        code: "OPEN",
        nameEn: "Open",
        nameAr: "مفتوحة",
        isInitial: true,
        isFinal: false,
        transitions: [{ code: "SUBMIT", nameEn: "Submit", nameAr: "إرسال", toStateCode: "DONE", toStateNameEn: "Done", toStateNameAr: "منتهية" }],
      }],
    });

    renderVersion("ar");
    fireEvent.click(screen.getByRole("button", { name: "معاينة" }));

    expect(screen.getByText("معاينة منظمة للمسودة الحالية.")).toBeInTheDocument();
    expect(screen.getByText("إرسال")).toBeInTheDocument();
    expect(screen.getByText("→ منتهية")).toBeInTheDocument();
  });

  it("requires confirmation before publishing and respects cancellation", () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Publish" }));
    const dialog = screen.getByRole("dialog", { name: "Publish this workflow version?" });

    expect(within(dialog).getByText("Publishing makes this version immutable. Existing workflow instances remain on their current versions.")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));

    expect(screen.queryByRole("dialog", { name: "Publish this workflow version?" })).not.toBeInTheDocument();
    expect(mocks.publish).not.toHaveBeenCalled();
  });

  it("hides validation from view-only users because validation requires Workflow.Manage", () => {
    mocks.permissions = ["Workflow.View"];
    renderVersion();

    expect(screen.queryByRole("button", { name: "Validate" })).not.toBeInTheDocument();
    expect(mocks.validate).not.toHaveBeenCalled();
  });

  it("publishes only after the confirmation is accepted", async () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Publish" }));
    const dialog = screen.getByRole("dialog", { name: "Publish this workflow version?" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Publish" }));

    await waitFor(() => expect(mocks.publish).toHaveBeenCalledWith({ id: "version-1", rowVersion: [1] }));
    expect(screen.queryByRole("dialog", { name: "Publish this workflow version?" })).not.toBeInTheDocument();
  });

  it("keeps the publish confirmation open and reports API failures", async () => {
    mocks.publish.mockRejectedValueOnce(new Error("conflict"));
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Publish" }));
    const dialog = screen.getByRole("dialog", { name: "Publish this workflow version?" });
    fireEvent.click(within(dialog).getByRole("button", { name: "Publish" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Unable to publish this workflow version. Try again.");
    expect(dialog).toBeInTheDocument();
    expect(mocks.publish).toHaveBeenCalledTimes(1);
  });

  it("localizes the publish confirmation in Arabic", () => {
    renderVersion("ar");
    fireEvent.click(screen.getByRole("button", { name: "نشر" }));
    const dialog = screen.getByRole("dialog", { name: "هل تريد نشر إصدار مسار العمل؟" });

    expect(within(dialog).getByText("سيجعل النشر هذا الإصدار غير قابل للتعديل. ستظل مثيلات مسار العمل الحالية مرتبطة بإصداراتها الحالية.")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "إلغاء" })).toBeInTheDocument();
  });

  it("shows existing guard and action types when editing a transition, matching the Next detail sheet", () => {
    mocks.version.mockReturnValue({
      data: {
        id: "version-1", versionNumber: 2, lifecycleStatus: "Draft", rowVersion: [1], states,
        transitions: [{
          ...transition,
          guards: [{ id: "guard-1", guardType: "RequireRole", configurationJson: "{}", executionOrder: 10, isActive: true }],
          actions: [{ id: "action-1", actionType: "SetStatus", configurationJson: "{}", executionOrder: 10, isActive: true }],
        }],
      },
      isLoading: false,
      isError: false,
    });

    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Open" }));
    const sheet = screen.getByRole("dialog", { name: "Open" });

    expect(within(sheet).getByText("Guards")).toBeInTheDocument();
    expect(within(sheet).getByText("RequireRole")).toBeInTheDocument();
    expect(within(sheet).getByText("Actions")).toBeInTheDocument();
    expect(within(sheet).getByText("SetStatus")).toBeInTheDocument();
  });

  it("matches the state table order and saves the state description", async () => {
    renderVersion();
    expect(screen.getAllByRole("columnheader").map((header) => header.textContent)).toEqual([
      "Display order", "Code", "English name", "Arabic name", "Initial", "Final", "Actions",
    ]);

    fireEvent.click(screen.getAllByRole("button", { name: "Open" })[0]);
    const sheet = screen.getByRole("dialog", { name: "Open" });
    const description = within(sheet).getByLabelText("Description");
    expect(description).toHaveValue("Waiting for review.");
    fireEvent.change(description, { target: { value: "Work item is waiting for review." } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.updateState).toHaveBeenCalledWith(expect.objectContaining({
      id: "state-open",
      body: expect.objectContaining({ description: "Work item is waiting for review.", displayOrder: 1 }),
    })));
  });

  it("localizes the state description label in Arabic", () => {
    renderVersion("ar");
    fireEvent.click(screen.getAllByRole("button", { name: "فتح" })[0]);
    const sheet = screen.getByRole("dialog", { name: "فتح" });
    expect(within(sheet).getByLabelText("الوصف")).toBeInTheDocument();
  });

  it("builds typed fact-guard configuration from the entity metadata", async () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Add guard" }));
    const dialog = screen.getByRole("dialog", { name: "Add guard" });
    fireEvent.change(within(dialog).getByLabelText("Rule type"), { target: { value: "RequireFact" } });
    fireEvent.change(within(dialog).getByLabelText("Fact"), { target: { value: "Priority" } });
    fireEvent.change(within(dialog).getByLabelText("Expected value"), { target: { value: "5" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.createGuard).toHaveBeenCalledWith({
      id: "transition-submit",
      body: {
        guardType: "RequireFact",
        configuration: { fact: "Priority", operator: "eq", expectedValue: 5 },
        executionOrder: 10,
        isActive: true,
      },
    }));
  });

  it("saves the API-supported comment guard configuration", async () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Add guard" }));
    const dialog = screen.getByRole("dialog", { name: "Add guard" });
    fireEvent.change(within(dialog).getByLabelText("Rule type"), { target: { value: "RequireComment" } });
    fireEvent.change(within(dialog).getByLabelText("Configuration JSON"), { target: { value: '{"minLength":8}' } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.createGuard).toHaveBeenCalledWith({
      id: "transition-submit",
      body: {
        guardType: "RequireComment",
        configuration: { minLength: 8 },
        executionOrder: 10,
        isActive: true,
      },
    }));
  });

  it("does not offer React-only action creation when adding a guard", () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Add guard" }));
    const dialog = screen.getByRole("dialog", { name: "Add guard" });

    expect(within(dialog).queryByRole("button", { name: "Actions" })).not.toBeInTheDocument();
    expect(within(dialog).queryByRole("option", { name: "SetStatus" })).not.toBeInTheDocument();
    expect(mocks.createAction).not.toHaveBeenCalled();
  });

  it("reports malformed JSON for extensible guards instead of swallowing the error", async () => {
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Add guard" }));
    const dialog = screen.getByRole("dialog", { name: "Add guard" });
    fireEvent.change(within(dialog).getByLabelText("Rule type"), { target: { value: "CustomGuard" } });
    fireEvent.change(within(dialog).getByLabelText("Configuration JSON"), { target: { value: "{invalid" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Enter configuration as a valid JSON object.");
    expect(mocks.createGuard).not.toHaveBeenCalled();
    expect(mocks.createAction).not.toHaveBeenCalled();
  });

  it("keeps the rule sheet open and shows API save failures", async () => {
    mocks.createGuard.mockRejectedValueOnce(new Error("Rule configuration was rejected."));
    renderVersion();
    fireEvent.click(screen.getByRole("button", { name: "Transitions" }));
    fireEvent.click(screen.getByRole("button", { name: "Add guard" }));
    const dialog = screen.getByRole("dialog", { name: "Add guard" });
    fireEvent.change(within(dialog).getByLabelText("Rule type"), { target: { value: "RequirePermission" } });
    fireEvent.change(within(dialog).getByLabelText("Permission"), { target: { value: "WorkItem.Edit" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Rule configuration was rejected.");
    expect(dialog).toBeInTheDocument();
  });

  it("shows fact metadata in Arabic when the Arabic locale is active", () => {
    renderVersion("ar");
    fireEvent.click(screen.getByRole("button", { name: "الانتقالات" }));
    fireEvent.click(screen.getByRole("button", { name: "إضافة شرط" }));
    const dialog = screen.getByRole("dialog", { name: "إضافة شرط" });
    fireEvent.change(within(dialog).getByLabelText("نوع القاعدة"), { target: { value: "RequireFact" } });

    expect(within(dialog).getByRole("option", { name: "الأولوية" })).toBeInTheDocument();
  });
});
