"use client";

import { useEffect, useRef, useState, type KeyboardEvent, type MouseEvent } from "react";
import { Bold, Italic, List, ListOrdered, Smile, Underline } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type Copy = Record<string, string>;
type FormatCommand = "bold" | "italic" | "underline" | "insertUnorderedList" | "insertOrderedList";

const emojis = ["😀", "😊", "👍", "👏", "🎯", "✅", "⚠️", "🚀", "💡", "🔥", "🎉", "🙏", "❤️", "💬", "📌", "⏳", "📎", "⭐", "🙌", "😄", "🤝", "👀", "💪", "✨"];
const allowedTags = new Set(["B", "STRONG", "I", "EM", "U", "S", "BR", "P", "DIV", "UL", "OL", "LI", "BLOCKQUOTE"]);

function fallbackSanitize(value: string): string {
  return value.replace(/<\/?(script|style|iframe|object|embed|form|meta|link)[^>]*>/gi, "").replace(/\son[a-z]+\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+)/gi, "").replace(/javascript\s*:/gi, "");
}

export function sanitizeRichText(value: string): string {
  if (!value || typeof DOMParser === "undefined") return fallbackSanitize(value);
  const document = new DOMParser().parseFromString(value, "text/html");
  const clean = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return node.textContent ?? "";
    if (node.nodeType !== Node.ELEMENT_NODE) return "";
    const element = node as HTMLElement;
    const children = Array.from(element.childNodes).map(clean).join("");
    if (!allowedTags.has(element.tagName)) return children;
    return element.tagName === "BR" ? "<br>" : `<${element.tagName.toLowerCase()}>${children}</${element.tagName.toLowerCase()}>`;
  };
  return Array.from(document.body.childNodes).map(clean).join("");
}

function hasContent(value: string): boolean {
  if (typeof DOMParser === "undefined") return value.replace(/<[^>]*>/g, "").trim().length > 0;
  const document = new DOMParser().parseFromString(value, "text/html");
  return Boolean(document.body.textContent?.replace(/\u00a0/g, " ").trim());
}

function ToolbarButton({ label, onClick, children }: { label: string; onClick: () => void; children: React.ReactNode }) {
  const keepSelection = (event: MouseEvent<HTMLButtonElement>) => event.preventDefault();
  return <Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={label} title={label} onMouseDown={keepSelection} onClick={onClick}>{children}</Button>;
}

export function RichTextEditor({ id, value, onChange, placeholder, ariaLabel, t, minHeight = "min-h-28" }: { id: string; value: string; onChange: (_value: string) => void; placeholder: string; ariaLabel: string; t: Copy; minHeight?: string }) {
  const editorRef = useRef<HTMLDivElement>(null);
  const lastValueRef = useRef<string | null>(null);
  const [empty, setEmpty] = useState(!hasContent(value));
  const [emojiOpen, setEmojiOpen] = useState(false);

  useEffect(() => {
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

  const onEditorKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if ((event.ctrlKey || event.metaKey) && ["b", "i", "u"].includes(event.key.toLowerCase())) {
      event.preventDefault();
      runCommand(event.key.toLowerCase() === "b" ? "bold" : event.key.toLowerCase() === "i" ? "italic" : "underline");
    }
  };

  return <div className="overflow-hidden rounded-md border border-input bg-background focus-within:border-primary/50 focus-within:ring-2 focus-within:ring-primary/10">
    <div className="flex flex-wrap items-center gap-1 border-b border-border bg-muted/30 p-1.5" aria-label={t.formatting}>
      <ToolbarButton label={t.bold} onClick={() => runCommand("bold")}><Bold className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.italic} onClick={() => runCommand("italic")}><Italic className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.underline} onClick={() => runCommand("underline")}><Underline className="h-4 w-4" /></ToolbarButton>
      <span className="mx-1 h-5 w-px bg-border" aria-hidden="true" />
      <ToolbarButton label={t.bulletedList} onClick={() => runCommand("insertUnorderedList")}><List className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label={t.numberedList} onClick={() => runCommand("insertOrderedList")}><ListOrdered className="h-4 w-4" /></ToolbarButton>
      <div className="relative"><ToolbarButton label={t.emoji} onClick={() => setEmojiOpen((open) => !open)}><Smile className="h-4 w-4" /></ToolbarButton>{emojiOpen ? <div className="absolute start-0 top-10 z-20 grid w-56 grid-cols-6 gap-1 rounded-md border border-border bg-background p-2 shadow-lg">{emojis.map((emoji) => <button key={emoji} type="button" className="flex h-8 w-8 items-center justify-center rounded-md text-lg hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onMouseDown={(event) => event.preventDefault()} onClick={() => insertEmoji(emoji)} aria-label={emoji}>{emoji}</button>)}</div> : null}</div>
    </div>
    <div className="relative">
      {empty ? <span className="pointer-events-none absolute start-3 top-3 text-sm text-muted-foreground">{placeholder}</span> : null}
      <div id={id} ref={editorRef} contentEditable suppressContentEditableWarning role="textbox" aria-multiline="true" aria-label={ariaLabel} onInput={emitChange} onKeyDown={onEditorKeyDown} onPaste={(event) => { event.preventDefault(); document.execCommand("insertText", false, event.clipboardData.getData("text/plain")); emitChange(); }} className={cn(minHeight, "max-h-64 overflow-y-auto whitespace-pre-wrap break-words px-3 py-3 text-sm leading-6 outline-none [&_blockquote]:border-s-2 [&_blockquote]:border-primary/40 [&_blockquote]:ps-3 [&_li]:ms-5 [&_ol]:list-decimal [&_p]:mb-2 [&_p:last-child]:mb-0 [&_ul]:list-disc")} />
    </div>
  </div>;
}

export function RichTextContent({ value, className }: { value?: string; className?: string }) {
  if (!value) return <span className={cn("text-muted-foreground", className)}>—</span>;
  return <div className={cn("whitespace-pre-wrap break-words text-sm leading-6 [&_blockquote]:my-2 [&_blockquote]:border-s-2 [&_blockquote]:border-primary/40 [&_blockquote]:ps-3 [&_li]:ms-5 [&_ol]:list-decimal [&_p]:mb-2 [&_p:last-child]:mb-0 [&_ul]:list-disc", className)} dangerouslySetInnerHTML={{ __html: sanitizeRichText(value) }} />;
}
