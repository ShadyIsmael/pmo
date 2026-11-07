import { beforeEach, describe, expect, it } from "vitest";
import { formatDateTimeValue, formatDateValue } from "../lib/formatting/date";
import { setRegionalDefaults } from "../lib/formatting/regional";

describe("shared date formatting", () => {
  beforeEach(() => setRegionalDefaults(null));

  it("keeps date-only values on their calendar day and preserves Latin digits in Arabic", () => {
    const expected = new Intl.DateTimeFormat("ar-EG-u-nu-latn", { dateStyle: "medium", timeZone: "UTC" }).format(new Date(Date.UTC(2026, 0, 2)));
    expect(formatDateValue("2026-01-02", "ar")).toBe(expected);
  });

  it.each(["Pacific/Kiritimati", "Etc/GMT+12"])(
    "keeps date-only values stable in configured timezone %s",
    (timeZone) => {
      setRegionalDefaults({ timeZone });
      const date = new Date(Date.UTC(2026, 0, 2));
      const expected = new Intl.DateTimeFormat("en-US-u-nu-latn", {
        dateStyle: "medium",
        timeZone: "UTC",
      }).format(date);

      expect(formatDateValue("2026-01-02", "en")).toBe(expected);
      expect(formatDateValue("2026-01-02", "en", { weekday: "long", month: "long", day: "numeric", year: "numeric" })).toBe(
        new Intl.DateTimeFormat("en-US-u-nu-latn", {
          weekday: "long",
          month: "long",
          day: "numeric",
          year: "numeric",
          timeZone: "UTC",
        }).format(date),
      );
    },
  );

  it("formats date-times consistently and preserves unavailable values", () => {
    const date = new Date("2026-10-17T10:30:00.000Z");
    const expected = new Intl.DateTimeFormat("ar-EG-u-nu-latn", { dateStyle: "medium", timeStyle: "short" }).format(date);
    expect(formatDateTimeValue(date, "ar")).toBe(expected);
    expect(formatDateTimeValue("invalid-date", "ar")).toBe("-");
    expect(formatDateValue("invalid-date", "en")).toBe("-");
  });
});
