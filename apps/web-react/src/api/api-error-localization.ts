import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";

export type ApiErrorLocale = "en" | "ar";

const localizedMessages = {
  en: enMessages.apiErrors,
  ar: arMessages.apiErrors,
} as const;

const codeMessageKeys: Record<string, keyof typeof enMessages.apiErrors> = {
  UNSUPPORTED_FILE_TYPE: "unsupportedFileType",
  DOCUMENT_TEMPLATE_FILE_TYPE_MISMATCH: "documentTemplateFileTypeMismatch",
  VALIDATION_ERROR: "validation",
  BUSINESS_VALIDATION_ERROR: "businessValidation",
  RESOURCE_WORKING_CALENDAR_UNAVAILABLE: "resourceWorkingCalendarUnavailable",
  RESOURCE_WORKING_CALENDAR_INVALID: "resourceWorkingCalendarInvalid",
  RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING: "resourceWorkingCalendarDefaultMissing",
  RESOURCE_WORKING_CALENDAR_DEFAULT_INVALID: "resourceWorkingCalendarDefaultInvalid",
  RESOURCE_WORKING_CALENDAR_REQUIRED: "resourceWorkingCalendarRequired",
  RESOURCE_HOURS_PER_DAY_INVALID: "resourceHoursPerDayInvalid",
  RESOURCE_HOURS_PER_WEEK_INVALID: "resourceHoursPerWeekInvalid",
  RESOURCE_CAPACITY_EXCEEDS_CALENDAR: "resourceCapacityExceedsCalendar",
  RESOURCE_CAPACITY_DAILY_WEEKLY_MISMATCH: "resourceCapacityDailyWeeklyMismatch",
  HANDOVER_DATE_AFTER_PROJECT_END: "handoverDateAfterProjectEnd",
  HANDOVER_REQUEST_ALREADY_PENDING: "handoverRequestAlreadyPending",
  HANDOVER_REQUEST_ALREADY_SCHEDULED: "handoverRequestAlreadyScheduled",
  PROJECT_ALREADY_ASSIGNED: "projectAlreadyAssigned",
  PROJECT_ASSIGNMENT_RECONCILIATION_REQUIRED: "projectAssignmentReconciliationRequired",
  UNAUTHENTICATED: "unauthenticated",
  FORBIDDEN: "forbidden",
  NOT_FOUND: "notFound",
  CONFLICT: "conflict",
  RATE_LIMITED: "rateLimited",
  INTERNAL_ERROR: "internal",
  NETWORK_ERROR: "network",
};

const resourceFieldCodeMessageKeys: Record<string, keyof typeof enMessages.apiErrors> = {
  RESOURCE_WORKING_CALENDAR_UNAVAILABLE: "resourceWorkingCalendarUnavailable",
  RESOURCE_WORKING_CALENDAR_INVALID: "resourceWorkingCalendarInvalid",
  RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING: "resourceWorkingCalendarDefaultMissing",
  RESOURCE_WORKING_CALENDAR_DEFAULT_INVALID: "resourceWorkingCalendarDefaultInvalid",
  RESOURCE_WORKING_CALENDAR_REQUIRED: "resourceWorkingCalendarRequired",
  RESOURCE_HOURS_PER_DAY_INVALID: "resourceHoursPerDayInvalid",
  RESOURCE_HOURS_PER_WEEK_INVALID: "resourceHoursPerWeekInvalid",
  RESOURCE_CAPACITY_EXCEEDS_CALENDAR: "resourceCapacityExceedsCalendar",
  RESOURCE_CAPACITY_DAILY_WEEKLY_MISMATCH: "resourceCapacityDailyWeeklyMismatch",
};

const localizedFieldLabels: Record<string, { en: string; ar: string }> = {
  amount: { en: "Amount", ar: "المبلغ" },
  addressline1: { en: "Address line 1", ar: "العنوان ١" },
  addressline2: { en: "Address line 2", ar: "العنوان ٢" },
  basecalendarid: { en: "Base calendar", ar: "التقويم الأساسي" },
  businessunitid: { en: "Business unit", ar: "وحدة الأعمال" },
  budget: { en: "Budget", ar: "الميزانية" },
  code: { en: "Code", ar: "الرمز" },
  currency: { en: "Currency", ar: "العملة" },
  category: { en: "Category", ar: "الفئة" },
  description: { en: "Description", ar: "الوصف" },
  displayorder: { en: "Display order", ar: "ترتيب العرض" },
  email: { en: "Email", ar: "البريد الإلكتروني" },
  firstname: { en: "First name", ar: "الاسم الأول" },
  lastname: { en: "Last name", ar: "اسم العائلة" },
  namear: { en: "Arabic name", ar: "الاسم بالعربية" },
  nameen: { en: "English name", ar: "الاسم بالإنجليزية" },
  ownerid: { en: "Owner", ar: "المالك" },
  phone: { en: "Phone", ar: "الهاتف" },
  permissionids: { en: "Permissions", ar: "الصلاحيات" },
  permissions: { en: "Permissions", ar: "الصلاحيات" },
  programmanagerid: { en: "Program manager", ar: "مدير البرنامج" },
  roles: { en: "Roles", ar: "الأدوار" },
  roleids: { en: "Roles", ar: "الأدوار" },
  website: { en: "Website", ar: "الموقع الإلكتروني" },
  enddate: { en: "End date", ar: "تاريخ الانتهاء" },
  effectivedate: { en: "Effective date", ar: "التاريخ المحدد" },
  effectivehandoverdate: { en: "Handover date", ar: "تاريخ التسليم" },
  endtime: { en: "End time", ar: "وقت الانتهاء" },
  handoverdate: { en: "Handover date", ar: "تاريخ التسليم" },
  hoursperday: { en: "Hours per day", ar: "الساعات في اليوم" },
  hoursperweek: { en: "Hours per week", ar: "الساعات في الأسبوع" },
  jobtitle: { en: "Job title", ar: "المسمى الوظيفي" },
  locationid: { en: "Location", ar: "الموقع" },
  name: { en: "Name", ar: "الاسم" },
  notes: { en: "Notes", ar: "ملاحظات" },
  organizerid: { en: "Meeting organizer", ar: "منظم الاجتماع" },
  paymentreference: { en: "Payment reference", ar: "مرجع الدفع" },
  portfolioid: { en: "Portfolio", ar: "المحفظة" },
  primaryorganizationalunitid: { en: "Organizational unit", ar: "الوحدة التنظيمية" },
  projectid: { en: "Project", ar: "المشروع" },
  projectenddate: { en: "Project end date", ar: "تاريخ انتهاء المشروع" },
  projectcode: { en: "Project code", ar: "رمز المشروع" },
  projectmanagerid: { en: "Project manager", ar: "مدير المشروع" },
  projectname: { en: "Project name", ar: "اسم المشروع" },
  purpose: { en: "Purpose", ar: "الغرض" },
  resourcetype: { en: "Resource type", ar: "نوع المورد" },
  seniority: { en: "Seniority", ar: "الأقدمية" },
  status: { en: "Status", ar: "الحالة" },
  starttime: { en: "Start time", ar: "وقت البدء" },
  startdate: { en: "Start date", ar: "تاريخ البدء" },
  title: { en: "Title", ar: "العنوان" },
  type: { en: "Type", ar: "النوع" },
  userid: { en: "User", ar: "المستخدم" },
  vendorid: { en: "Vendor", ar: "المورد" },
  departmentid: { en: "Department", ar: "القسم" },
};

function messageKeyForStatus(status: number): keyof typeof enMessages.apiErrors {
  if (status === 400) return "validation";
  if (status === 401) return "unauthenticated";
  if (status === 403) return "forbidden";
  if (status === 404) return "notFound";
  if (status === 409) return "conflict";
  if (status === 422) return "businessValidation";
  if (status === 429) return "rateLimited";
  if (status >= 500) return "internal";
  return "requestFailed";
}

function formatFieldLabel(fieldName: string, locale: ApiErrorLocale): string {
  const normalized = fieldName.replace(/[^a-z]/gi, "").toLowerCase();
  const known = localizedFieldLabels[normalized];
  if (known) return known[locale];
  const words = fieldName.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/[_-]/g, " ").trim();
  return locale === "en" ? words || "This field" : "هذا الحقل";
}

function localizeFieldMessage(
  fieldName: string,
  message: string,
  locale: ApiErrorLocale,
): string | undefined {
  const field = formatFieldLabel(fieldName, locale);
  const normalized = message.toLowerCase();
  const required = /\b(is required|required field|must not be empty|must not be null|cannot be empty)\b/.test(normalized);
  if (required) return localizedMessages[locale].fieldRequired.replace("{field}", field);

  if (/\b(?:already exists|already in use|duplicate|not unique)\b/.test(normalized))
    return localizedMessages[locale].fieldDuplicate.replace("{field}", field);

  if (/\b(valid email|email address)\b/.test(normalized) && /\b(valid|invalid|must be|format)\b/.test(normalized))
    return localizedMessages[locale].fieldInvalidEmail.replace("{field}", field);

  const minLength = /(?:minimum length|min(?:imum)?\s*(?:length)?\s*(?:of)?\s*['"]?)(\d+)/i.exec(message);
  if (minLength)
    return localizedMessages[locale].fieldMinLength
      .replace("{field}", field)
      .replace("{length}", minLength[1]);

  const maxLength = /(?:maximum length|max(?:imum)?\s*(?:length)?\s*(?:of)?\s*['"]?)(\d+)/i.exec(message);
  if (maxLength)
    return localizedMessages[locale].fieldMaxLength
      .replace("{field}", field)
      .replace("{length}", maxLength[1]);

  const between = /\bbetween\s*['"]?(-?\d+(?:\.\d+)?)['"]?\s+and\s+['"]?(-?\d+(?:\.\d+)?)/i.exec(message);
  if (between)
    return localizedMessages[locale].fieldBetween
      .replace("{field}", field)
      .replace("{minimum}", between[1])
      .replace("{maximum}", between[2]);

  const minimum = /\b(?:at least|greater than or equal to|minimum(?: value)?(?: of)?)\s*['"]?(-?\d+(?:\.\d+)?)/i.exec(message);
  if (minimum)
    return localizedMessages[locale].fieldMinimum
      .replace("{field}", field)
      .replace("{minimum}", minimum[1]);

  const maximum = /\b(?:at most|less than or equal to|maximum(?: value)?(?: of)?)\s*['"]?(-?\d+(?:\.\d+)?)/i.exec(message);
  if (maximum)
    return localizedMessages[locale].fieldMaximum
      .replace("{field}", field)
      .replace("{maximum}", maximum[1]);

  if (/\b(?:before|after|on or before|on or after)\b/.test(normalized))
    return localizedMessages[locale].fieldDateOrder.replace("{field}", field);

  return undefined;
}

function isSafeServerMessage(message: string): boolean {
  return message.trim().length > 0
    && !/(?:SqlException|SQL(?: Server|STATE| error)|DbException|Microsoft\.EntityFrameworkCore|System\.[A-Za-z]+Exception|stack trace|Invalid (?:object|column) name|Incorrect syntax near|password\s*[:=]|token\s*[:=]|connection\s*string\s*[:=])/i.test(message);
}

function getFieldMessageParts(message: string): [string, string] | undefined {
  const explicit = /^The\s+field\s+([A-Za-z][A-Za-z0-9_.]*)\s+(.+)$/i.exec(message);
  if (explicit) return [explicit[1], explicit[2]];
  const match = /^(.+?)\s+(is required|required|must(?: not)?\b|cannot\b|should\b|already exists\b|already in use\b|is invalid\b|does not\b)(.*)$/i.exec(message);
  if (!match) return undefined;
  const field = match[1].replace(/^The\s+/i, "").replace(/\s+field$/i, "").trim();
  return field ? [field, `${match[2]}${match[3]}`] : undefined;
}

export function getLocalizedApiFieldError(
  code: string | undefined,
  status: number,
  locale: ApiErrorLocale,
  fieldName: string,
  serverMessage: string,
): string {
  if (status >= 500) return localizedMessages[locale].internal;
  const codeMessageKey = code && resourceFieldCodeMessageKeys[code];
  if (codeMessageKey) return localizedMessages[locale][codeMessageKey];
  const translated = localizeFieldMessage(fieldName, serverMessage, locale);
  if (translated) return translated;
  if (locale === "en" && isSafeServerMessage(serverMessage))
    return serverMessage;
  return localizedMessages[locale].fieldValidation.replace("{field}", formatFieldLabel(fieldName, locale));
}

function isGenericServerMessage(message: string): boolean {
  return /^(?:validation failed|business validation failed|request failed|the request failed|an error occurred|something went wrong|forbidden|unauthorized|access denied)[.! ]*$/i.test(message.trim());
}

const localizedServerMessages = {
  en: {
    ...enMessages.apiErrors,
    handoverViewPermissionDenied: enMessages.managerHandover.viewPermissionRequired,
    incomingManagerActionRequired: enMessages.managerHandover.approvePermissionHint,
    handoverReadOnlyActionUnavailable: enMessages.managerHandover.readOnlyHandover,
    handoverProjectManagerMissing: enMessages.managerHandover.noCurrentManager,
    handoverProjectManagerAlreadyAssigned: enMessages.managerHandover.managerChangeViaHandover,
    handoverDateAfterProjectEndMessage: enMessages.managerHandover.handoverDateAfterProjectEnd,
  },
  ar: {
    ...arMessages.apiErrors,
    handoverViewPermissionDenied: arMessages.managerHandover.viewPermissionRequired,
    incomingManagerActionRequired: arMessages.managerHandover.approvePermissionHint,
    handoverReadOnlyActionUnavailable: arMessages.managerHandover.readOnlyHandover,
    handoverProjectManagerMissing: arMessages.managerHandover.noCurrentManager,
    handoverProjectManagerAlreadyAssigned: arMessages.managerHandover.managerChangeViaHandover,
    handoverDateAfterProjectEndMessage: arMessages.managerHandover.handoverDateAfterProjectEnd,
  },
} as const;

type LocalizedServerMessageKey = keyof typeof localizedServerMessages.en;

const serverMessageRules: Array<{ pattern: RegExp; key: LocalizedServerMessageKey }> = [
  { pattern: /you do not have permission to view handovers/i, key: "handoverViewPermissionDenied" },
  { pattern: /you do not have permission to initiate this handover/i, key: "handoverInitiatePermissionDenied" },
  { pattern: /only the current project manager can request a handover/i, key: "currentProjectManagerRequired" },
  { pattern: /only the current program manager can request a handover/i, key: "currentProgramManagerRequired" },
  { pattern: /only the current portfolio manager can request a handover/i, key: "currentPortfolioManagerRequired" },
  { pattern: /only the selected new manager can accept this request/i, key: "incomingManagerActionRequired" },
  { pattern: /only the selected new manager can reject this request/i, key: "incomingManagerActionRequired" },
  { pattern: /only the requester can cancel this request/i, key: "requesterCancelRequired" },
  { pattern: /an active management-category role is required to approve handovers/i, key: "managementRoleRequiredForHandoverApproval" },
  { pattern: /read-only management views cannot accept handovers/i, key: "handoverReadOnlyActionUnavailable" },
  { pattern: /read-only management views cannot change handover requests/i, key: "handoverReadOnlyActionUnavailable" },
  { pattern: /this project has no assigned manager/i, key: "handoverProjectManagerMissing" },
  { pattern: /this project already has a manager/i, key: "handoverProjectManagerAlreadyAssigned" },
  { pattern: /new project manager must be different/i, key: "newProjectManagerMustDiffer" },
  { pattern: /handover date cannot be after the project end date/i, key: "handoverDateAfterProjectEndMessage" },
  { pattern: /only the requester or a pmo administrator can cancel this change request/i, key: "changeRequestRequesterOrPmoAdminCancelRequired" },
  { pattern: /only the requester or an authorized manager can cancel this change request/i, key: "changeRequestRequesterOrAuthorizedManagerCancelRequired" },
  { pattern: /only an authorized reviewer can approve a change request/i, key: "changeRequestAuthorizedReviewerApproveRequired" },
  { pattern: /only an authorized reviewer can reject a change request/i, key: "changeRequestAuthorizedReviewerRejectRequired" },
  { pattern: /a change request requester cannot approve their own request/i, key: "changeRequestSelfApprovalDenied" },
  { pattern: /the authenticated user is not active in this tenant|the authenticated user is not an active user in this tenant/i, key: "tenantUserInactive" },
  { pattern: /this action is not available while using read-only perspective mode/i, key: "readOnlyPerspectiveUnavailable" },
  { pattern: /project is outside the current management scope|the project is outside the current management scope/i, key: "projectOutsideManagementScope" },
];

const viewPermissionMessageKeys: Record<string, LocalizedServerMessageKey> = {
  handovers: "handoverViewPermissionDenied",
  "change requests": "changeRequestsViewPermissionDenied",
  "status reports": "statusReportsViewPermissionDenied",
  snapshots: "snapshotsViewPermissionDenied",
  "audit logs": "auditLogsViewPermissionDenied",
  history: "auditLogsViewPermissionDenied",
};

function localizeServerMessage(
  message: string,
  locale: ApiErrorLocale,
): string | undefined {
  if (locale === "en") return message;
  const rule = serverMessageRules.find(({ pattern }) => pattern.test(message));
  if (rule) return localizedServerMessages[locale][rule.key];

  const viewMatch = /^you do not have permission to view\s+(.+?)[.! ]*$/i.exec(message.trim());
  const viewKey = viewMatch && viewPermissionMessageKeys[viewMatch[1].toLowerCase()];
  return viewKey ? localizedServerMessages[locale][viewKey] : undefined;
}

function formatParameter(value: unknown, locale: ApiErrorLocale): string {
  if (typeof value !== "string") return String(value ?? "");
  const dateParts = /^(\d{4})-(\d{2})-(\d{2})(?:T.*)?$/.exec(value);
  if (!dateParts) return value;
  const year = Number(dateParts[1]);
  const month = Number(dateParts[2]);
  const day = Number(dateParts[3]);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day)
    return value;
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "long",
    timeZone: "UTC",
    ...(locale === "ar" ? { numberingSystem: "arab" } : {}),
  }).format(date);
}

export function getLocalizedApiErrorMessage(
  code: string | undefined,
  status: number,
  locale: ApiErrorLocale,
  details?: unknown,
  serverMessage?: string,
): string {
  if (status >= 500)
    return localizedMessages[locale].internal;
  if (code === "DOCUMENT_TEMPLATE_FILE_TYPE_MISMATCH") {
    const parameters = typeof details === "object" && details !== null
      ? details as Record<string, unknown>
      : {};
    const expected = formatParameter(parameters[locale === "ar" ? "expectedTypeAr" : "expectedType"], locale);
    const detected = formatParameter(parameters[locale === "ar" ? "detectedTypeAr" : "detectedType"], locale);
    return localizedMessages[locale].documentTemplateFileTypeMismatch
      .replace("{detectedType}", detected)
      .replace("{expectedType}", expected);
  }
  const codeKey = code && codeMessageKeys[code];
  const isGenericCode = !codeKey || [
    "VALIDATION_ERROR",
    "BUSINESS_VALIDATION_ERROR",
    "CONFLICT",
    "REQUEST_FAILED",
    "FORBIDDEN",
  ].includes(code);
  if (serverMessage && isGenericCode) {
    const trimmedMessage = serverMessage.trim();
    if (!isGenericServerMessage(trimmedMessage) && isSafeServerMessage(trimmedMessage)) {
      const serverTranslated = localizeServerMessage(trimmedMessage, locale);
      if (serverTranslated) return serverTranslated;
      if (locale === "en") return trimmedMessage;
    }
    const fieldParts = getFieldMessageParts(trimmedMessage);
    const translated = fieldParts && localizeFieldMessage(fieldParts[0], fieldParts[1], locale);
    if (translated) return translated;
  }
  const messageKey = codeKey || messageKeyForStatus(status);
  const template = localizedMessages[locale][messageKey];
  const parameters = typeof details === "object" && details !== null
    ? details as Record<string, unknown>
    : {};
  return template.replace(/\{(\w+)\}/g, (_match, key: string) =>
    formatParameter(parameters[key], locale));
}
