import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectChangeRequestActionSheet } from "../features/projects/components/ProjectOverviewActionSheets";
import type { ProjectDto } from "../features/projects/api/projects";

const fixtures = vi.hoisted(() => ({
  permissions: ["ChangeRequest.Manage"] as string[],
  management: { canMutate: true, effectiveReadOnly: false },
  createRequest: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => fixtures.management }));
vi.mock("@features/projects/api/projects", () => ({
  useCreateProjectChangeRequest: () => ({ mutateAsync: fixtures.createRequest, isPending: false }),
  useUpdateProjectChangeRequestDraft: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSubmitProjectChangeRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useProjectChangeRequestApproverOptions: () => ({ data: {
    approvers: [{ userId: "approver-1", fullName: "Project Sponsor", projectRole: "Project Sponsor", isSponsor: true, isMandatory: true }],
    allowedStrategies: ["AllRequired", "Sequential"], sponsorUserId: "approver-1", policyMessage: null,
  }, isLoading: false, isError: false }),
  useCreateProjectHandoverRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useProjectHandoverManagers: () => ({ data: [], isLoading: false, isError: false }),
}));

const project = { id: "project-1", budget: 1000 } as ProjectDto;

function renderSheet() {
  const onOpenChange = vi.fn();
  render(<I18nProvider><ProjectChangeRequestActionSheet open onOpenChange={onOpenChange} project={project} /></I18nProvider>);
  return { onOpenChange, form: screen.getByRole("combobox", { name: /Change Type/ }).closest("form")! };
}

describe("Project change request side sheet", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["ChangeRequest.Manage"];
    fixtures.management = { canMutate: true, effectiveReadOnly: false };
    fixtures.createRequest.mockReset();
    fixtures.createRequest.mockResolvedValue({ id: "change-1" });
  });

  it("validates trimmed reasons before submitting", async () => {
    const { form } = renderSheet();
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "   " } });
    fireEvent.submit(form);

    expect(await screen.findByRole("alert")).toHaveTextContent("Enter a reason for the change request.");
    expect(fixtures.createRequest).not.toHaveBeenCalled();
  });

  it("submits the selected change type and validated financial fields", async () => {
    const { form, onOpenChange } = renderSheet();
    fireEvent.change(screen.getByRole("combobox", { name: /Change Type/ }), { target: { value: "Baseline" } });
    fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: "  Adjust the delivery baseline  " } });
    fireEvent.change(screen.getByLabelText("Budget impact amount"), { target: { value: "125.50" } });
    fireEvent.change(screen.getByLabelText("Justification"), { target: { value: "Approved scope adjustment" } });
    fireEvent.change(screen.getByLabelText(/Impact description/), { target: { value: "No schedule delay" } });
    fireEvent.change(screen.getByLabelText("Attachments"), { target: { value: "baseline-review.pdf" } });
    fireEvent.submit(form);

    await waitFor(() => expect(fixtures.createRequest).toHaveBeenCalledWith({
      projectId: "project-1",
      type: "Baseline",
      reason: "Adjust the delivery baseline",
      budgetChangeAmount: 125.5,
      justification: "Approved scope adjustment",
      impact: "No schedule delay",
      attachments: "baseline-review.pdf",
      approvalStrategy: "AllRequired",
      approverUserIds: ["approver-1"],
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("shows an API error without losing the entered reason", async () => {
    fixtures.createRequest.mockRejectedValueOnce(new Error("Change request is no longer allowed."));
    const { form } = renderSheet();
    const reason = screen.getByLabelText(/Reason/) as HTMLInputElement;
    fireEvent.change(reason, { target: { value: "Request an administrative update" } });
    fireEvent.submit(form);

    expect(await screen.findByRole("alert")).toHaveTextContent("Change request is no longer allowed.");
    expect(reason).toHaveValue("Request an administrative update");
  });

  it("does not render for users without change-request management permission", () => {
    fixtures.permissions = [];
    render(<I18nProvider><ProjectChangeRequestActionSheet open onOpenChange={vi.fn()} project={project} /></I18nProvider>);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not render in an effective read-only management view", () => {
    fixtures.management = { canMutate: true, effectiveReadOnly: true };
    render(<I18nProvider><ProjectChangeRequestActionSheet open onOpenChange={vi.fn()} project={project} /></I18nProvider>);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not render without a mutable management context", () => {
    fixtures.management = { canMutate: false, effectiveReadOnly: false };
    render(<I18nProvider><ProjectChangeRequestActionSheet open onOpenChange={vi.fn()} project={project} /></I18nProvider>);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
