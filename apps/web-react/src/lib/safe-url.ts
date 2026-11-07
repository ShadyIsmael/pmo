/**
 * Returns an absolute HTTP(S) URL, or undefined for anything else.
 *
 * Stored user input must never be placed directly into an `href`: a `javascript:` or
 * `data:` value would execute when the link is activated. Callers render the link only
 * when this helper returns a value.
 */
export function safeHttpUrl(value?: string | null): string | undefined {
  if (!value) return undefined;
  try {
    const parsed = new URL(value.trim());
    return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed.href : undefined;
  } catch {
    return undefined;
  }
}
