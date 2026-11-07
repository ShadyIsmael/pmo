import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { MyWorkLayout } from "../features/my-work/MyWorkLayout";
import { MyWorkTabs } from "../features/my-work/MyWorkTabs";

function RouteLocation() {
  const location = useLocation();
  return <output aria-label="Current location">{location.pathname}</output>;
}

function WorkSurface({ title, content }: { title: string; content: string }) {
  return <>
    <h1>{title}</h1>
    <MyWorkTabs />
    <p>{content}</p>
  </>;
}

function WorkRoutes() {
  return <>
    <Routes>
      <Route path="/my-work" element={<MyWorkLayout />}>
        <Route index element={<Navigate to="tasks" replace />} />
        <Route path="tasks" element={<WorkSurface title="Tasks page header" content="Tasks content" />} />
        <Route path="approvals" element={<WorkSurface title="Approvals page header" content="Approvals content" />} />
        <Route path="timesheets" element={<WorkSurface title="My Time page header" content="My Time content" />} />
      </Route>
    </Routes>
    <RouteLocation />
  </>;
}

function renderMyWork(initialEntry = "/my-work") {
  return render(<MemoryRouter initialEntries={[initialEntry]}><I18nProvider><WorkRoutes /></I18nProvider></MemoryRouter>);
}

describe("My Work route tabs", () => {
  beforeEach(() => window.localStorage.clear());

  it("uses the horizontal route tabs below the page header and defaults /my-work to Tasks", () => {
    renderMyWork();

    const tabs = screen.getAllByRole("tab");
    expect(tabs.map((tab) => tab.textContent?.trim())).toEqual(["My Time", "Approvals", "Tasks"]);
    expect(screen.getByRole("tablist")).toHaveClass("overflow-x-auto");
    expect(screen.getByLabelText("Current location")).toHaveTextContent("/my-work/tasks");
    expect(screen.getByText("Tasks content")).toBeInTheDocument();
    expect(screen.queryByText("Approvals content")).not.toBeInTheDocument();
    expect(screen.queryByText("My Time content")).not.toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Tasks" })).toHaveAttribute("aria-current", "page");

    const header = screen.getByRole("heading", { name: "Tasks page header" });
    const tabList = screen.getByRole("tablist");
    expect(header.compareDocumentPosition(tabList) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("keeps tab navigation route-backed and renders only the selected surface", () => {
    renderMyWork("/my-work/timesheets");

    expect(screen.getByLabelText("Current location")).toHaveTextContent("/my-work/timesheets");
    expect(screen.getByRole("tab", { name: "My Time" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("My Time content")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Approvals" }));
    expect(screen.getByLabelText("Current location")).toHaveTextContent("/my-work/approvals");
    expect(screen.getByRole("tab", { name: "Approvals" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("Approvals content")).toBeInTheDocument();
    expect(screen.queryByText("My Time content")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Tasks" }));
    expect(screen.getByLabelText("Current location")).toHaveTextContent("/my-work/tasks");
    expect(screen.getByRole("tab", { name: "Tasks" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("Tasks content")).toBeInTheDocument();
    expect(screen.queryByText("Approvals content")).not.toBeInTheDocument();
  });

  it("localizes tab labels and keeps Arabic direction", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderMyWork("/my-work/approvals");

    expect(screen.getAllByRole("tab").map((tab) => tab.textContent?.trim())).toEqual(["وقتي", "الموافقات", "المهام"]);
    expect(document.documentElement).toHaveAttribute("dir", "rtl");
    expect(screen.getByRole("tab", { name: "الموافقات" })).toHaveAttribute("aria-current", "page");
  });
});
