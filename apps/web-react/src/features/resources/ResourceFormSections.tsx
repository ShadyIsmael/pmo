import * as React from "react";
import { useI18n } from "@i18n/messages";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Textarea } from "@components/ui/Textarea";
import type { ResourceBaseCalendarDto } from "./api/resources";
import type { UserLookupDto } from "../projects/lib/project-lookups";
import type { VendorDto } from "../vendors/api/vendors";
import { dialogValuesToResourceForm, validateResourceForm } from "./resource-form";

export type ResourceFormValues = {
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  employeeId: string;
  organizationalUnitId: string;
  resourceType: "Staff" | "VendorResource";
  status: "Active" | "Inactive";
  vendorId: string;
  baseCalendarId: string;
  systemUserId: string;
  employmentType: string;
  seniority: string;
  notes: string;
};

type OrganizationalUnitOption = { id: string; name: string; code?: string; isActive?: boolean };

export const emptyResourceFormValues: ResourceFormValues = {
  name: "",
  email: "",
  phone: "",
  jobTitle: "",
  employeeId: "",
  organizationalUnitId: "",
  resourceType: "Staff",
  status: "Active",
  vendorId: "",
  baseCalendarId: "",
  systemUserId: "",
  employmentType: "",
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

export function ResourceFormSections({
  values,
  setValues,
  calendars = [],
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
  const { t } = useI18n();
  const directory = "resources.directory";
  const validation = validateResourceForm(dialogValuesToResourceForm(values));
  const update = (field: keyof ResourceFormValues, value: string) => setValues((current) => ({ ...current, [field]: value }));
  const activeVendors = vendors.filter((vendor) => vendor.isActive || vendor.id === currentVendorId);

  return (
    <div className="space-y-5">
      <FormSection number={1} title={t(`${directory}.basicInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="resource-name">{t(`${directory}.name`)}</Label>
            <Input id="resource-name" value={values.name} onChange={(event) => update("name", event.target.value)} required aria-invalid={submitted && validation.name ? true : undefined} />
            {submitted && validation.name ? <p role="alert" className="text-xs text-destructive">{t(`${directory}.nameRequired`)}</p> : null}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="resource-type">{t(`${directory}.resourceType`)}</Label>
            {readOnlyType ? (
              <>
                <Input id="resource-type" readOnly value={values.resourceType === "VendorResource" ? t(`${directory}.vendorResource`) : t(`${directory}.staff`)} />
                <p className="text-xs text-muted-foreground">{t(`${directory}.typeReadOnlyHint`)}</p>
              </>
            ) : (
              <Select value={values.resourceType} onValueChange={(value) => setValues((current) => ({ ...current, resourceType: value === "VendorResource" ? "VendorResource" : "Staff", vendorId: value === "VendorResource" ? current.vendorId : "" }))}>
                <SelectTrigger id="resource-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Staff">{t(`${directory}.staff`)}</SelectItem>
                  <SelectItem value="VendorResource">{t(`${directory}.vendorResource`)}</SelectItem>
                </SelectContent>
              </Select>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="resource-status">{t(`${directory}.status`)}</Label>
            <Select value={values.status} onValueChange={(value) => update("status", value === "Inactive" ? "Inactive" : "Active")}>
              <SelectTrigger id="resource-status"><SelectValue /></SelectTrigger>
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
            <Label htmlFor="resource-email">{t(`${directory}.email`)}</Label>
            <Input id="resource-email" type="email" value={values.email} onChange={(event) => update("email", event.target.value)} aria-invalid={submitted && validation.email ? true : undefined} />
            {submitted && validation.email ? <p role="alert" className="text-xs text-destructive">{t(`${directory}.emailInvalid`)}</p> : null}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="resource-phone">{t(`${directory}.phone`)}</Label>
            <Input id="resource-phone" value={values.phone} onChange={(event) => update("phone", event.target.value)} />
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <Label htmlFor="resource-job-title">{t(`${directory}.jobTitle`)}</Label>
            <Input id="resource-job-title" value={values.jobTitle} onChange={(event) => update("jobTitle", event.target.value)} />
          </div>
        </div>
      </FormSection>

      <FormSection number={3} title={t(`${directory}.associationInformation`)}>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="resource-base-calendar">{t(`${directory}.baseCalendar`)}</Label>
            <Select value={values.baseCalendarId || "__none__"} onValueChange={(value) => update("baseCalendarId", value === "__none__" ? "" : value)}>
              <SelectTrigger id="resource-base-calendar" aria-invalid={submitted && validation.baseCalendar ? true : undefined}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t(`${directory}.optional`)}</SelectItem>
                {calendarsLoading ? <SelectItem value="__loading__" disabled>{t(`${directory}.loadingCalendars`)}</SelectItem> : null}
                {!calendarsLoading && calendars.length === 0 ? <SelectItem value="__empty__" disabled>{t(`${directory}.noCalendars`)}</SelectItem> : null}
                {calendars.map((calendar) => <SelectItem key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isDefault ? ` · ${t(`${directory}.default`)}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{t(`${directory}.baseCalendarHint`)}</p>
            {submitted && validation.baseCalendar ? <p role="alert" className="text-xs text-destructive">{t(`${directory}.baseCalendarInvalid`)}</p> : null}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="resource-organizational-unit">{t(`${directory}.organizationalUnit`)}</Label>
            <Select value={values.organizationalUnitId || "__none__"} onValueChange={(value) => update("organizationalUnitId", value === "__none__" ? "" : value)}>
              <SelectTrigger id="resource-organizational-unit"><SelectValue placeholder={t(`${directory}.selectOrganizationalUnit`)} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t(`${directory}.none`)}</SelectItem>
                {units.filter((unit) => unit.isActive !== false || unit.id === values.organizationalUnitId).map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.name}{unit.code ? ` · ${unit.code}` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {values.resourceType === "VendorResource" ? (
            <div className="space-y-1.5">
              <Label htmlFor="resource-vendor">{t(`${directory}.vendor`)}</Label>
              <Select value={values.vendorId} onValueChange={(value) => update("vendorId", value)}>
                <SelectTrigger id="resource-vendor" aria-invalid={submitted && validation.vendor ? true : undefined}><SelectValue placeholder={t(`${directory}.selectVendor`)} /></SelectTrigger>
                <SelectContent>
                  {vendorsLoading ? <SelectItem value="__loading__" disabled>{t("common.loading")}</SelectItem> : null}
                  {activeVendors.map((vendor) => <SelectItem key={vendor.id} value={vendor.id}>{vendor.name}{vendor.code ? ` · ${vendor.code}` : ""}</SelectItem>)}
                </SelectContent>
              </Select>
              {submitted && validation.vendor ? <p role="alert" className="text-xs text-destructive">{t(`${directory}.vendorRequired`)}</p> : null}
            </div>
          ) : (
            <>
              <div className="space-y-1.5">
                <Label htmlFor="resource-system-user">{t(`${directory}.systemUser`)}</Label>
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
                <Label htmlFor="resource-employee-id">{t(`${directory}.employeeId`)}</Label>
                <Input id="resource-employee-id" value={values.employeeId} onChange={(event) => update("employeeId", event.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-employment-type">{t(`${directory}.employmentType`)}</Label>
                <Input id="resource-employment-type" value={values.employmentType} onChange={(event) => update("employmentType", event.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-seniority">{t(`${directory}.seniority`)}</Label>
                <Input id="resource-seniority" value={values.seniority} onChange={(event) => update("seniority", event.target.value)} />
              </div>
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="resource-notes">{t(`${directory}.notes`)}</Label>
                <Textarea id="resource-notes" value={values.notes} onChange={(event) => update("notes", event.target.value)} rows={3} />
              </div>
            </>
          )}
        </div>
      </FormSection>
    </div>
  );
}
