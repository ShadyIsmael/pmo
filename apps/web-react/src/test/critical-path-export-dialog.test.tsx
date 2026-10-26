import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { CriticalPathExportDialog } from "@features/projects/components/CriticalPathExportDialog";
import type { CriticalPathExportSnapshot } from "@features/projects/lib/critical-path-export";

const { downloadPdf } = vi.hoisted(() => ({ downloadPdf: vi.fn().mockResolvedValue(undefined) }));

vi.mock("@features/projects/lib/critical-path-export", () => ({
  criticalPathExportErrorMessage: () => "Export failed",
  DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS: {
    format: "pdf", scope: "full", pageSize: "AUTO", orientation: "auto", pdfLayout: "fit", pngScale: 2,
    showTaskDates: true, showDuration: true, showTotalFloat: true, showProgress: true,
    showDependencyLabels: true, includeLegend: true, includeSummary: true,
  },
  downloadCriticalPathPdf: downloadPdf,
  downloadCriticalPathPng: vi.fn().mockResolvedValue(undefined),
  downloadCriticalPathSvg: vi.fn(),
  prepareCriticalPathExport: vi.fn(() => ({})),
}));

describe("CriticalPathExportDialog", () => {
  it("keeps export available after success and starts another copy without closing or resetting options", async () => {
    downloadPdf.mockClear();
    const onOpenChange = vi.fn();
    const snapshot = {} as CriticalPathExportSnapshot;
    const t = (key: string) => key;

    render(<I18nProvider><CriticalPathExportDialog open onOpenChange={onOpenChange} snapshot={snapshot} t={t} /></I18nProvider>);

    const sheet = screen.getByRole("dialog", { name: "planning.exportDiagram.title" });
    fireEvent.change(within(sheet).getByLabelText("planning.exportDiagram.pageSize"), { target: { value: "A1" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "planning.exportDiagram.export" }));

    expect(await within(sheet).findByText("planning.exportDiagram.success")).toBeInTheDocument();
    expect(within(sheet).getByLabelText("planning.exportDiagram.pageSize")).toHaveValue("A1");
    expect(within(sheet).getByRole("button", { name: "planning.exportDiagram.done" })).toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("button", { name: "planning.exportDiagram.exportAnotherCopy" }));

    await waitFor(() => expect(downloadPdf).toHaveBeenCalledTimes(2));
    expect(await within(sheet).findByText("planning.exportDiagram.success")).toBeInTheDocument();
    expect(within(sheet).getByLabelText("planning.exportDiagram.pageSize")).toHaveValue("A1");
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });
});
