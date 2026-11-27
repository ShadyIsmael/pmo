import * as React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";

function NestedSideSheets() {
  const [settingsOpen, setSettingsOpen] = React.useState(true);
  const [phaseChangeOpen, setPhaseChangeOpen] = React.useState(false);

  return <SideSheet
    open={settingsOpen}
    onOpenChange={setSettingsOpen}
    title="Project Settings"
    ariaLabel="Project Settings"
  >
    <SideSheetBody>
      <button type="button" onClick={() => setPhaseChangeOpen(true)}>Open Change Phase</button>
      <SideSheet
        open={phaseChangeOpen}
        onOpenChange={setPhaseChangeOpen}
        title="Change Phase"
        ariaLabel="Change Phase"
      >
        <SideSheetBody>Phase transition details</SideSheetBody>
      </SideSheet>
    </SideSheetBody>
  </SideSheet>;
}

describe("SideSheet stacking", () => {
  afterEach(() => {
    document.body.style.overflow = "";
  });

  it("closes only the top sheet on Escape and keeps the page locked until the last sheet closes", async () => {
    document.body.style.overflow = "auto";
    render(<NestedSideSheets />);

    fireEvent.click(screen.getByRole("button", { name: "Open Change Phase" }));
    expect(screen.getByRole("dialog", { name: "Change Phase" })).toBeInTheDocument();
    expect(document.body.style.overflow).toBe("hidden");

    fireEvent.keyDown(document, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Change Phase" })).not.toBeInTheDocument());
    expect(screen.getByRole("dialog", { name: "Project Settings" })).toBeInTheDocument();
    expect(document.body.style.overflow).toBe("hidden");

    fireEvent.keyDown(document, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Project Settings" })).not.toBeInTheDocument());
    expect(document.body.style.overflow).toBe("auto");
  });
});
