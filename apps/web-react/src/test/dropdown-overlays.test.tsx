import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Button } from "@components/ui/Button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { calculateAnchoredOverlayPosition } from "@components/ui/AnchoredOverlay";

describe("shared dropdown overlays", () => {
  it("renders Select options outside a narrow clipped container", () => {
    const { getByTestId } = render(
      <div data-testid="clipped-frame" className="w-32 overflow-hidden">
        <Select>
          <SelectTrigger aria-label="Choose status"><SelectValue placeholder="Choose status" /></SelectTrigger>
          <SelectContent><SelectItem value="active">Active</SelectItem></SelectContent>
        </Select>
      </div>
    );

    fireEvent.click(screen.getByRole("button", { name: "Choose status" }));

    const listbox = screen.getByRole("listbox");
    expect(listbox.parentElement).toBe(document.body);
    expect(getByTestId("clipped-frame")).not.toContainElement(listbox);
    expect(listbox).toHaveStyle({ position: "fixed" });
  });

  it("renders menu content outside a narrow clipped container and closes on outside interaction", () => {
    const { getByTestId } = render(
      <div data-testid="clipped-frame" className="w-32 overflow-hidden">
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button type="button">Actions</Button></DropdownMenuTrigger>
          <DropdownMenuContent><DropdownMenuItem>Open record</DropdownMenuItem></DropdownMenuContent>
        </DropdownMenu>
      </div>
    );

    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    const menu = screen.getByRole("menu");
    expect(menu.parentElement).toBe(document.body);
    expect(getByTestId("clipped-frame")).not.toContainElement(menu);

    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("renders searchable options outside a narrow clipped container", () => {
    const { getByTestId } = render(
      <div data-testid="clipped-frame" className="w-32 overflow-hidden">
        <SearchableSelect options={[{ value: "active", label: "Active" }]} aria-label="Choose owner" />
      </div>
    );

    fireEvent.click(screen.getByRole("combobox", { name: "Choose owner" }));

    const listbox = screen.getByRole("listbox");
    expect(listbox.parentElement).toBe(document.body);
    expect(getByTestId("clipped-frame")).not.toContainElement(listbox);
  });

  it("flips above the trigger and clamps the menu to the viewport", () => {
    const style = calculateAnchoredOverlayPosition(
      { top: 690, bottom: 730, left: 700, right: 780, width: 80, height: 40 },
      { width: 220, height: 220 },
      { width: 800, height: 768 },
      { align: "end" }
    );

    expect(style.position).toBe("fixed");
    expect(style.top).toBe(466);
    expect(style.left).toBe(560);
    expect(style.maxHeight).toBe(678);
    expect(style.zIndex).toBeGreaterThan(100);
  });

  it("uses logical menu alignment in RTL", () => {
    const style = calculateAnchoredOverlayPosition(
      { top: 100, bottom: 140, left: 300, right: 380, width: 80, height: 40 },
      { width: 192, height: 100 },
      { width: 800, height: 768 },
      { align: "end", rtl: true }
    );

    expect(style.left).toBe(300);
  });
});
