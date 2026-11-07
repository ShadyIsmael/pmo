export type RegionalDefaults = {
  defaultLanguage?: "en" | "ar" | null;
  timeZone?: string | null;
  currency?: string | null;
  dateFormat?: string | null;
  timeFormat?: string | null;
  firstDayOfWeek?: string | null;
};

let currentDefaults: RegionalDefaults | null = null;

export function setRegionalDefaults(defaults: RegionalDefaults | null): void {
  currentDefaults = defaults ? { ...defaults } : null;
}

export function getRegionalDefaults(): RegionalDefaults | null {
  return currentDefaults;
}

export function getFirstDayOfWeekIndex(fallback = 0): number {
  const day = currentDefaults?.firstDayOfWeek?.toLowerCase();
  const index: Record<string, number> = {
    sunday: 0,
    monday: 1,
    tuesday: 2,
    wednesday: 3,
    thursday: 4,
    friday: 5,
    saturday: 6,
  };
  return day && day in index ? index[day] : fallback;
}
