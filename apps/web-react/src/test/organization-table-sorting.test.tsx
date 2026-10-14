import * as React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider, useI18n } from "../i18n/messages";
import { OrganizationStructureTable } from "../features/organization/OrganizationPage";
import type { OrganizationUnit } from "../features/organization/api/organization";

const units: OrganizationUnit[] = [
  {
    id: "unit-beta", code: "Z-10", name: "Beta", typeCode: "division", typeNameEn: "Zeta", typeNameAr: "زيتا",
    description: "", parentOrganizationalUnitId: "parent-z", parentName: "Zulu", isActive: false, sortOrder: 1, childCount: 0, currentMemberCount: 10,
  },
  {
    id: "unit-alpha", code: "A-2", name: "Alpha", typeCode: "department", typeNameEn: "Alpha type", typeNameAr: "ألفا",
    description: "", parentOrganizationalUnitId: null, isActive: true, sortOrder: 0, childCount: 0, currentMemberCount: 2,
  },
];

function TableHarness() {
  const { t } = useI18n();
  return <OrganizationStructureTable units={units} canManage={false} onEdit={vi.fn()} onStatus={vi.fn()} onDelete={vi.fn()} emptyLabel="No units" t={t} />;
}

function renderTable() {
  window.localStorage.setItem("nexus-locale", "en");
  render(<I18nProvider><TableHarness /></I18nProvider>);
}

function rowNames() {
  return screen.getAllByRole("row").slice(1).map((row) => within(row).getAllByRole("cell")[1].textContent);
}

describe("Organization structure table sorting", () => {
  it.each([
    ["Code", ["Alpha", "Beta"]],
    ["Type", ["Alpha", "Beta"]],
    ["Parent", ["Alpha", "Beta"]],
    ["Members", ["Alpha", "Beta"]],
    ["Status", ["Alpha", "Beta"]],
  ])("sorts by %s in both directions", (label, ascendingNames) => {
    renderTable();
    const header = screen.getByRole("columnheader", { name: label });
    const button = within(header).getByRole("button", { name: label });

    fireEvent.click(button);
    expect(header).toHaveAttribute("aria-sort", "ascending");
    expect(rowNames()).toEqual(ascendingNames);

    fireEvent.click(button);
    expect(header).toHaveAttribute("aria-sort", "descending");
    expect(rowNames()).toEqual([...ascendingNames].reverse());
  });

  it("starts name-sorted and reverses the default column when selected", () => {
    renderTable();
    const header = screen.getByRole("columnheader", { name: "Name" });
    expect(header).toHaveAttribute("aria-sort", "ascending");
    expect(rowNames()).toEqual(["Alpha", "Beta"]);

    fireEvent.click(within(header).getByRole("button", { name: "Name" }));
    expect(header).toHaveAttribute("aria-sort", "descending");
    expect(rowNames()).toEqual(["Beta", "Alpha"]);
  });
});
