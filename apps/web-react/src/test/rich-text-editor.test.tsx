import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
