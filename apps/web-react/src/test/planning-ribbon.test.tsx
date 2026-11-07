import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PlanningRibbonTabs } from "@features/projects/components/PlanningRibbon";

const tabs = [
  { id: "file" as const, label: "File" },
  { id: "task" as const, label: "Task" },
  { id: "resource" as const, label: "Resource" },
  { id: "project" as const, label: "Project" },
  { id: "view" as const, label: "View" },
  { id: "report" as const, label: "Report" },
];

describe("PlanningRibbonTabs keyboard access", () => {
  it("uses roving focus and moves selection with arrow, Home and End keys", () => {
    const onChange = vi.fn();
    render(<PlanningRibbonTabs ariaLabel="Planning commands" activeTab="project" tabs={tabs} onChange={onChange} />);
    const project = screen.getByRole("tab", { name: "Project" });
    const task = screen.getByRole("tab", { name: "Task" });
    const view = screen.getByRole("tab", { name: "View" });
    const file = screen.getByRole("tab", { name: "File" });
    const report = screen.getByRole("tab", { name: "Report" });

    expect(project).toHaveAttribute("tabindex", "0");
    expect(task).toHaveAttribute("tabindex", "-1");
    project.focus();
    fireEvent.keyDown(project, { key: "ArrowRight" });
    expect(onChange).toHaveBeenLastCalledWith("view");
    expect(view).toHaveFocus();
    fireEvent.keyDown(view, { key: "End" });
    expect(onChange).toHaveBeenLastCalledWith("report");
    expect(report).toHaveFocus();
    fireEvent.keyDown(report, { key: "Home" });
    expect(onChange).toHaveBeenLastCalledWith("file");
    expect(file).toHaveFocus();
  });

  it("reverses horizontal navigation in RTL", () => {
    const onChange = vi.fn();
    render(<div dir="rtl"><PlanningRibbonTabs ariaLabel="أوامر التخطيط" activeTab="task" tabs={tabs} onChange={onChange} /></div>);
    const task = screen.getByRole("tab", { name: "Task" });
    fireEvent.keyDown(task, { key: "ArrowLeft" });
    expect(onChange).toHaveBeenLastCalledWith("resource");
  });
});
