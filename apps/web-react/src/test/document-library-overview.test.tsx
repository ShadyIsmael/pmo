import * as React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { DocumentLibraryHeader, DocumentLibraryOverview } from "../features/projects/DocumentLibraryOverview";
import { LibraryDocuments } from "../features/projects/LibraryDocuments";
import type { DocumentListItem } from "../features/projects/api/documents";

describe("Document Library overview", () => {
  beforeEach(() => window.localStorage.setItem("nexus-locale", "en"));

  it("renders supplied scoped counts and navigable managed categories", () => {
    const select = vi.fn();
    const props = { totalDocuments: 42, activeTemplates: 7, categories: [{ id: "custom-1", name: "Custom policies", count: 3 }, { id: "custom-2", name: "Finance", count: 0 }], selectedCategoryId: "custom-1", onSelectCategory: select };
    const { rerender } = render(<I18nProvider><DocumentLibraryOverview {...props} /></I18nProvider>);
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    const policies = screen.getByRole("button", { name: /Custom policies/ });
    expect(policies).toHaveAttribute("aria-pressed", "true");
    expect(within(policies).getByText("3 documents")).toBeInTheDocument();
    const iconClass = policies.firstElementChild?.className;
    fireEvent.click(screen.getByRole("button", { name: /Finance/ }));
    expect(select).toHaveBeenCalledWith("custom-2");
    rerender(<I18nProvider><DocumentLibraryOverview {...props} selectedCategoryId="custom-2" /></I18nProvider>);
    expect(screen.getByRole("button", { name: /Finance/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /Custom policies/ }).firstElementChild?.className).toBe(iconClass);
  });

  it("does not manufacture counts and guards upload", () => {
    const upload = vi.fn();
    const { rerender } = render(<I18nProvider><DocumentLibraryHeader canUpload={false} onUpload={upload} /><DocumentLibraryOverview categories={[{ id: "1", name: "Governance" }]} selectedCategoryId="" onSelectCategory={vi.fn()} /></I18nProvider>);
    expect(screen.queryByRole("button", { name: /upload/i })).not.toBeInTheDocument();
    expect(within(screen.getByRole("button", { name: /Governance/ })).getByText("Unavailable")).toBeInTheDocument();
    rerender(<I18nProvider><DocumentLibraryHeader canUpload onUpload={upload} /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: /upload/i }));
    expect(upload).toHaveBeenCalledOnce();
  });

  it("renders semantic file identities, metadata and contextual actions", () => {
    const base = { status: "Active", version: 4, isArchived: false, categoryName: "Policies" };
    const items = [
      { ...base, id: "word", name: "Word file", fileType: "DOCX" },
      { ...base, id: "excel", name: "Excel file", fileType: "XLSX" },
      { ...base, id: "pdf", name: "PDF file", fileType: "PDF" },
    ] as DocumentListItem[];
    const open = vi.fn(); const download = vi.fn();
    render(<I18nProvider><LibraryDocuments items={items} view="list" loading={false} error={false} filtered={false} onRetry={vi.fn()} onOpen={open} onDownload={download} /></I18nProvider>);
    for (const [name, color] of [["Word file", "text-blue-700"], ["Excel file", "text-emerald-700"], ["PDF file", "text-red-700"]]) {
      const article = screen.getByRole("button", { name }).closest("article")!;
      expect(article.querySelector(`.${color}`)).toBeInTheDocument();
      expect(within(article).getByText(/Version: v4/)).toBeInTheDocument();
      expect(within(article).getByText("Policies")).toBeInTheDocument();
    }
    fireEvent.click(screen.getByRole("button", { name: "Word file" }));
    expect(open).toHaveBeenCalledWith("word");
    fireEvent.click(within(screen.getByRole("button", { name: "PDF file" }).closest("article")!).getByRole("button", { name: "Download" }));
    expect(download).toHaveBeenCalledWith(items[2]);
  });
});
