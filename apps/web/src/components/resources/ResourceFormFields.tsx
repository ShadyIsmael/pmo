"use client";

/**
 * Shared Resource form field components.
 *
 * Add and Edit render the same field components with the same validation wiring so
 * the two forms cannot expose different business fields. Labels are passed in already
 * translated so these components stay namespace-agnostic.
 */

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  RESOURCE_STATUSES,
  RESOURCE_TYPES,
  type ResourceFormModel,
  type ResourceStatus,
  type ResourceType,
} from "@/lib/resources/resource-form";

export type VendorOption = { id: string; name: string; code?: string };
export type UserOption = { id: string; fullName: string; email: string };
export type BaseCalendarOption = { id: string; name: string; isDefault?: boolean };
export type OrganizationalUnitOption = { id: string; name: string; code?: string };

export type ResourceFormLabels = {
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  employeeId: string;
  resourceType: string;
  status: string;
  employmentType: string;
  seniority: string;
  notes: string;
  vendor: string;
  selectVendor: string;
  baseCalendar: string;
  baseCalendarHint: string;
  systemUser: string;
  noSystemUser: string;
  systemUserHint: string;
  loadingUsers: string;
  loadingCalendars: string;
  noCalendars: string;
  staff: string;
  vendorResource: string;
  active: string;
  inactive: string;
  optional: string;
  typeReadOnlyHint: string;
  organizationalUnit: string;
  selectOrganizationalUnit: string;
};

export type ResourceFormProps = {
  form: ResourceFormModel;
  labels: ResourceFormLabels;
  sectionLabels?: {
    basicInformation: string;
    professionalInformation: string;
    associationInformation: string;
  };
  vendorOptions: VendorOption[];
  baseCalendarOptions: BaseCalendarOption[];
  organizationalUnitOptions: OrganizationalUnitOption[];
  userOptions: UserOption[];
  usersLoading: boolean;
  calendarsLoading: boolean;
  fieldErrors?: Partial<Record<keyof ResourceFormModel, string>>;
  readOnlyType?: boolean;
  idPrefix?: string;
  onChange: <K extends keyof ResourceFormModel>(_field: K, _value: ResourceFormModel[K]) => void;
};

/** Shared callback shape. Declared once so every field reports changes the same way. */
type FieldChange = (value: string) => void;

function FieldError({ children }: { children?: React.ReactNode }) {
  if (!children) return null;
  return <p className="text-xs text-destructive">{children}</p>;
}

export function NameField({
  id,
  value,
  onChange,
  label,
  error,
  required = true,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  error?: string;
  required?: boolean;
}) {
  return (
    <div className="space-y-1.5 sm:col-span-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        required={required}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error ? true : undefined}
      />
      <FieldError>{error}</FieldError>
    </div>
  );
}

export function EmailField({
  id,
  value,
  onChange,
  label,
  error,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  error?: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="email"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error ? true : undefined}
      />
      <FieldError>{error}</FieldError>
    </div>
  );
}

export function PhoneField({
  id,
  value,
  onChange,
  label,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}

export function JobTitleField({
  id,
  value,
  onChange,
  label,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}

export function TextField({
  id,
  value,
  onChange,
  label,
  className,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  className?: string;
}) {
  return (
    <div className={className ?? "space-y-1.5"}>
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}

/**
 * Resource type. Read-only in Edit because type is an immutable identity field:
 * changing it would orphan allocations and vendor links.
 */
export function TypeField({
  id,
  value,
  onChange,
  label,
  staff,
  vendorResource,
  readOnly = false,
  readOnlyHint,
}: {
  id: string;
  value: ResourceType;
  onChange: (value: ResourceType) => void;
  label: string;
  staff: string;
  vendorResource: string;
  readOnly?: boolean;
  readOnlyHint?: string;
}) {
  if (readOnly) {
    return (
      <div className="space-y-1.5">
        <Label htmlFor={id}>{label}</Label>
        <Input id={id} readOnly value={value === RESOURCE_TYPES[1] ? vendorResource : staff} />
        {readOnlyHint ? <p className="text-xs text-muted-foreground">{readOnlyHint}</p> : null}
      </div>
    );
  }

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={(next) => onChange(next as ResourceType)}>
        <SelectTrigger id={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={RESOURCE_TYPES[0]}>{staff}</SelectItem>
          <SelectItem value={RESOURCE_TYPES[1]}>{vendorResource}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  );
}

export function StatusField({
  id,
  value,
  onChange,
  label,
  active,
  inactive,
}: {
  id: string;
  value: ResourceStatus;
  onChange: (value: ResourceStatus) => void;
  label: string;
  active: string;
  inactive: string;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={(next) => onChange(next as ResourceStatus)}>
        <SelectTrigger id={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={RESOURCE_STATUSES[0]}>{active}</SelectItem>
          <SelectItem value={RESOURCE_STATUSES[1]}>{inactive}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  );
}

export function VendorField({
  id,
  value,
  onChange,
  label,
  placeholder,
  error,
  options,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  placeholder: string;
  error?: string;
  options: VendorOption[];
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <select
        id={id}
        className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        required
        aria-invalid={error ? true : undefined}
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.code ? `[${option.code}] ` : ""}
            {option.name}
          </option>
        ))}
      </select>
      <FieldError>{error}</FieldError>
    </div>
  );
}

export function BaseCalendarField({
  id,
  value,
  onChange,
  label,
  hint,
  placeholder,
  options,
  loading,
  noOptions,
  isLoading,
  error,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  hint: string;
  placeholder: string;
  options: BaseCalendarOption[];
  loading: string;
  noOptions: string;
  isLoading: boolean;
  error?: string;
}) {
  const NO_CALENDAR = "__none__";
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value || NO_CALENDAR} onValueChange={(next) => onChange(next === NO_CALENDAR ? "" : next)}>
        <SelectTrigger id={id} aria-invalid={error ? true : undefined}>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NO_CALENDAR}>{placeholder}</SelectItem>
          {isLoading ? <SelectItem value="__loading__" disabled>{loading}</SelectItem> : null}
          {!isLoading && options.length === 0 ? <SelectItem value="__empty__" disabled>{noOptions}</SelectItem> : null}
          {options.map((option) => (
            <SelectItem key={option.id} value={option.id}>
              {option.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">{hint}</p>
      <FieldError>{error}</FieldError>
    </div>
  );
}

export function OrganizationalUnitField({ id, value, onChange, label, placeholder, options }: { id: string; value: string; onChange: FieldChange; label: string; placeholder: string; options: OrganizationalUnitOption[] }) {
  return <div className="space-y-1.5"><Label htmlFor={id}>{label}</Label><select id={id} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}><option value="">{placeholder}</option>{options.map((option) => <option key={option.id} value={option.id}>{option.name}{option.code ? ` · ${option.code}` : ""}</option>)}</select></div>;
}

export function SystemUserField({
  id,
  value,
  onChange,
  label,
  noSystemUser,
  hint,
  loading,
  options,
  isLoading,
}: {
  id: string;
  value: string;
  onChange: FieldChange;
  label: string;
  noSystemUser: string;
  hint: string;
  loading: string;
  options: { id: string; fullName: string; email: string }[];
  isLoading: boolean;
}) {
  const NO_USER = "__none__";
  return (
    <div className="space-y-1.5 sm:col-span-2">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value || NO_USER} onValueChange={(next) => onChange(next === NO_USER ? "" : next)}>
        <SelectTrigger id={id}>
          <SelectValue placeholder={noSystemUser} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NO_USER}>{noSystemUser}</SelectItem>
          {options.map((option) => (
            <SelectItem key={option.id} value={option.id}>
              {option.fullName} · {option.email}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">{isLoading ? loading : hint}</p>
    </div>
  );
}

/**
 * The single Resource Add/Edit form. Entry points provide translated labels,
 * options, and the model; the business fields and conditional sections stay
 * identical everywhere the Resource form is mounted.
 */
export function ResourceForm({
  form,
  labels,
  sectionLabels,
  vendorOptions,
  baseCalendarOptions,
  organizationalUnitOptions,
  userOptions,
  usersLoading,
  calendarsLoading,
  fieldErrors = {},
  readOnlyType = false,
  idPrefix = "resource",
  onChange,
}: ResourceFormProps) {
  const errorFor = (field: keyof ResourceFormModel) => fieldErrors[field];

  const basicFields = (
    <div className="grid gap-4 sm:grid-cols-2">
      <NameField
        id={`${idPrefix}-name`}
        value={form.name}
        onChange={(value) => onChange("name", value)}
        label={labels.name}
        error={errorFor("name")}
      />
      <TypeField
        id={`${idPrefix}-type`}
        value={form.type}
        onChange={(value) => onChange("type", value)}
        label={labels.resourceType}
        staff={labels.staff}
        vendorResource={labels.vendorResource}
        readOnly={readOnlyType}
        readOnlyHint={readOnlyType ? labels.typeReadOnlyHint : undefined}
      />
      <StatusField
        id={`${idPrefix}-status`}
        value={form.status}
        onChange={(value) => onChange("status", value)}
        label={labels.status}
        active={labels.active}
        inactive={labels.inactive}
      />
    </div>
  );

  const professionalFields = (
    <div className="grid gap-4 sm:grid-cols-2">
      <EmailField
        id={`${idPrefix}-email`}
        value={form.email}
        onChange={(value) => onChange("email", value)}
        label={labels.email}
        error={errorFor("email")}
      />
      <PhoneField
        id={`${idPrefix}-phone`}
        value={form.phone}
        onChange={(value) => onChange("phone", value)}
        label={labels.phone}
      />
      <JobTitleField
        id={`${idPrefix}-job-title`}
        value={form.jobTitle}
        onChange={(value) => onChange("jobTitle", value)}
        label={labels.jobTitle}
      />
    </div>
  );

  const associationFields = (
    <div className="grid gap-4 sm:grid-cols-2">
      <BaseCalendarField
        id={`${idPrefix}-base-calendar`}
        value={form.baseCalendarId}
        onChange={(value) => onChange("baseCalendarId", value)}
        label={labels.baseCalendar}
        hint={labels.baseCalendarHint}
        placeholder={labels.optional}
        options={baseCalendarOptions}
        loading={labels.loadingCalendars}
        noOptions={labels.noCalendars}
        isLoading={calendarsLoading}
        error={errorFor("baseCalendarId")}
      />
      <OrganizationalUnitField id={`${idPrefix}-organizational-unit`} value={form.organizationalUnitId} onChange={(value) => onChange("organizationalUnitId", value)} label={labels.organizationalUnit} placeholder={labels.selectOrganizationalUnit} options={organizationalUnitOptions} />

      {form.type === RESOURCE_TYPES[1] ? (
        <VendorField
          id={`${idPrefix}-vendor`}
          value={form.vendorId}
          onChange={(value) => onChange("vendorId", value)}
          label={labels.vendor}
          placeholder={labels.selectVendor}
          error={errorFor("vendorId")}
          options={vendorOptions}
        />
      ) : (
        <>
          <SystemUserField
            id={`${idPrefix}-system-user`}
            value={form.userId}
            onChange={(value) => onChange("userId", value)}
            label={labels.systemUser}
            noSystemUser={labels.noSystemUser}
            hint={labels.systemUserHint}
            loading={labels.loadingUsers}
            options={userOptions}
            isLoading={usersLoading}
          />
          <TextField
            id={`${idPrefix}-employee-id`}
            value={form.employeeId}
            onChange={(value) => onChange("employeeId", value)}
            label={labels.employeeId}
          />
          <TextField
            id={`${idPrefix}-employment-type`}
            value={form.employmentType}
            onChange={(value) => onChange("employmentType", value)}
            label={labels.employmentType}
          />
          <TextField
            id={`${idPrefix}-seniority`}
            value={form.seniority}
            onChange={(value) => onChange("seniority", value)}
            label={labels.seniority}
          />
          <TextField
            id={`${idPrefix}-notes`}
            value={form.notes}
            onChange={(value) => onChange("notes", value)}
            label={labels.notes}
            className="space-y-1.5 sm:col-span-2"
          />
        </>
      )}
    </div>
  );

  if (sectionLabels) {
    const sectionClassName = "rounded-xl border bg-card/50 p-4 shadow-sm";
    const sectionHeaderClassName = "mb-4 flex items-center gap-3";

    return (
      <div className="space-y-5">
        <section className={sectionClassName}>
          <div className={sectionHeaderClassName}>
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">1</span>
            <h3 className="text-sm font-semibold text-foreground">{sectionLabels.basicInformation}</h3>
          </div>
          {basicFields}
        </section>
        <section className={sectionClassName}>
          <div className={sectionHeaderClassName}>
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">2</span>
            <h3 className="text-sm font-semibold text-foreground">{sectionLabels.professionalInformation}</h3>
          </div>
          {professionalFields}
        </section>
        <section className={sectionClassName}>
          <div className={sectionHeaderClassName}>
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">3</span>
            <h3 className="text-sm font-semibold text-foreground">{sectionLabels.associationInformation}</h3>
          </div>
          {associationFields}
        </section>
      </div>
    );
  }

  return <div className="grid gap-4 sm:grid-cols-2">{basicFields}{professionalFields}{associationFields}</div>;
}
