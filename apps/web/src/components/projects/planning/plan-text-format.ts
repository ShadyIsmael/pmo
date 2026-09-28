import type {
  PlanFontSizeToken,
  PlanHighlightColorToken,
  PlanTextColorToken,
  PlanTextFormatDto,
} from "@/lib/api/planning";

export const PLAN_TEXT_COLOR_TOKENS = ["Slate", "Blue", "Teal", "Green", "Amber", "Red", "Violet"] as const satisfies readonly PlanTextColorToken[];
export const PLAN_HIGHLIGHT_COLOR_TOKENS = ["Yellow", "Blue", "Green", "Orange", "Pink"] as const satisfies readonly PlanHighlightColorToken[];
export const PLAN_FONT_SIZE_TOKENS = ["Small", "Normal", "Large", "ExtraLarge"] as const satisfies readonly PlanFontSizeToken[];

export function isPlanTextColorToken(value: string): value is PlanTextColorToken {
  return PLAN_TEXT_COLOR_TOKENS.some((token) => token === value);
}

export function isPlanHighlightColorToken(value: string): value is PlanHighlightColorToken {
  return PLAN_HIGHLIGHT_COLOR_TOKENS.some((token) => token === value);
}

export function isPlanFontSizeToken(value: string): value is PlanFontSizeToken {
  return PLAN_FONT_SIZE_TOKENS.some((token) => token === value);
}

export function planTextFormatClassName(format?: PlanTextFormatDto | null) {
  if (!format) return "";
  const classes: string[] = [];
  if (format.isBold) classes.push("font-semibold");
  if (format.isItalic) classes.push("italic");
  if (format.isUnderline) classes.push("underline decoration-1 underline-offset-2");
  if (format.textColorToken) classes.push(PLAN_TEXT_COLOR_CLASSES[format.textColorToken]);
  if (format.highlightColorToken) classes.push(PLAN_HIGHLIGHT_CLASSES[format.highlightColorToken]);
  if (format.fontSizeToken) classes.push(PLAN_FONT_SIZE_CLASSES[format.fontSizeToken]);
  return classes.join(" ");
}

const PLAN_TEXT_COLOR_CLASSES: Record<PlanTextColorToken, string> = {
  Slate: "text-slate-700 dark:text-slate-200",
  Blue: "text-blue-700 dark:text-blue-300",
  Teal: "text-teal-700 dark:text-teal-300",
  Green: "text-green-700 dark:text-green-300",
  Amber: "text-amber-700 dark:text-amber-300",
  Red: "text-red-700 dark:text-red-300",
  Violet: "text-violet-700 dark:text-violet-300",
};

const PLAN_HIGHLIGHT_CLASSES: Record<PlanHighlightColorToken, string> = {
  Yellow: "bg-yellow-100 dark:bg-yellow-900/40",
  Blue: "bg-blue-100 dark:bg-blue-900/40",
  Green: "bg-green-100 dark:bg-green-900/40",
  Orange: "bg-orange-100 dark:bg-orange-900/40",
  Pink: "bg-pink-100 dark:bg-pink-900/40",
};

const PLAN_FONT_SIZE_CLASSES: Record<PlanFontSizeToken, string> = {
  Small: "text-[11px]",
  Normal: "text-[13px]",
  Large: "text-[15px]",
  ExtraLarge: "text-[17px]",
};
