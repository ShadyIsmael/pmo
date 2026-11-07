import { getRegionalDefaults } from "./regional";

function parseDateValue(value: Date | string): Date {
  if (value instanceof Date) return value;
  const calendarDate = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return calendarDate ? new Date(Number(calendarDate[1]), Number(calendarDate[2]) - 1, Number(calendarDate[3])) : new Date(value);
}

function dateLocale(locale: string): string {
  return locale === "ar" ? "ar-EG-u-nu-latn" : "en-US-u-nu-latn";
}

function withRegionalTimeZone(options: Intl.DateTimeFormatOptions = {}): Intl.DateTimeFormatOptions {
  return { timeZone: getRegionalDefaults()?.timeZone ?? undefined, ...options };
}

function formatDatePattern(date: Date | string, pattern: string): string {
  const calendarDate = typeof date === "string" ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(date) : null;
  let year: string;
  let month: string;
  let day: string;

  if (calendarDate) {
    [, year, month, day] = calendarDate;
  } else {
    const value = parseDateValue(date);
    if (!Number.isFinite(value.getTime())) return "-";
    const parts = new Intl.DateTimeFormat("en-CA-u-nu-latn", withRegionalTimeZone({
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    })).formatToParts(value);
    year = parts.find(part => part.type === "year")?.value ?? "";
    month = parts.find(part => part.type === "month")?.value ?? "";
    day = parts.find(part => part.type === "day")?.value ?? "";
  }

  return pattern.replace("yyyy", year.padStart(4, "0"))
    .replace("MM", month.padStart(2, "0"))
    .replace("dd", day.padStart(2, "0"));
}

function formatTime(value: Date, locale: string): string {
  const timeFormat = getRegionalDefaults()?.timeFormat;
  if (timeFormat !== "HH:mm" && timeFormat !== "hh:mm tt") {
    return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ hour: "numeric", minute: "2-digit" })).format(value);
  }

  const parts = new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: timeFormat === "HH:mm" ? "h23" : "h12",
  })).formatToParts(value);
  const hour = (parts.find(part => part.type === "hour")?.value ?? "00").padStart(2, "0");
  const minute = (parts.find(part => part.type === "minute")?.value ?? "00").padStart(2, "0");
  const time = `${hour}:${minute}`;
  if (timeFormat === "HH:mm") return time;
  const dayPeriod = parts.find(part => part.type === "dayPeriod")?.value;
  return dayPeriod ? `${time} ${dayPeriod}` : time;
}

export function formatDateValue(date: Date | string, locale = "en", options?: Intl.DateTimeFormatOptions): string {
  const dateOnlyParts = typeof date === "string" ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(date) : null;
  const value = dateOnlyParts
    ? new Date(Date.UTC(Number(dateOnlyParts[1]), Number(dateOnlyParts[2]) - 1, Number(dateOnlyParts[3])))
    : parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  const dateFormat = getRegionalDefaults()?.dateFormat;
  if (!options && dateFormat && ["yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy"].includes(dateFormat)) {
    return formatDatePattern(date, dateFormat);
  }
  const formatOptions = dateOnlyParts
    ? { dateStyle: "medium" as const, ...options, timeZone: "UTC" }
    : withRegionalTimeZone({ dateStyle: "medium", ...options });
  return new Intl.DateTimeFormat(dateLocale(locale), formatOptions).format(value);
}

export function formatDateTimeValue(date: Date | string, locale = "en"): string {
  const value = parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  const regionalDefaults = getRegionalDefaults();
  if (!regionalDefaults?.dateFormat && !regionalDefaults?.timeFormat) {
    return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ dateStyle: "medium", timeStyle: "short" })).format(value);
  }
  return `${formatDateValue(date, locale)} ${formatTime(value, locale)}`;
}

export function formatCalendarMonthValue(date: Date | string, locale = "en"): string {
  const value = parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ month: "long", year: "numeric" })).format(value);
}

export function formatCalendarDayValue(date: Date | string, locale = "en"): string {
  const value = parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ weekday: "long", day: "numeric", month: "long", year: "numeric" })).format(value);
}

export function formatCalendarWeekdayValue(date: Date | string, locale = "en", style: "short" | "long" = "short"): string {
  const value = parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ weekday: style })).format(value);
}

export function formatCalendarTimeValue(date: Date | string, locale = "en"): string {
  const value = parseDateValue(date);
  if (!Number.isFinite(value.getTime())) return "-";
  return formatTime(value, locale);
}

export function formatCalendarWeekRangeValue(from: Date, to: Date, locale = "en"): string {
  if (!Number.isFinite(from.getTime()) || !Number.isFinite(to.getTime())) return "-";
  return new Intl.DateTimeFormat(dateLocale(locale), withRegionalTimeZone({ month: "short", day: "numeric", year: "numeric" })).formatRange(from, to);
}
