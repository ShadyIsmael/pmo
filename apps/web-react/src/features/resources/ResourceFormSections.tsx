import * as React from "react";
import { useI18n } from "@i18n/messages";
import { getApiFieldError } from "@api/client";
import { Input } from "@components/ui/Input";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Label } from "@components/ui/Label";
import { Button } from "@components/ui/Button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Textarea } from "@components/ui/Textarea";
import type { ResourceBaseCalendarDto, ResourceReferenceDataDto, ResourceVendorLookupDto } from "./api/resources";
import type { WorkflowUserLookupDto } from "../projects/lib/project-lookups";
import { dialogValuesToResourceForm, RESOURCE_FIELD_LIMITS, validateResourceForm } from "./resource-form";

export type ResourceFormValues = {
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  organizationalUnitId: string;
  resourceType: string;
  status: "Active" | "Inactive";
  vendorId: string;
  baseCalendarId: string;
  hoursPerDay: string;
  hoursPerWeek: string;
  systemUserId: string;
  seniority: string;
  notes: string;
};

type OrganizationalUnitOption = { id: string; name: string; code?: string; isActive?: boolean };

export const emptyResourceFormValues: ResourceFormValues = {
  name: "",
  email: "",
  phone: "",
  jobTitle: "",
  organizationalUnitId: "",
  resourceType: "",
  status: "Active",
  vendorId: "",
  baseCalendarId: "",
  hoursPerDay: "",
  hoursPerWeek: "",
  systemUserId: "",
  seniority: "",
  notes: "",
};

function FormSection({ number, title, children }: { number: number; title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border bg-card/50 p-4 shadow-sm">
      <div className="mb-4 flex items-center gap-3">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">{number}</span>
        <h3 className="text-sm font-semibold text-foreground">{title}</h3>
      </div>
      {children}
    </section>
  );
}

function FieldLabel({ id, label, required = false, optionalText = "" }: { id: string; label: string; required?: boolean; optionalText?: string }) {
  return (
    <div className="flex items-center justify-between gap-2">
      <div className="flex items-center gap-1">
        <Label htmlFor={id}>{label}</Label>
        {required ? <span className="text-destructive" aria-hidden="true">*</span> : null}
      </div>
      {!required ? <span className="text-xs text-muted-foreground">{optionalText}</span> : null}
    </div>
  );
}

function FieldError({ id, children }: { id: string; children?: React.ReactNode }) {
  return children ? <p id={id} role="alert" className="text-xs text-destructive">{children}</p> : null;
}

export function ResourceFormSections({
  values,
  setValues,
  calendars = [],
  referenceData,
  referenceDataLoading = false,
  referenceDataError = false,
  onRetryReferenceData,
  units = [],
  users = [],
  vendors = [],
  calendarsLoading = false,
  calendarsError = false,
  onRetryCalendars,
  usersLoading = false,
  usersError = false,
  onRetryUsers,
  unitsError = false,
  onRetryUnits,
  vendorsLoading = false,
  vendorsError = false,
  onRetryVendors,
  readOnlyType = false,
  currentVendorId,
  submitted = false,
  apiError,
}: {
  values: ResourceFormValues;
  setValues: React.Dispatch<React.SetStateAction<ResourceFormValues>>;
  calendars?: ResourceBaseCalendarDto[];
  referenceData?: ResourceReferenceDataDto;
  referenceDataLoading?: boolean;
  referenceDataError?: boolean;
  onRetryReferenceData?: () => void;
  units?: OrganizationalUnitOption[];
  users?: WorkflowUserLookupDto[];
  vendors?: ResourceVendorLookupDto[];
  calendarsLoading?: boolean;
  calendarsError?: boolean;
  onRetryCalendars?: () => void;
  usersLoading?: boolean;
  usersError?: boolean;
  onRetryUsers?: () => void;
  unitsError?: boolean;
  onRetryUnits?: () => void;
  vendorsLoading?: boolean;
  vendorsError?: boolean;
  onRetryVendors?: () => void;
  readOnlyType?: boolean;
  currentVendorId?: string;
  submitted?: boolean;
  apiError?: unknown;
}) {
  const { t, locale } = useI18n();
  const directory = "resources.directory";
  const resourceTypes = referenceData?.resourceTypes ?? [];
  const seniorityLevels = referenceData?.seniorityLevels ?? [];
  const standardCalendar = calendars.find((calendar) => calendar.isDefault && calendar.isConfigured);
  const capacityCalendar = calendars.find((calendar) => calendar.id === values.baseCalendarId) ?? standardCalendar;
  const selectedType = resourceTypes.find((option) => option.code === values.resourceType);
  const isVendorResource = selectedType?.requiresVendor ?? Boolean(currentVendorId);
  const validation = validateResourceForm(dialogValuesToResourceForm(values, resourceTypes), capacityCalendar?.workingDaysPerWeek);
  const update = (field: keyof ResourceFormValues, value: string) => setValues((current) => ({ ...current, [field]: value }));
  const isStaffResource = selectedType?.code === "Staff";
  const selectedStaffUser = isStaffResource ? users.find((user) => user.id === values.systemUserId) : undefined;
  const selectedStaffName = selectedStaffUser?.fullName ?? (isStaffResource && values.systemUserId ? values.name : "");
  const selectedStaffEmail = selectedStaffUser?.email ?? (isStaffResource && values.systemUserId ? values.email : "");
  const staffUserOptions = users.map((user) => ({
    value: user.id,
    label: user.email ? user.fullName + " · " + user.email : user.fullName,
    keywords: user.email ? [user.email] : [],
  }));
  React.useEffect(() => {
    if (!isStaffResource || !selectedStaffUser) return;
    setValues((current) => {
      const email = selectedStaffUser.email ?? "";
      if (current.systemUserId !== selectedStaffUser.id || (current.name === selectedStaffUser.fullName && current.email === email)) return current;
      return { ...current, name: selectedStaffUser.fullName, email };
    });
  }, [isStaffResource, selectedStaffUser?.id, selectedStaffUser?.fullName, selectedStaffUser?.email, setValues]);
  const activeVendors = vendors.filter((vendor) => vendor.isActive || vendor.id === currentVendorId);
  const optionalText = t(`${directory}.optional`);
  const serverError = (field: string) => getApiFieldError(apiError, field);

  return (
    <div className="space-y-5">
      <FormSection number={1} title={t(`${directory}.basicInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-name" label={t(`${directory}.name`)} required />
            <Input id="resource-name" maxLength={RESOURCE_FIELD_LIMITS.name} value={isStaffResource ? selectedStaffName : values.name} onChange={(event) => update("name", event.target.value)} readOnly={isStaffResource} required={!isStaffResource} aria-invalid={Boolean(serverError("name")) || (submitted && (validation.name || validation.nameTooLong))} aria-describedby={serverError("name") || (submitted && (validation.name || validation.nameTooLong)) ? "resource-name-error" : undefined} />
            <FieldError id="resource-name-error">{serverError("name") ?? (submitted && validation.name ? t(`${directory}.nameRequired`) : submitted && validation.nameTooLong ? t(`${directory}.nameTooLong`) : undefined)}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-type" label={t(`${directory}.resourceType`)} required />
            {readOnlyType ? (
              <>
                <Input id="resource-type" readOnly aria-required="true" aria-invalid={Boolean(serverError("type"))} aria-describedby={serverError("type") ? "resource-type-error" : undefined} value={selectedType ? (locale === "ar" ? selectedType.nameAr : selectedType.nameEn) : values.resourceType} />
                <p className="text-xs text-muted-foreground">{t(`${directory}.typeReadOnlyHint`)}</p>
                <FieldError id="resource-type-error">{serverError("type")}</FieldError>
              </>
            ) : (
              <>
              <Select value={values.resourceType} onValueChange={(value) => {
                const next = resourceTypes.find((option) => option.code === value);
                setValues((current) => ({ ...current, resourceType: value, vendorId: next?.requiresVendor ? current.vendorId : "", systemUserId: next?.requiresVendor ? "" : current.systemUserId }));
              }}>
                <SelectTrigger id="resource-type" aria-required="true" aria-invalid={Boolean(serverError("type"))} aria-describedby={serverError("type") ? "resource-type-error" : undefined}><SelectValue /></SelectTrigger>
                <SelectContent>
                  {referenceDataLoading ? <SelectItem value="__loading__" disabled>{t("common.loading")}</SelectItem> : null}
                  {!referenceDataLoading && resourceTypes.length === 0 ? <SelectItem value="__empty__" disabled>{referenceDataError ? t("common.errorLoading") : t(`${directory}.noResourceTypes`)}</SelectItem> : null}
                  {resourceTypes.map((option) => <SelectItem key={option.code} value={option.code}>{locale === "ar" ? option.nameAr : option.nameEn}</SelectItem>)}
                </SelectContent>
              </Select>
              <FieldError id="resource-type-error">{serverError("type") ?? (submitted && validation.type ? t(`${directory}.typeRequired`) : undefined)}</FieldError>
              {referenceDataError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t(`${directory}.referenceDataFailed`)}</span>{onRetryReferenceData ? <Button type="button" size="sm" variant="outline" onClick={onRetryReferenceData}>{t("common.retry")}</Button> : null}</div> : null}
              </>
            )}
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-status" label={t(`${directory}.status`)} required />
            <Select value={values.status} onValueChange={(value) => update("status", value === "Inactive" ? "Inactive" : "Active")}>
              <SelectTrigger id="resource-status" aria-required="true" aria-invalid={Boolean(serverError("status"))} aria-describedby={serverError("status") ? "resource-status-error" : undefined}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">{t(`${directory}.active`)}</SelectItem>
                <SelectItem value="Inactive">{t(`${directory}.inactive`)}</SelectItem>
              </SelectContent>
            </Select>
            <FieldError id="resource-status-error">{serverError("status")}</FieldError>
          </div>
        </div>
      </FormSection>

      <FormSection number={2} title={t(`${directory}.professionalInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-email" label={t(`${directory}.email`)} optionalText={optionalText} />
            <Input id="resource-email" type="email" maxLength={RESOURCE_FIELD_LIMITS.email} value={isStaffResource ? selectedStaffEmail : values.email} onChange={(event) => update("email", event.target.value)} readOnly={isStaffResource} aria-invalid={Boolean(serverError("email")) || (submitted && (validation.email || validation.emailTooLong))} aria-describedby={serverError("email") || (submitted && (validation.email || validation.emailTooLong)) ? "resource-email-error" : undefined} />
            <FieldError id="resource-email-error">{serverError("email") ?? (submitted && validation.emailTooLong ? t(`${directory}.emailTooLong`) : submitted && validation.email ? t(`${directory}.emailInvalid`) : undefined)}</FieldError>
            {isStaffResource ? <p className="text-xs text-muted-foreground">{t(`${directory}.staffIdentityHint`)}</p> : null}
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-phone" label={t(`${directory}.phone`)} optionalText={optionalText} />
            <Input id="resource-phone" type="tel" maxLength={RESOURCE_FIELD_LIMITS.phone} value={values.phone} onChange={(event) => update("phone", event.target.value)} aria-invalid={Boolean(serverError("phone")) || (submitted && validation.phoneTooLong)} aria-describedby={serverError("phone") || (submitted && validation.phoneTooLong) ? "resource-phone-error" : undefined} />
            <FieldError id="resource-phone-error">{serverError("phone") ?? (submitted && validation.phoneTooLong ? t(`${directory}.phoneTooLong`) : undefined)}</FieldError>
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <FieldLabel id="resource-job-title" label={t(`${directory}.jobTitle`)} optionalText={optionalText} />
            <Input id="resource-job-title" maxLength={RESOURCE_FIELD_LIMITS.jobTitle} value={values.jobTitle} onChange={(event) => update("jobTitle", event.target.value)} aria-invalid={Boolean(serverError("jobTitle")) || (submitted && validation.jobTitleTooLong)} aria-describedby={serverError("jobTitle") || (submitted && validation.jobTitleTooLong) ? "resource-job-title-error" : undefined} />
            <FieldError id="resource-job-title-error">{serverError("jobTitle") ?? (submitted && validation.jobTitleTooLong ? t(`${directory}.jobTitleTooLong`) : undefined)}</FieldError>
          </div>
        </div>
      </FormSection>

      <FormSection number={3} title={t(`${directory}.associationInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-base-calendar" label={t(`${directory}.baseCalendar`)} optionalText={optionalText} />
            <Select value={values.baseCalendarId || "__inherit__"} onValueChange={(value) => update("baseCalendarId", value === "__inherit__" ? "" : value)}>
              <SelectTrigger id="resource-base-calendar" aria-invalid={Boolean(serverError("baseCalendarId")) || (submitted && validation.baseCalendar)} aria-describedby={serverError("baseCalendarId") || (submitted && validation.baseCalendar) ? "resource-base-calendar-error" : undefined}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__inherit__">{standardCalendar ? `${t(`${directory}.inheritCalendar`)} · ${standardCalendar.name}` : t(`${directory}.inheritCalendar`)}</SelectItem>
                {calendarsLoading ? <SelectItem value="__loading__" disabled>{t(`${directory}.loadingCalendars`)}</SelectItem> : null}
                {!calendarsLoading && !calendarsError && calendars.length === 0 ? <SelectItem value="__empty__" disabled>{t(`${directory}.noCalendars`)}</SelectItem> : null}
                {calendars.map((calendar) => <SelectItem key={calendar.id} value={calendar.id} disabled={!calendar.isConfigured}>{calendar.name}{calendar.isDefault ? ` · ${t(`${directory}.default`)}` : ""}{!calendar.isConfigured ? ` · ${t(`${directory}.calendarNeedsConfiguration`)}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{standardCalendar ? t(`${directory}.baseCalendarHint`) : t(`${directory}.noStandardCalendarAvailable`)}</p>
            {calendarsError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t(`${directory}.calendarsLoadError`)}</span>{onRetryCalendars ? <Button type="button" size="sm" variant="outline" onClick={onRetryCalendars}>{t("common.retry")}</Button> : null}</div> : null}
            <FieldError id="resource-base-calendar-error">{serverError("baseCalendarId") ?? (submitted && validation.baseCalendar ? t(`${directory}.baseCalendarInvalid`) : undefined)}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-hours-per-day" label={t(`${directory}.hoursPerDay`)} required />
            <Input id="resource-hours-per-day" type="text" inputMode="decimal" autoComplete="off" value={values.hoursPerDay} onChange={(event) => update("hoursPerDay", event.target.value)} aria-required="true" aria-invalid={Boolean(serverError("hoursPerDay")) || (submitted && validation.hoursPerDay)} aria-describedby={serverError("hoursPerDay") || (submitted && validation.hoursPerDay) ? "resource-hours-per-day-error" : undefined} />
            <FieldError id="resource-hours-per-day-error">{serverError("hoursPerDay") ?? (submitted && validation.hoursPerDay ? t(`${directory}.hoursPerDayInvalid`) : undefined)}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-hours-per-week" label={t(`${directory}.hoursPerWeek`)} optionalText={optionalText} />
            <Input id="resource-hours-per-week" type="text" inputMode="decimal" autoComplete="off" value={values.hoursPerWeek} onChange={(event) => update("hoursPerWeek", event.target.value)} aria-invalid={Boolean(serverError("hoursPerWeek")) || (submitted && (validation.hoursPerWeek || validation.capacityMismatch))} aria-describedby={serverError("hoursPerWeek") || (submitted && (validation.hoursPerWeek || validation.capacityMismatch)) ? "resource-hours-per-week-error" : undefined} />
            <FieldError id="resource-hours-per-week-error">{serverError("hoursPerWeek") ?? (submitted && validation.hoursPerWeek ? t(`${directory}.hoursPerWeekInvalid`) : submitted && validation.capacityMismatch ? t(`${directory}.capacityMismatch`) : undefined)}</FieldError>
          </div>
          <p className="text-xs text-muted-foreground sm:col-span-2">{t(`${directory}.workingCapacityHint`)}</p>
          <div className="space-y-1.5">
            <FieldLabel id="resource-organizational-unit" label={t(`${directory}.organizationalUnit`)} optionalText={optionalText} />
            <Select value={values.organizationalUnitId || "__none__"} onValueChange={(value) => update("organizationalUnitId", value === "__none__" ? "" : value)}>
              <SelectTrigger id="resource-organizational-unit" aria-invalid={Boolean(serverError("primaryOrganizationalUnitId"))} aria-describedby={serverError("primaryOrganizationalUnitId") ? "resource-organizational-unit-error" : undefined}><SelectValue placeholder={t(`${directory}.selectOrganizationalUnit`)} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t(`${directory}.none`)}</SelectItem>
                {units.filter((unit) => unit.isActive !== false || unit.id === values.organizationalUnitId).map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.name}{unit.code ? ` · ${unit.code}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
            <FieldError id="resource-organizational-unit-error">{serverError("primaryOrganizationalUnitId")}</FieldError>
            {unitsError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("common.errorLoading")}</span>{onRetryUnits ? <Button type="button" size="sm" variant="outline" onClick={onRetryUnits}>{t("common.retry")}</Button> : null}</div> : null}
          </div>
          {isVendorResource ? (
            <div className="space-y-1.5">
              <FieldLabel id="resource-vendor" label={t(`${directory}.vendor`)} required />
              <Select value={values.vendorId} onValueChange={(value) => update("vendorId", value)}>
                <SelectTrigger id="resource-vendor" aria-required="true" aria-invalid={Boolean(serverError("vendorId")) || (submitted && validation.vendor)} aria-describedby={serverError("vendorId") || (submitted && validation.vendor) ? "resource-vendor-error" : undefined}><SelectValue placeholder={t(`${directory}.selectVendor`)} /></SelectTrigger>
                <SelectContent>
                  {vendorsLoading ? <SelectItem value="__loading__" disabled>{t("common.loading")}</SelectItem> : null}
                  {!vendorsLoading && !vendorsError && activeVendors.length === 0 ? <SelectItem value="__empty__" disabled>{t(`${directory}.noVendors`)}</SelectItem> : null}
                  {activeVendors.map((vendor) => <SelectItem key={vendor.id} value={vendor.id}>{vendor.name}{vendor.code ? ` · ${vendor.code}` : ""}</SelectItem>)}
                </SelectContent>
              </Select>
              <FieldError id="resource-vendor-error">{serverError("vendorId") ?? (submitted && validation.vendor ? t(`${directory}.vendorRequired`) : undefined)}</FieldError>
              {vendorsError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t(`${directory}.vendorsLoadError`)}</span>{onRetryVendors ? <Button type="button" size="sm" variant="outline" onClick={onRetryVendors}>{t("common.retry")}</Button> : null}</div> : null}
            </div>
          ) : (
            <>
              <div className="space-y-1.5">
                <FieldLabel id="resource-system-user" label={t(`${directory}.systemUser`)} required={isStaffResource} optionalText={optionalText} />
                {isStaffResource ? (
                  <SearchableSelect
                    aria-label={t(`${directory}.systemUser`)}
                    aria-invalid={Boolean(serverError("userId")) || (submitted && validation.user)}
                    aria-describedby={serverError("userId") || (submitted && validation.user) ? "resource-system-user-error" : undefined}
                    options={staffUserOptions}
                    value={values.systemUserId || undefined}
                    onValueChange={(userId) => {
                      const user = users.find((candidate) => candidate.id === userId);
                      setValues((current) => ({ ...current, systemUserId: userId, name: user?.fullName ?? "", email: user?.email ?? "" }));
                    }}
                    placeholder={t(`${directory}.selectStaffUser`)}
                    searchPlaceholder={t(`${directory}.searchStaffUsers`)}
                    emptyText={users.length ? t(`${directory}.noMatchingStaffUsers`) : t(`${directory}.noEligibleStaffUsers`)}
                    loading={usersLoading}
                    loadingText={t(`${directory}.loadingUsers`)}
                    disabled={usersError}
                  />
                ) : (
                  <Select value={values.systemUserId || "__none__"} onValueChange={(value) => update("systemUserId", value === "__none__" ? "" : value)}>
                    <SelectTrigger id="resource-system-user" aria-invalid={Boolean(serverError("userId"))} aria-describedby={serverError("userId") ? "resource-system-user-error" : undefined}><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none__">{t(`${directory}.noSystemUser`)}</SelectItem>
                      {usersLoading ? <SelectItem value="__loading__" disabled>{t(`${directory}.loadingUsers`)}</SelectItem> : null}
                      {users.map((user) => <SelectItem key={user.id} value={user.id}>{user.fullName}</SelectItem>)}
                    </SelectContent>
                  </Select>
                )}
                <FieldError id="resource-system-user-error">{serverError("userId") ?? (submitted && validation.user ? t(`${directory}.systemUserRequired`) : undefined)}</FieldError>
                {usersError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("common.errorLoading")}</span>{onRetryUsers ? <Button type="button" size="sm" variant="outline" onClick={onRetryUsers}>{t("common.retry")}</Button> : null}</div> : null}
                <p className="text-xs text-muted-foreground">{t(isStaffResource ? `${directory}.staffUserHint` : `${directory}.systemUserHint`)}</p>
              </div>
              <div className="space-y-1.5">
                <FieldLabel id="resource-seniority" label={t(`${directory}.seniority`)} optionalText={optionalText} />
                <Select value={values.seniority || "__none__"} onValueChange={(value) => update("seniority", value === "__none__" ? "" : value)}>
                  <SelectTrigger id="resource-seniority" aria-invalid={Boolean(serverError("seniority"))} aria-describedby={serverError("seniority") ? "resource-seniority-error" : undefined}><SelectValue placeholder={optionalText} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">{optionalText}</SelectItem>
                    {values.seniority && !seniorityLevels.some((level) => level.code === values.seniority) ? <SelectItem value={values.seniority}>{values.seniority} · {t(`${directory}.legacyValue`)}</SelectItem> : null}
                    {seniorityLevels.map((level) => <SelectItem key={level.code} value={level.code}>{locale === "ar" ? level.nameAr : level.nameEn}</SelectItem>)}
                  </SelectContent>
                </Select>
                <FieldError id="resource-seniority-error">{serverError("seniority")}</FieldError>
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <FieldLabel id="resource-notes" label={t(`${directory}.notes`)} optionalText={optionalText} />
                <Textarea id="resource-notes" maxLength={RESOURCE_FIELD_LIMITS.notes} value={values.notes} onChange={(event) => update("notes", event.target.value)} rows={3} aria-invalid={Boolean(serverError("notes")) || (submitted && validation.notesTooLong)} aria-describedby={serverError("notes") || (submitted && validation.notesTooLong) ? "resource-notes-error" : undefined} />
                <FieldError id="resource-notes-error">{serverError("notes") ?? (submitted && validation.notesTooLong ? t(`${directory}.notesTooLong`) : undefined)}</FieldError>
              </div>
            </>
          )}
        </div>
      </FormSection>
    </div>
  );
}
