import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectLifecycleCatalogPage } from "../features/administration/ProjectLifecycleCatalogPage";

const lifecycleMocks = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  action: vi.fn(),
  phases: [] as unknown[],
  statuses: [] as unknown[],
}));

vi.mock("../auth/AuthProvider", () => ({ useAuth: () => ({ user: {
  id: "admin-1",
  permissions: [
    "ProjectLifecyclePhase.View", "ProjectLifecyclePhase.Create", "ProjectLifecyclePhase.Edit", "ProjectLifecyclePhase.ActivateDeactivate",
    "ProjectLifecycleStatus.View", "ProjectLifecycleStatus.Create", "ProjectLifecycleStatus.Edit", "ProjectLifecycleStatus.ActivateDeactivate",
  ],
} }) }));
vi.mock("../api/management", () => ({ useManagement: () => ({ snapshot: null, canMutate: true, loading: false }) }));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@components/ui/ConfirmProvider")>()),
  useConfirm: () => vi.fn().mockResolvedValue(false),
}));
vi.mock("../features/administration/api/projectLifecycle", () => ({
  useProjectLifecycleCatalog: (kind: "phases" | "statuses") => ({ data: kind === "phases" ? lifecycleMocks.phases : lifecycleMocks.statuses, isLoading: false, isError: false, refetch: vi.fn() }),
  useCreateProjectLifecycleItem: () => ({ mutateAsync: lifecycleMocks.create, isPending: false }),
  useUpdateProjectLifecycleItem: () => ({ mutateAsync: lifecycleMocks.update, isPending: false }),
  useProjectLifecycleItemAction: () => ({ mutateAsync: lifecycleMocks.action, isPending: false }),
}));

function renderCatalog() {
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/project-lifecycle"]}><ProjectLifecycleCatalogPage /></MemoryRouter></I18nProvider>);
}

describe("Project Lifecycle Reference Data", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    lifecycleMocks.create.mockReset().mockResolvedValue(undefined);
    lifecycleMocks.update.mockReset().mockResolvedValue(undefined);
    lifecycleMocks.action.mockReset().mockResolvedValue(undefined);
    lifecycleMocks.phases = [];
    lifecycleMocks.statuses = [];
  });

  it("creates a status with reporting semantics and no transition setup", async () => {
    renderCatalog();
    fireEvent.click(screen.getByRole("tab", { name: "Project Statuses" }));
    fireEvent.click(screen.getByRole("button", { name: "Add entry" }));

    const dialog = screen.getByRole("dialog", { name: "Add entry" });
    fireEvent.change(within(dialog).getByLabelText("Stable code"), { target: { value: "DELIVERY_READY" } });
    fireEvent.change(within(dialog).getByLabelText("Name (English)"), { target: { value: "Delivery ready" } });
    fireEvent.change(within(dialog).getByLabelText("Name (Arabic)"), { target: { value: "جاهز للتسليم" } });
    fireEvent.change(within(dialog).getByLabelText("Reporting category"), { target: { value: "Completed" } });
    fireEvent.change(within(dialog).getByLabelText("Reporting attention"), { target: { value: "Critical" } });

    expect(within(dialog).queryByText(/allowed next|transition step/i)).not.toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Save entry" }));

    await waitFor(() => expect(lifecycleMocks.create).toHaveBeenCalledWith(expect.objectContaining({
      code: "DELIVERY_READY",
      nameEn: "Delivery ready",
      nameAr: "جاهز للتسليم",
      reportingCategory: "Completed",
      attentionLevel: "Critical",
      organizationId: null,
    })));
  });
});
