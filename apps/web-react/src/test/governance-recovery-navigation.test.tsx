import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { GovernancePage } from "@features/administration/AdminPages";
import { RequireFreshPermission } from "@auth/RequireFreshPermission";
import { Permissions } from "@lib/permissions";

const auth = vi.hoisted(() => ({
  current: null as null | {
    status: "authenticated";
    user: { id: string; permissions: string[] };
    refreshUser: () => Promise<void>;
  },
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => auth.current }));

function renderGovernance(initialPath = "/administration/governance") {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/administration/governance" element={
            <RequireFreshPermission permissions={[
              Permissions.Settings.View,
              Permissions.Workflow.View,
              Permissions.AssignmentRecovery.View,
              Permissions.AssignmentRecovery.Initiate,
              Permissions.AssignmentRecovery.Manage,
            ]}>
              <GovernancePage />
            </RequireFreshPermission>
          } />
          <Route path="/administration/governance/entity-assignment-recovery" element={
            <RequireFreshPermission permissions={[
              Permissions.AssignmentRecovery.View,
              Permissions.AssignmentRecovery.Initiate,
              Permissions.AssignmentRecovery.Manage,
            ]}>
              <div>Mounted recovery page</div>
            </RequireFreshPermission>
          } />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  window.localStorage.removeItem("nexus-locale");
  auth.current = null;
});

describe("Governance entity-recovery navigation", () => {
  it("refreshes effective permissions and reveals the existing recovery entry", async () => {
    const refreshUser = vi.fn(async () => {
      if (auth.current) {
        auth.current.user.permissions = [Permissions.Settings.View, Permissions.AssignmentRecovery.View];
      }
    });
    auth.current = {
      status: "authenticated",
      user: { id: "admin", permissions: [Permissions.Settings.View] },
      refreshUser,
    };

    renderGovernance();

    const link = await screen.findByRole("link", { name: /Entity Assignment & Recovery/i });
    expect(link).toHaveAttribute("href", "/administration/governance/entity-assignment-recovery");
    expect(refreshUser).toHaveBeenCalledOnce();
  });

  it("allows direct recovery navigation after refreshing an authorized user's grants", async () => {
    auth.current = {
      status: "authenticated",
      user: { id: "admin", permissions: [Permissions.Settings.View] },
      refreshUser: vi.fn(async () => {
        if (auth.current) auth.current.user.permissions = [Permissions.AssignmentRecovery.Initiate];
      }),
    };

    renderGovernance("/administration/governance/entity-assignment-recovery");

    expect(await screen.findByText("Mounted recovery page")).toBeInTheDocument();
  });

  it("denies direct access when the refreshed user has no recovery permission", async () => {
    auth.current = {
      status: "authenticated",
      user: { id: "viewer", permissions: [Permissions.Settings.View] },
      refreshUser: vi.fn().mockResolvedValue(undefined),
    };

    renderGovernance("/administration/governance/entity-assignment-recovery");

    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have permission to access this area.");
    expect(screen.queryByText("Mounted recovery page")).not.toBeInTheDocument();
  });

  it("shows a recoverable authorization-refresh error and retries", async () => {
    const refreshUser = vi.fn()
      .mockRejectedValueOnce(new Error("temporary auth service error"))
      .mockImplementationOnce(async () => {
        if (auth.current) auth.current.user.permissions = [Permissions.AssignmentRecovery.View];
      });
    auth.current = {
      status: "authenticated",
      user: { id: "admin", permissions: [Permissions.Settings.View] },
      refreshUser,
    };

    renderGovernance("/administration/governance/entity-assignment-recovery");

    expect(await screen.findByRole("alert")).toHaveTextContent("Your current permissions could not be refreshed.");
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("Mounted recovery page")).toBeInTheDocument();
    expect(refreshUser).toHaveBeenCalledTimes(2);
  });
});
