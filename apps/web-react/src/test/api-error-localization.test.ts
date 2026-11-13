import { describe, expect, it } from "vitest";
import { ApiError, getApiFieldError, getApiFieldErrors, setApiErrorLocale } from "../api/client";
import { getLocalizedApiErrorMessage, getLocalizedApiFieldError } from "../api/api-error-localization";

describe("API error localization", () => {
  it("uses localized validation guidance and keeps field errors available", () => {
    setApiErrorLocale("ar");
    const error = new ApiError(
      400,
      getLocalizedApiErrorMessage("VALIDATION_ERROR", 400, "ar"),
      "VALIDATION_ERROR",
      { errors: { effectiveHandoverDate: ["Date is outside the allowed project period."] } },
    );

    expect(error.message).toContain("صحح");
    expect(error.fieldErrors.effectiveHandoverDate).toEqual(["راجع قيمة تاريخ التسليم وصححها."]);
    setApiErrorLocale("en");
  });

  it("distinguishes permission and concurrency responses in both locales", () => {
    expect(getLocalizedApiErrorMessage("FORBIDDEN", 403, "en")).toContain("permission");
    expect(getLocalizedApiErrorMessage("CONFLICT", 409, "en")).toContain("Refresh");
    expect(getLocalizedApiErrorMessage("FORBIDDEN", 403, "ar")).toContain("صلاحية");
    expect(getLocalizedApiErrorMessage("CONFLICT", 409, "ar")).toContain("حدّث");
  });

  it("localizes handover date conflicts and includes the actual dates", () => {
    const details = {
      handoverDate: "2026-11-05",
      projectEndDate: "2026-11-04",
    };

    const english = getLocalizedApiErrorMessage("HANDOVER_DATE_AFTER_PROJECT_END", 400, "en", details);
    const arabic = getLocalizedApiErrorMessage("HANDOVER_DATE_AFTER_PROJECT_END", 400, "ar", details);

    expect(english).toContain("November 5, 2026");
    expect(english).toContain("November 4, 2026");
    expect(arabic).toContain("٥ نوفمبر ٢٠٢٦");
    expect(arabic).toContain("٤ نوفمبر ٢٠٢٦");
  });

  it("provides localized business feedback for the matching field", () => {
    setApiErrorLocale("en");
    const error = new ApiError(
      400,
      "Validation failed",
      "HANDOVER_DATE_AFTER_PROJECT_END",
      {
        errors: { EffectiveHandoverDate: ["The handover date is invalid."] },
        details: {
          handoverDate: "2026-11-05",
          projectEndDate: "2026-11-04",
        },
      },
    );

    expect(getApiFieldError(error, "effectiveHandoverDate")).toContain("November 5, 2026");
  });

  it("localizes standard backend field-validation messages", () => {
    setApiErrorLocale("ar");
    const required = new ApiError(400, "Validation failed", "VALIDATION_ERROR", {
      errors: { Name: ["The Name field is required."] },
    });
    const minimum = new ApiError(400, "Validation failed", "VALIDATION_ERROR", {
      errors: { Budget: ["Budget must be greater than or equal to 0."] },
    });

    expect(getApiFieldError(required, "name")).toBe("حقل الاسم مطلوب.");
    expect(getApiFieldError(minimum, "budget")).toContain("الميزانية");
    expect(getApiFieldError(minimum, "budget")).toContain("0");
    setApiErrorLocale("en");
  });

  it("localizes resource field errors in English and Arabic", () => {
    const error = new ApiError(400, "Validation failed", "VALIDATION_ERROR", {
      errors: {
        PrimaryOrganizationalUnitId: ["The PrimaryOrganizationalUnitId field is required."],
      },
    });

    setApiErrorLocale("en");
    expect(getApiFieldError(error, "primaryOrganizationalUnitId")).toBe("Organizational unit is required.");
    setApiErrorLocale("ar");
    expect(getApiFieldError(error, "primaryOrganizationalUnitId")).toBe("حقل الوحدة التنظيمية مطلوب.");
    setApiErrorLocale("en");
  });

  it("uses safe server validation detail when available and localizes recognized rules", () => {
    const message = "The project end date must be on or after the project start date.";

    expect(getLocalizedApiErrorMessage("PROJECT_DATE_INVALID", 400, "en", undefined, message)).toBe(message);
    expect(getLocalizedApiErrorMessage("PROJECT_DATE_INVALID", 400, "ar", undefined, message))
      .toContain("تاريخ انتهاء المشروع");
  });

  it("replaces generic server validation summaries with actionable localized guidance", () => {
    expect(getLocalizedApiErrorMessage("VALIDATION_ERROR", 400, "en", undefined, "Validation failed."))
      .toContain("Correct the highlighted values");
    expect(getLocalizedApiErrorMessage("BUSINESS_VALIDATION_ERROR", 422, "ar", undefined, "Business validation failed."))
      .toContain("راجع التفاصيل وصحح المعلومات");
  });

  it("exposes localized field errors for form-level rendering in English and Arabic", () => {
    setApiErrorLocale("en");
    const error = new ApiError(400, "Some fields need attention.", "VALIDATION_ERROR", {
      errors: {
        Email: ["The Email field is required."],
        FirstName: ["The FirstName field is required."],
      },
    });

    expect(error.message).toContain("Email is required.");
    expect(error.message).toContain("First name is required.");
    expect(getApiFieldErrors(error)).toEqual({
      Email: ["Email is required."],
      FirstName: ["First name is required."],
    });
    setApiErrorLocale("ar");
    const arabicError = new ApiError(400, "تحتاج بعض الحقول إلى مراجعة.", "VALIDATION_ERROR", {
      errors: { FirstName: ["The FirstName field is required."] },
    });
    expect(arabicError.message).toContain("حقل الاسم الأول مطلوب.");
    expect(getApiFieldErrors(arabicError).FirstName?.[0]).toBe("حقل الاسم الأول مطلوب.");
    setApiErrorLocale("en");
  });

  it("localizes resource calendar and capacity codes to their associated fields", () => {
    const missingDefault = new ApiError(400, "Validation failed", "RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING", {
      errors: { BaseCalendarId: ["No calendar is configured."] },
    });
    const invalidHours = new ApiError(400, "Validation failed", "RESOURCE_HOURS_PER_DAY_INVALID", {
      errors: { HoursPerDay: ["HoursPerDay must be between 1 and 24."] },
    });

    setApiErrorLocale("en");
    expect(getApiFieldError(missingDefault, "baseCalendarId")).toContain("No configured tenant Standard");
    expect(getApiFieldError(missingDefault, "baseCalendarId")).toContain("select a Base Calendar");
    expect(getApiFieldError(invalidHours, "hoursPerDay")).toContain("from 1 through 24");

    setApiErrorLocale("ar");
    expect(getApiFieldError(missingDefault, "baseCalendarId")).toContain("لا يوجد تقويم قياسي");
    expect(getApiFieldError(invalidHours, "hoursPerDay")).toContain("من 1 إلى 24");
    setApiErrorLocale("en");
  });

  it("localizes between-range API validation messages", () => {
    expect(getLocalizedApiFieldError(undefined, 400, "ar", "HoursPerDay", "HoursPerDay must be between 1 and 24."))
      .toContain("بين 1 و24");
  });

  it("localizes unsupported document file type errors in both locales", () => {
    expect(getLocalizedApiErrorMessage("UNSUPPORTED_FILE_TYPE", 400, "en")).toBe(
      "Unsupported file type. Only Word (.doc, .docx), Excel (.xls, .xlsx), and PDF (.pdf) files are supported.",
    );
    expect(getLocalizedApiErrorMessage("UNSUPPORTED_FILE_TYPE", 400, "ar")).toBe(
      "نوع الملف غير مدعوم. يُسمح برفع ملفات Word ‏(.doc, .docx) وExcel ‏(.xls, .xlsx) وPDF ‏(.pdf) فقط.",
    );
  });

  it("uses a localized network message instead of exposing fetch errors", () => {
    expect(getLocalizedApiErrorMessage("NETWORK_ERROR", 0, "en")).toContain("service could not be reached");
    expect(getLocalizedApiErrorMessage("NETWORK_ERROR", 0, "ar")).toContain("تعذر الاتصال");
  });

  it("never interpolates server details into an unexpected server error message", () => {
    expect(getLocalizedApiErrorMessage("INTERNAL_ERROR", 500, "en", {
      exception: "SqlException: private diagnostic",
    })).not.toContain("private diagnostic");
  });
});
