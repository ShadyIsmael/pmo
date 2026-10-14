import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { AuditLogPageFull } from "../features/operations/OperationsPagesFull";

const mocks = vi.hoisted(() => ({ useAuditEntries: vi.fn() }));

vi.mock("@api/audit", () => ({ useAuditEntries: mocks.useAuditEntries }));

const auditEntry = {
  id: "audit-1",
  entityType: "Project",
  entityId: "project-123",
  action: "Project.Updated",
  changes: "Status changed",
  userId: "user-1",
  userName: "Alex Admin",
  timestamp: "2026-09-28T12:00:00Z",
};

function renderPage(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<I18nProvider><AuditLogPageFull /></I18nProvider>);
}

describe("audit log filters", () => {
  beforeEach(() => {
    mocks.useAuditEntries.mockReset().mockReturnValue({
      data: { items: [auditEntry], totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    });
  });

  it("applies supported entity, action, and entity ID filters to the audit query", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Advanced filters" }));
    fireEvent.change(screen.getByLabelText("Entity type"), { target: { value: "Project" } });
    fireEvent.change(screen.getByLabelText("Action name"), { target: { value: "Project.Updated" } });
    fireEvent.change(screen.getByLabelText("Entity ID"), { target: { value: "project-123" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));

    expect(mocks.useAuditEntries).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      entityType: "Project",
      action: "Project.Updated",
      entityId: "project-123",
      search: undefined,
    });
  });

  it("clears applied filters and presents filter controls in Arabic", () => {
    renderPage("ar");
    fireEvent.click(screen.getByRole("button", { name: "عوامل التصفية المتقدمة" }));
    fireEvent.change(screen.getByLabelText("نوع الكيان"), { target: { value: "Project" } });
    fireEvent.click(screen.getByRole("button", { name: "تطبيق عوامل التصفية" }));
    fireEvent.click(screen.getByRole("button", { name: "مسح عوامل التصفية" }));

    expect(mocks.useAuditEntries).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      entityType: undefined,
      action: undefined,
      entityId: undefined,
      search: undefined,
    });
  });

  it("sends the debounced search term to the server", async () => {
    renderPage();
    fireEvent.change(screen.getByPlaceholderText("Search by user, action, or entity..."), { target: { value: "Alex Admin" } });

    await waitFor(() => expect(mocks.useAuditEntries).toHaveBeenLastCalledWith({
      page: 1,
      pageSize: 25,
      entityType: undefined,
      action: undefined,
      entityId: undefined,
      search: "Alex Admin",
    }));
  });

  it("requests the next audit page from the server", () => {
    mocks.useAuditEntries.mockReturnValue({
      data: { items: [auditEntry], totalCount: 26, totalPages: 2 },
      isLoading: false,
      isError: false,
    });
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(mocks.useAuditEntries).toHaveBeenLastCalledWith({
      page: 2,
      pageSize: 25,
      entityType: undefined,
      action: undefined,
      entityId: undefined,
      search: undefined,
    });
  });
});
