import * as React from "react";
import { useI18n } from "@i18n/messages";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Textarea } from "@components/ui/Textarea";
import type { ResourceBaseCalendarDto, ResourceReferenceDataDto } from "./api/resources";
import type { UserLookupDto } from "../projects/lib/project-lookups";
import type { VendorDto } from "../vendors/api/vendors";
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
  units = [],
  users = [],
  vendors = [],
  calendarsLoading = false,
  usersLoading = false,
  vendorsLoading = false,
  readOnlyType = false,
  currentVendorId,
  submitted = false,
}: {
  values: ResourceFormValues;
  setValues: React.Dispatch<React.SetStateAction<ResourceFormValues>>;
  calendars?: ResourceBaseCalendarDto[];
  referenceData?: ResourceReferenceDataDto;
  referenceDataLoading?: boolean;
  referenceDataError?: boolean;
  units?: OrganizationalUnitOption[];
  users?: UserLookupDto[];
  vendors?: VendorDto[];
  calendarsLoading?: boolean;
  usersLoading?: boolean;
  vendorsLoading?: boolean;
  readOnlyType?: boolean;
  currentVendorId?: string;
  submitted?: boolean;
}) {
  const { t, locale } = useI18n();
  const directory = "resources.directory";
  const resourceTypes = referenceData?.resourceTypes ?? [];
  const seniorityLevels = referenceData?.seniorityLevels ?? [];
  const selectedType = resourceTypes.find((option) => option.code === values.resourceType);
  const isVendorResource = selectedType?.requiresVendor ?? Boolean(currentVendorId);
  const validation = validateResourceForm(dialogValuesToResourceForm(values, resourceTypes));
  const update = (field: keyof ResourceFormValues, value: string) => setValues((current) => ({ ...current, [field]: value }));
  const activeVendors = vendors.filter((vendor) => vendor.isActive || vendor.id === currentVendorId);
  const optionalText = t(`${directory}.optional`);

  return (
    <div className="space-y-5">
      <FormSection number={1} title={t(`${directory}.basicInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-name" label={t(`${directory}.name`)} required />
            <Input id="resource-name" maxLength={RESOURCE_FIELD_LIMITS.name} value={values.name} onChange={(event) => update("name", event.target.value)} required aria-invalid={submitted && (validation.name || validation.nameTooLong) ? true : undefined} aria-describedby={submitted && (validation.name || validation.nameTooLong) ? "resource-name-error" : undefined} />
            <FieldError id="resource-name-error">{submitted && validation.name ? t(`${directory}.nameRequired`) : submitted && validation.nameTooLong ? t(`${directory}.nameTooLong`) : undefined}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-type" label={t(`${directory}.resourceType`)} required />
            {readOnlyType ? (
              <>
                <Input id="resource-type" readOnly aria-required="true" value={selectedType ? (locale === "ar" ? selectedType.nameAr : selectedType.nameEn) : values.resourceType} />
                <p className="text-xs text-muted-foreground">{t(`${directory}.typeReadOnlyHint`)}</p>
              </>
            ) : (
              <>
              <Select value={values.resourceType} onValueChange={(value) => {
                const next = resourceTypes.find((option) => option.code === value);
                setValues((current) => ({ ...current, resourceType: value, vendorId: next?.requiresVendor ? current.vendorId : "", systemUserId: next?.requiresVendor ? "" : current.systemUserId }));
              }}>
                <SelectTrigger id="resource-type" aria-required="true"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {referenceDataLoading ? <SelectItem value="__loading__" disabled>{t("common.loading")}</SelectItem> : null}
                  {!referenceDataLoading && resourceTypes.length === 0 ? <SelectItem value="__empty__" disabled>{referenceDataError ? t("common.errorLoading") : t(`${directory}.noResourceTypes`)}</SelectItem> : null}
                  {resourceTypes.map((option) => <SelectItem key={option.code} value={option.code}>{locale === "ar" ? option.nameAr : option.nameEn}</SelectItem>)}
                </SelectContent>
              </Select>
              <FieldError id="resource-type-error">{submitted && validation.type ? t(`${directory}.typeRequired`) : undefined}</FieldError>
              {referenceDataError ? <p role="alert" className="text-xs text-destructive">{t("resources.finder.referenceDataFailed")}</p> : null}
              </>
            )}
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-status" label={t(`${directory}.status`)} required />
            <Select value={values.status} onValueChange={(value) => update("status", value === "Inactive" ? "Inactive" : "Active")}>
              <SelectTrigger id="resource-status" aria-required="true"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">{t(`${directory}.active`)}</SelectItem>
                <SelectItem value="Inactive">{t(`${directory}.inactive`)}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </FormSection>

      <FormSection number={2} title={t(`${directory}.professionalInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-email" label={t(`${directory}.email`)} optionalText={optionalText} />
            <Input id="resource-email" type="email" maxLength={RESOURCE_FIELD_LIMITS.email} value={values.email} onChange={(event) => update("email", event.target.value)} aria-invalid={submitted && (validation.email || validation.emailTooLong) ? true : undefined} aria-describedby={submitted && (validation.email || validation.emailTooLong) ? "resource-email-error" : undefined} />
            <FieldError id="resource-email-error">{submitted && validation.emailTooLong ? t(`${directory}.emailTooLong`) : submitted && validation.email ? t(`${directory}.emailInvalid`) : undefined}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-phone" label={t(`${directory}.phone`)} optionalText={optionalText} />
            <Input id="resource-phone" type="tel" maxLength={RESOURCE_FIELD_LIMITS.phone} value={values.phone} onChange={(event) => update("phone", event.target.value)} aria-invalid={submitted && validation.phoneTooLong ? true : undefined} aria-describedby={submitted && validation.phoneTooLong ? "resource-phone-error" : undefined} />
            <FieldError id="resource-phone-error">{submitted && validation.phoneTooLong ? t(`${directory}.phoneTooLong`) : undefined}</FieldError>
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <FieldLabel id="resource-job-title" label={t(`${directory}.jobTitle`)} optionalText={optionalText} />
            <Input id="resource-job-title" maxLength={RESOURCE_FIELD_LIMITS.jobTitle} value={values.jobTitle} onChange={(event) => update("jobTitle", event.target.value)} aria-invalid={submitted && validation.jobTitleTooLong ? true : undefined} aria-describedby={submitted && validation.jobTitleTooLong ? "resource-job-title-error" : undefined} />
            <FieldError id="resource-job-title-error">{submitted && validation.jobTitleTooLong ? t(`${directory}.jobTitleTooLong`) : undefined}</FieldError>
          </div>
        </div>
      </FormSection>

      <FormSection number={3} title={t(`${directory}.associationInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <FieldLabel id="resource-base-calendar" label={t(`${directory}.baseCalendar`)} optionalText={optionalText} />
            <Select value={values.baseCalendarId || "__none__"} onValueChange={(value) => update("baseCalendarId", value === "__none__" ? "" : value)}>
              <SelectTrigger id="resource-base-calendar" aria-invalid={submitted && validation.baseCalendar ? true : undefined} aria-describedby={submitted && validation.baseCalendar ? "resource-base-calendar-error" : undefined}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t(`${directory}.optional`)}</SelectItem>
                {calendarsLoading ? <SelectItem value="__loading__" disabled>{t(`${directory}.loadingCalendars`)}</SelectItem> : null}
                {!calendarsLoading && calendars.length === 0 ? <SelectItem value="__empty__" disabled>{t(`${directory}.noCalendars`)}</SelectItem> : null}
                {calendars.map((calendar) => <SelectItem key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isDefault ? ` · ${t(`${directory}.default`)}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{t(`${directory}.baseCalendarHint`)}</p>
            <FieldError id="resource-base-calendar-error">{submitted && validation.baseCalendar ? t(`${directory}.baseCalendarInvalid`) : undefined}</FieldError>
          </div>
          <div className="space-y-1.5">
            <FieldLabel id="resource-organizational-unit" label={t(`${directory}.organizationalUnit`)} optionalText={optionalText} />
            <Select value={values.organizationalUnitId || "__none__"} onValueChange={(value) => update("organizationalUnitId", value === "__none__" ? "" : value)}>
              <SelectTrigger id="resource-organizational-unit"><SelectValue placeholder={t(`${directory}.selectOrganizationalUnit`)} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t(`${directory}.none`)}</SelectItem>
                {units.filter((unit) => unit.isActive !== false || unit.id === values.organizationalUnitId).map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.name}{unit.code ? ` · ${unit.code}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {isVendorResource ? (
            <div className="space-y-1.5">
              <FieldLabel id="resource-vendor" label={t(`${directory}.vendor`)} required />
              <Select value={values.vendorId} onValueChange={(value) => update("vendorId", value)}>
                <SelectTrigger id="resource-vendor" aria-required="true" aria-invalid={submitted && validation.vendor ? true : undefined} aria-describedby={submitted && validation.vendor ? "resource-vendor-error" : undefined}><SelectValue placeholder={t(`${directory}.selectVendor`)} /></SelectTrigger>
                <SelectContent>
                  {vendorsLoading ? <SelectItem value="__loading__" disabled>{t("common.loading")}</SelectItem> : null}
                  {activeVendors.map((vendor) => <SelectItem key={vendor.id} value={vendor.id}>{vendor.name}{vendor.code ? ` · ${vendor.code}` : ""}</SelectItem>)}
                </SelectContent>
              </Select>
              <FieldError id="resource-vendor-error">{submitted && validation.vendor ? t(`${directory}.vendorRequired`) : undefined}</FieldError>
            </div>
          ) : (
            <>
              <div className="space-y-1.5">
                <FieldLabel id="resource-system-user" label={t(`${directory}.systemUser`)} optionalText={optionalText} />
                <Select value={values.systemUserId || "__none__"} onValueChange={(value) => update("systemUserId", value === "__none__" ? "" : value)}>
                  <SelectTrigger id="resource-system-user"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">{t(`${directory}.noSystemUser`)}</SelectItem>
                    {usersLoading ? <SelectItem value="__loading__" disabled>{t(`${directory}.loadingUsers`)}</SelectItem> : null}
                    {users.map((user) => <SelectItem key={user.id} value={user.id}>{user.fullName} · {user.email}</SelectItem>)}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">{t(`${directory}.systemUserHint`)}</p>
              </div>
              <div className="space-y-1.5">
                <FieldLabel id="resource-seniority" label={t(`${directory}.seniority`)} optionalText={optionalText} />
                <Select value={values.seniority || "__none__"} onValueChange={(value) => update("seniority", value === "__none__" ? "" : value)}>
                  <SelectTrigger id="resource-seniority"><SelectValue placeholder={optionalText} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">{optionalText}</SelectItem>
                    {values.seniority && !seniorityLevels.some((level) => level.code === values.seniority) ? <SelectItem value={values.seniority}>{values.seniority} · {t(`${directory}.legacyValue`)}</SelectItem> : null}
                    {seniorityLevels.map((level) => <SelectItem key={level.code} value={level.code}>{locale === "ar" ? level.nameAr : level.nameEn}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <FieldLabel id="resource-notes" label={t(`${directory}.notes`)} optionalText={optionalText} />
                <Textarea id="resource-notes" maxLength={RESOURCE_FIELD_LIMITS.notes} value={values.notes} onChange={(event) => update("notes", event.target.value)} rows={3} aria-invalid={submitted && validation.notesTooLong ? true : undefined} aria-describedby={submitted && validation.notesTooLong ? "resource-notes-error" : undefined} />
                <FieldError id="resource-notes-error">{submitted && validation.notesTooLong ? t(`${directory}.notesTooLong`) : undefined}</FieldError>
              </div>
            </>
          )}
        </div>
      </FormSection>
    </div>
  );
}
