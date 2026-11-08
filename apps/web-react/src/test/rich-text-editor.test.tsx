import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { RichTextContent, RichTextEditor, sanitizeRichText, type RichTextCopy } from "@components/ui/rich-text-editor";

const editorCopy: RichTextCopy = {
  formatting: "Formatting",
  bold: "Bold",
  italic: "Italic",
  underline: "Underline",
  bulletedList: "Bulleted list",
  numberedList: "Numbered list",
  emoji: "Emoji",
};

describe("meeting rich text", () => {
  it("keeps supported formatting while removing attributes and unsafe elements", () => {
    const clean = sanitizeRichText('<p onclick="run()"><strong>Decision</strong><img src=x onerror="run()"></p><script>alert(1)</script>');
    expect(clean).toBe("<p><strong>Decision</strong></p>");
  });

  it("escapes markup encoded inside unsupported elements rather than reactivating it", () => {
    const clean = sanitizeRichText("<span>&lt;img src=x onerror=run()&gt;</span>");
    expect(clean).toBe("&lt;img src=x onerror=run()&gt;");
  });

  it("renders sanitized persisted meeting notes", () => {
    const { container } = render(<RichTextContent value={'<p>Safe note</p><img src=x onerror="run()">'} />);
    expect(screen.getByText("Safe note")).toBeInTheDocument();
    expect(container.querySelector("img")).toBeNull();
  });

  it("removes parser-created active namespaces and every unsafe attribute", () => {
    const clean = sanitizeRichText('<svg><g onload="run()"></g></svg><math><mi>math</mi></math><p style="background:url(javascript:run())" onclick="run()">Safe text</p>');
    const parsed = new DOMParser().parseFromString(clean, "text/html");

    expect(parsed.body.querySelector("svg, math, img, script, iframe, object")).toBeNull();
    expect(parsed.body.querySelector("[onload], [onclick], [onerror], [style]")).toBeNull();
    expect(parsed.body.textContent).toContain("Safe text");
  });

  it("keeps report text inert when links and active attributes are supplied", () => {
    const clean = sanitizeRichText('<p onmouseover="run()">Summary <a href="javascript:run()" onclick="run()">details</a><iframe srcdoc="<script>run()</script>"></iframe></p>');
    const parsed = new DOMParser().parseFromString(clean, "text/html");

    expect(clean).toBe("<p>Summary details</p>");
    expect(parsed.body.querySelector("a, iframe, script, [href], [onclick], [onmouseover]")).toBeNull();
    expect(parsed.body.textContent).toBe("Summary details");
  });

  it("keeps the DOMParser fallback output inert", () => {
    vi.stubGlobal("DOMParser", undefined);
    try {
      const clean = sanitizeRichText('<script>alert(1)</script><img src=x onerror="run()">Safe text');
      expect(clean).toBe("Safe text");
      expect(clean).not.toMatch(/<[^>]+>/);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("renders the emoji picker outside the editor's clipped frame", () => {
    const { getByTestId } = render(
      <div data-testid="editor-frame" className="overflow-hidden">
        <RichTextEditor id="notes" value="" onChange={() => undefined} placeholder="Write notes" ariaLabel="Meeting notes" t={editorCopy} />
      </div>
    );

    fireEvent.click(screen.getByRole("button", { name: "Emoji" }));

    const picker = screen.getByRole("dialog", { name: "Emoji" });
    expect(picker.parentElement).toBe(document.body);
    expect(getByTestId("editor-frame")).not.toContainElement(picker);
  });
});
