import * as React from "react";
import { createPortal } from "react-dom";
import { Bold, Italic, List, ListOrdered, Smile, Underline } from "lucide-react";
import { Button } from "@components/ui/Button";
import { cn } from "@lib/utils";
import { useAnchoredOverlayPosition } from "./AnchoredOverlay";

export type RichTextCopy = Record<string, string>;
type FormatCommand = "bold" | "italic" | "underline" | "insertUnorderedList" | "insertOrderedList";

const emojis = ["😀", "😊", "👍", "👏", "🎯", "✅", "⚠️", "🚀", "💡", "🔥", "🎉", "🙏", "❤️", "💬", "📌", "⏳", "📎", "⭐", "🙌", "😄", "🤝", "👀", "💪", "✨"];
const allowedTags = new Set(["B", "STRONG", "I", "EM", "U", "S", "BR", "P", "DIV", "UL", "OL", "LI", "BLOCKQUOTE"]);
const dangerousTags = new Set(["SCRIPT", "STYLE", "IFRAME", "OBJECT", "EMBED", "FORM", "META", "LINK", "SVG", "MATH", "TEMPLATE"]);

function escapeText(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

function fallbackSanitize(value: string): string {
  const text = value
    .replace(/<(script|style|iframe|object|embed|form|svg|math|template)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, "")
    .replace(/<[^>]*>/g, "");
  return escapeText(text);
}

export function sanitizeRichText(value: string): string {
  if (!value || typeof DOMParser === "undefined") return fallbackSanitize(value);
  const parsed = new DOMParser().parseFromString(value, "text/html");
  const clean = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return escapeText(node.textContent ?? "");
    if (node.nodeType !== Node.ELEMENT_NODE) return "";
    const element = node as HTMLElement;
    if (dangerousTags.has(element.tagName)) return "";
    const children = Array.from(element.childNodes).map(clean).join("");
    if (!allowedTags.has(element.tagName)) return children;
    const tag = element.tagName.toLowerCase();
    return tag === "br" ? "<br>" : `<${tag}>${children}</${tag}>`;
  };
  return Array.from(parsed.body.childNodes).map(clean).join("");
}

function hasContent(value: string): boolean {
  if (typeof DOMParser === "undefined") return value.replace(/<[^>]*>/g, "").trim().length > 0;
  const parsed = new DOMParser().parseFromString(value, "text/html");
  return Boolean(parsed.body.textContent?.replace(/\u00a0/g, " ").trim());
}

function ToolbarButton({ label, onClick, children, buttonRef }: { label: string; onClick: () => void; children: React.ReactNode; buttonRef?: React.Ref<HTMLButtonElement> }) {
  return <Button ref={buttonRef} type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={label} title={label} onMouseDown={(event) => event.preventDefault()} onClick={onClick}>{children}</Button>;
}

export function RichTextEditor({ id, value, onChange, placeholder, ariaLabel, t, minHeight = "min-h-28", fillAvailable = false }: { id: string; value: string; onChange: (_value: string) => void; placeholder: string; ariaLabel: string; t: RichTextCopy; minHeight?: string; fillAvailable?: boolean }) {
  const editorRef = React.useRef<HTMLDivElement>(null);
  const emojiTriggerRef = React.useRef<HTMLButtonElement>(null);
  const emojiPickerRef = React.useRef<HTMLDivElement>(null);
  const lastValueRef = React.useRef<string | null>(null);
  const [empty, setEmpty] = React.useState(!hasContent(value));
  const [emojiOpen, setEmojiOpen] = React.useState(false);
  const emojiPickerStyle = useAnchoredOverlayPosition({ open: emojiOpen, anchorRef: emojiTriggerRef, contentRef: emojiPickerRef });

  React.useEffect(() => {
    if (!emojiOpen) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (emojiTriggerRef.current?.contains(target) || emojiPickerRef.current?.contains(target)) return;
      setEmojiOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setEmojiOpen(false);
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [emojiOpen]);

  React.useEffect(() => {
    if (!editorRef.current || lastValueRef.current === value) return;
    const nextValue = sanitizeRichText(value || "");
    editorRef.current.innerHTML = nextValue;
    lastValueRef.current = nextValue;
    setEmpty(!hasContent(nextValue));
  }, [value]);

  const emitChange = () => {
    if (!editorRef.current) return;
    const nextValue = sanitizeRichText(editorRef.current.innerHTML);
    lastValueRef.current = nextValue;
    setEmpty(!hasContent(nextValue));
    onChange(nextValue);
  };

  const runCommand = (command: FormatCommand) => {
    editorRef.current?.focus();
    document.execCommand(command, false);
    emitChange();
  };

  const insertEmoji = (emoji: string) => {
    editorRef.current?.focus();
    document.execCommand("insertText", false, emoji);
    emitChange();
    setEmojiOpen(false);
  };

  const onEditorKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if ((event.ctrlKey || event.metaKey) && ["b", "i", "u"].includes(event.key.toLowerCase())) {
      event.preventDefault();
      runCommand(event.key.toLowerCase() === "b" ? "bold" : event.key.toLowerCase() === "i" ? "italic" : "underline");
    }
  };

  return <div className={cn("overflow-hidden rounded-md border border-input bg-background focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-primary/10", fillAvailable && "flex min-h-0 flex-1 flex-col")}>
    <div className={cn("flex flex-wrap items-center gap-1 border-b border-border bg-muted/30 p-1.5", fillAvailable && "shrink-0")} aria-label={t.formatting}>
      <ToolbarButton label={t.bold} onClick={() => runCommand("bold")}><Bold className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.italic} onClick={() => runCommand("italic")}><Italic className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.underline} onClick={() => runCommand("underline")}><Underline className="h-4 w-4" /></ToolbarButton>
      <span className="mx-1 h-5 w-px bg-border" aria-hidden="true" />
      <ToolbarButton label={t.bulletedList} onClick={() => runCommand("insertUnorderedList")}><List className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.numberedList} onClick={() => runCommand("insertOrderedList")}><ListOrdered className="h-4 w-4" /></ToolbarButton>
      <div><ToolbarButton buttonRef={emojiTriggerRef} label={t.emoji} onClick={() => setEmojiOpen((open) => !open)}><Smile className="h-4 w-4" /></ToolbarButton>{emojiOpen ? createPortal(<div ref={emojiPickerRef} role="dialog" aria-label={t.emoji} style={emojiPickerStyle} className="grid max-h-72 w-56 grid-cols-6 gap-1 overflow-y-auto rounded-md border border-border bg-background p-2 shadow-lg">{emojis.map((emoji) => <button key={emoji} type="button" className="flex h-8 w-8 items-center justify-center rounded-md text-lg hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onMouseDown={(event) => event.preventDefault()} onClick={() => insertEmoji(emoji)} aria-label={emoji}>{emoji}</button>)}</div>, document.body) : null}</div>
    </div>
    <div className={cn("relative", fillAvailable && "flex min-h-0 flex-1")}>
      {empty ? <span className="pointer-events-none absolute start-3 top-3 text-sm text-muted-foreground">{placeholder}</span> : null}
      <div id={id} ref={editorRef} contentEditable suppressContentEditableWarning role="textbox" aria-multiline="true" aria-label={ariaLabel} onInput={emitChange} onKeyDown={onEditorKeyDown} onPaste={(event) => { event.preventDefault(); document.execCommand("insertText", false, event.clipboardData.getData("text/plain")); emitChange(); }} className={cn(fillAvailable ? "min-h-40 max-h-none flex-1" : minHeight, "overflow-y-auto whitespace-pre-wrap break-words px-3 py-3 text-sm leading-6 outline-none [&_blockquote]:border-s-2 [&_blockquote]:border-primary/40 [&_blockquote]:ps-3 [&_li]:ms-5 [&_ol]:list-decimal [&_p]:mb-2 [&_p:last-child]:mb-0 [&_ul]:list-disc")} />
    </div>
  </div>;
}

export function RichTextContent({ value, className }: { value?: string; className?: string }) {
  if (!value) return <span className={cn("text-muted-foreground", className)}>—</span>;
  return <div className={cn("whitespace-pre-wrap break-words text-sm leading-6 [&_blockquote]:my-2 [&_blockquote]:border-s-2 [&_blockquote]:border-primary/40 [&_blockquote]:ps-3 [&_li]:ms-5 [&_ol]:list-decimal [&_p]:mb-2 [&_p:last-child]:mb-0 [&_ul]:list-disc", className)} dangerouslySetInnerHTML={{ __html: sanitizeRichText(value) }} />;
}
