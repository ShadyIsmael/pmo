import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { WorkItemStatusDto } from "@api/workItems";
import type { WorkItemDto } from "@api/workItemsFull";
import { WorkItemViewSwitcher, WorkItemsKanban } from "@features/delivery/WorkItemsKanban";

const statuses: WorkItemStatusDto[] = [
  { id: "s1", code: "Open", nameEn: "Open", nameAr: "مفتوح", displayOrder: 1, isActive: true, isTerminal: false },
  { id: "s2", code: "Done", nameEn: "Done", nameAr: "مكتمل", displayOrder: 2, isActive: true, isTerminal: true },
];
const item: WorkItemDto = {
  id: "wi-1", title: "Prepare release", type: "Task", priority: "Medium", status: "Open", source: "Manual",
  createdAt: "2026-10-01T00:00:00Z", createdBy: "user-1",
};
const translate = (key: string) => ({
  "workItems.priorityMedium": "Medium",
  "workItems.changeStatus": "Change status",
  "common.noResults": "No items",
}[key] ?? key);

describe("Work Items Kanban", () => {
  it("supports accessible status selection for editors", () => {
    const onMove = vi.fn();
    render(<MemoryRouter><WorkItemsKanban items={[item]} statuses={statuses} locale="en" canEdit movingItemId={undefined} onMove={onMove} t={translate} /></MemoryRouter>);

    fireEvent.change(screen.getByRole("combobox", { name: "Change status: Prepare release" }), { target: { value: "Done" } });
    expect(onMove).toHaveBeenCalledWith("wi-1", "Done");
  });

  it("does not expose status changes to read-only viewers", () => {
    render(<MemoryRouter><WorkItemsKanban items={[item]} statuses={statuses} locale="en" canEdit={false} onMove={vi.fn()} t={translate} /></MemoryRouter>);

    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Prepare release" })).toHaveAttribute("href", "/work-items/wi-1");
  });

  it("opens the item detail side sheet when the parent screen provides an open handler", () => {
    const onOpen = vi.fn();
    render(<MemoryRouter><WorkItemsKanban items={[item]} statuses={statuses} locale="en" canEdit={false} onMove={vi.fn()} onOpen={onOpen} t={translate} /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Prepare release" }));
    expect(onOpen).toHaveBeenCalledWith("wi-1");
    expect(screen.queryByRole("link", { name: "Prepare release" })).not.toBeInTheDocument();
  });

  it("switches between list and Kanban views", () => {
    const onChange = vi.fn();
    render(<WorkItemViewSwitcher view="list" onChange={onChange} label="Work item view" listLabel="List" kanbanLabel="Kanban" />);

    fireEvent.click(screen.getByRole("tab", { name: "Kanban" }));
    expect(onChange).toHaveBeenCalledWith("kanban");
  });
});
