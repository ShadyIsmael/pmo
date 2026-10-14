import * as React from "react";
import { createPortal } from "react-dom";
import { CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@lib/utils";

export interface DateInputLabels {
  openCalendar: string;
  today: string;
  clear: string;
  invalid: string;
}

const LABELS: Record<"en" | "ar", DateInputLabels> = {
  en: { openCalendar: "Open calendar", today: "Today", clear: "Clear", invalid: "Enter a valid date (dd/mm/yyyy)." },
  ar: { openCalendar: "فتح التقويم", today: "اليوم", clear: "مسح", invalid: "أدخل تاريخًا صحيحًا (يوم/شهر/سنة)." },
};

const PANEL_WIDTH = 272;
const PANEL_HEIGHT = 296;

function readLocale(): string {
  if (typeof document === "undefined") return "en";
  return document.documentElement.lang || "en";
}

function labelsFor(locale: string): DateInputLabels {
  return LABELS[locale.toLowerCase().startsWith("ar") ? "ar" : "en"];
}

function pad(value: number): string {
  return String(value).padStart(2, "0");
}

function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

function daysInMonth(year: number, month: number): number {
  return [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
}

function toText(value: unknown): string {
  if (typeof value === "string") return value;
  if (typeof value === "number") return String(value);
  return "";
}

function normalizeIso(value?: unknown): string {
  const candidate = toText(value).trim().slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(candidate)) return "";
  const [year, month, day] = candidate.split("-").map(Number);
  if (month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) return "";
  return candidate;
}

function isoFromDigits(digits: string): string | null {
  if (digits.length !== 8) return null;
  const day = Number(digits.slice(0, 2));
  const month = Number(digits.slice(2, 4));
  const year = Number(digits.slice(4, 8));
  if (year < 1 || month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) return null;
  return `${String(year).padStart(4, "0")}-${pad(month)}-${pad(day)}`;
}

function formatText(iso: string): string {
  if (!iso) return "";
  const [year, month, day] = iso.split("-");
  return `${day}/${month}/${year}`;
}

function applyMask(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 8);
  return [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4, 8)].filter(Boolean).join("/");
}

function dateFromIso(iso: string): Date | null {
  const normalized = normalizeIso(iso);
  if (!normalized) return null;
  const [year, month, day] = normalized.split("-").map(Number);
  return new Date(year, month - 1, day);
}

function isoFromDate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function widthClasses(className?: string): string {
  return (className ?? "")
    .split(/\s+/)
    .filter((token) => /^(?:max-)?w-/.test(token))
    .join(" ");
}

function changeEventFor(input: HTMLInputElement | null): React.ChangeEvent<HTMLInputElement> {
  return {
    target: input,
    currentTarget: input,
    type: "change",
    bubbles: true,
    cancelable: false,
    defaultPrevented: false,
    eventPhase: 2,
    isTrusted: false,
    nativeEvent: new Event("change"),
    preventDefault() {},
    stopPropagation() {},
    stopImmediatePropagation() {},
    isDefaultPrevented: () => false,
    isPropagationStopped: () => false,
    persist() {},
    timeStamp: Date.now(),
  } as unknown as React.ChangeEvent<HTMLInputElement>;
}

export interface DateInputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "onChange" | "type"> {
  onChange?: (event: React.ChangeEvent<HTMLInputElement>) => void;
}

export const DateInput = React.forwardRef<HTMLInputElement, DateInputProps>(function DateInput(
  { className, value, defaultValue, name, onChange, onBlur, onFocus, min, max, disabled, required, readOnly, id, placeholder, ...rest },
  forwardedRef
) {
  const controlled = value !== undefined;
  const seed = () => normalizeIso(controlled ? value : defaultValue);
  const [iso, setIso] = React.useState(seed);
  const [text, setText] = React.useState(() => formatText(seed()));
  const [open, setOpen] = React.useState(false);
  const [view, setView] = React.useState(() => {
    const base = dateFromIso(seed()) ?? new Date();
    return new Date(base.getFullYear(), base.getMonth(), 1);
  });
  const [position, setPosition] = React.useState<{ top: number; left: number } | null>(null);
  const inputRef = React.useRef<HTMLInputElement | null>(null);
  const hiddenRef = React.useRef<HTMLInputElement | null>(null);
  const triggerRef = React.useRef<HTMLButtonElement | null>(null);
  const panelRef = React.useRef<HTMLDivElement | null>(null);
  const focusedRef = React.useRef(false);
  const labels = labelsFor(readLocale());
  const minIso = normalizeIso(min);
  const maxIso = normalizeIso(max);

  const setRefs = (node: HTMLInputElement | null) => {
    inputRef.current = node;
    if (typeof forwardedRef === "function") forwardedRef(node);
    else if (forwardedRef) forwardedRef.current = node;
  };

  React.useEffect(() => {
    if (!controlled) return;
    const next = normalizeIso(value);
    setIso(next);
    if (!focusedRef.current) setText(formatText(next));
  }, [controlled, value]);

  React.useLayoutEffect(() => {
    if (!open) {
      setPosition(null);
      return;
    }
    const rect = triggerRef.current?.getBoundingClientRect();
    if (!rect) return;
    const rtl = document.documentElement.dir === "rtl";
    const top = rect.bottom + PANEL_HEIGHT + 8 > window.innerHeight ? Math.max(8, rect.top - PANEL_HEIGHT - 4) : rect.bottom + 4;
    const left = rtl
      ? Math.max(8, Math.min(rect.right - PANEL_WIDTH, window.innerWidth - PANEL_WIDTH - 8))
      : Math.max(8, Math.min(rect.left, window.innerWidth - PANEL_WIDTH - 8));
    setPosition({ top, left });
  }, [open]);

  React.useEffect(() => {
    if (!open) return;
    const onDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (panelRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const emit = (next: string) => {
    if (hiddenRef.current) hiddenRef.current.value = next;
    onChange?.(changeEventFor(hiddenRef.current));
  };

  const commit = (next: string) => {
    setIso(next);
    setText(formatText(next));
    emit(next);
  };

  const applyBoundsValidity = (candidate: string) => {
    const outOfRange = (minIso && candidate < minIso) || (maxIso && candidate > maxIso);
    inputRef.current?.setCustomValidity(outOfRange ? labels.invalid : "");
  };

  const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value;
    if (/^\s*\d{4}-\d{1,2}-\d{1,2}/.test(raw)) {
      const pasted = normalizeIso(raw.trim().slice(0, 10));
      if (pasted) {
        commit(pasted);
        applyBoundsValidity(pasted);
        return;
      }
    }
    const masked = applyMask(raw);
    setText(masked);
    if (masked === "") {
      setIso("");
      emit("");
      inputRef.current?.setCustomValidity("");
      return;
    }
    const parsed = isoFromDigits(masked.replace(/\D/g, ""));
    if (parsed) {
      setIso(parsed);
      emit(parsed);
      applyBoundsValidity(parsed);
    }
  };

  const handleBlur = (event: React.FocusEvent<HTMLInputElement>) => {
    focusedRef.current = false;
    if (text.trim() === "") {
      inputRef.current?.setCustomValidity("");
    } else {
      const parsed = isoFromDigits(text.replace(/\D/g, ""));
      if (parsed) applyBoundsValidity(parsed);
      else setText(formatText(iso));
    }
    onBlur?.(event);
  };

  const handleFocus = (event: React.FocusEvent<HTMLInputElement>) => {
    focusedRef.current = true;
    onFocus?.(event);
  };

  const openCalendar = (nextOpen: boolean) => {
    if (nextOpen) {
      const base = dateFromIso(iso) ?? new Date();
      setView(new Date(base.getFullYear(), base.getMonth(), 1));
    }
    setOpen(nextOpen);
  };

  const selectDay = (dayIso: string) => {
    commit(dayIso);
    applyBoundsValidity(dayIso);
    setOpen(false);
    inputRef.current?.focus();
  };

  return (
    <span className={cn("relative inline-flex w-full items-center", widthClasses(className))}>
      <input
        {...rest}
        ref={setRefs}
        id={id}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        className={cn(
          "flex h-10 w-full rounded-md border border-input bg-background ps-3 pe-9 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50",
          className
        )}
        value={text}
        placeholder={placeholder ?? "dd/mm/yyyy"}
        disabled={disabled}
        required={required}
        readOnly={readOnly}
        onChange={handleChange}
        onBlur={handleBlur}
        onFocus={handleFocus}
      />
      <input ref={hiddenRef} type="hidden" name={name} value={iso} readOnly />
      <button
        ref={triggerRef}
        type="button"
        tabIndex={-1}
        aria-label={labels.openCalendar}
        disabled={disabled || readOnly}
        onMouseDown={(event) => event.preventDefault()}
        onClick={() => openCalendar(!open)}
        className="absolute end-1.5 inline-flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50"
      >
        <CalendarDays className="h-4 w-4" />
      </button>
      {open && position
        ? createPortal(
            <div
              ref={panelRef}
              role="dialog"
              aria-label={labels.openCalendar}
              style={{ position: "fixed", top: position.top, left: position.left, zIndex: 80 }}
              className="w-[17rem] rounded-md border bg-popover p-2 text-popover-foreground shadow-md"
            >
              <CalendarPanel
                view={view}
                onViewChange={setView}
                value={iso}
                min={minIso}
                max={maxIso}
                labels={labels}
                locale={readLocale()}
                onSelect={selectDay}
                onToday={() => selectDay(isoFromDate(new Date()))}
                onClear={() => selectDay("")}
              />
            </div>,
            document.body
          )
        : null}
    </span>
  );
});

function CalendarPanel({
  view,
  onViewChange,
  value,
  min,
  max,
  labels,
  locale,
  onSelect,
  onToday,
  onClear,
}: {
  view: Date;
  onViewChange: (view: Date) => void;
  value: string;
  min: string;
  max: string;
  labels: DateInputLabels;
  locale: string;
  onSelect: (iso: string) => void;
  onToday: () => void;
  onClear: () => void;
}) {
  const arabic = locale.toLowerCase().startsWith("ar");
  const weekStart = arabic ? 6 : 0;
  const gregorian = `${locale}-u-ca-gregory`;
  const firstOfMonth = new Date(view.getFullYear(), view.getMonth(), 1);
  const leading = (firstOfMonth.getDay() - weekStart + 7) % 7;
  const totalDays = daysInMonth(view.getFullYear(), view.getMonth() + 1);
  const monthTitle = new Intl.DateTimeFormat(gregorian, { month: "long", year: "numeric" }).format(view);
  const weekdays = Array.from({ length: 7 }, (_, index) => {
    const day = new Date(2024, 0, 6 + weekStart + index);
    return new Intl.DateTimeFormat(gregorian, { weekday: "short" }).format(day);
  });
  const shiftMonth = (delta: number) => onViewChange(new Date(view.getFullYear(), view.getMonth() + delta, 1));

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-1">
        <button
          type="button"
          aria-label={monthTitle}
          onClick={() => shiftMonth(-1)}
          className="inline-flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <ChevronLeft className="h-4 w-4 rtl:rotate-180" />
        </button>
        <p className="text-sm font-medium tabular-nums">{monthTitle}</p>
        <button
          type="button"
          aria-label={monthTitle}
          onClick={() => shiftMonth(1)}
          className="inline-flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <ChevronRight className="h-4 w-4 rtl:rotate-180" />
        </button>
      </div>
      <div className="grid grid-cols-7 gap-0.5">
        {weekdays.map((weekday) => (
          <span key={weekday} className="flex h-7 items-center justify-center text-[11px] font-medium text-muted-foreground">
            {weekday}
          </span>
        ))}
        {Array.from({ length: leading }, (_, index) => (
          <span key={`blank-${index}`} />
        ))}
        {Array.from({ length: totalDays }, (_, index) => {
          const day = index + 1;
          const dayIso = `${view.getFullYear()}-${pad(view.getMonth() + 1)}-${pad(day)}`;
          const outOfRange = Boolean((min && dayIso < min) || (max && dayIso > max));
          return (
            <button
              key={dayIso}
              type="button"
              disabled={outOfRange}
              aria-label={dayIso}
              aria-pressed={value === dayIso}
              onClick={() => onSelect(dayIso)}
              className={cn(
                "inline-flex h-7 items-center justify-center rounded-md text-xs tabular-nums hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:text-muted-foreground/40",
                value === dayIso && "bg-primary font-medium text-primary-foreground hover:bg-primary hover:text-primary-foreground"
              )}
            >
              {day}
            </button>
          );
        })}
      </div>
      <div className="flex items-center justify-between border-t border-border/60 pt-2">
        <button
          type="button"
          onClick={onToday}
          className="rounded-md px-2 py-1 text-xs font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.today}
        </button>
        <button
          type="button"
          onClick={onClear}
          className="rounded-md px-2 py-1 text-xs font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.clear}
        </button>
      </div>
    </div>
  );
}
