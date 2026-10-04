"use client";

import { FormEvent, useMemo, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { useAuth } from "@/lib/auth";
import { hasAnyPermission, Permissions } from "@/lib/permissions";
import { useCreateVendor, type VendorDto } from "@/hooks/resources/useVendors";
import { useVendorCategories } from "@/lib/api/reference-data";

type VendorForm = {
  name: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  vendorCategoryId: string;
};

const emptyForm: VendorForm = {
  name: "",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  vendorCategoryId: "",
};

function isValidEmail(value: string): boolean {
  if (!value) return true;
  try {
    // eslint-disable-next-line no-new
    return Boolean(new URL(`mailto:${value}`));
  } catch {
    return false;
  }
}

export function AddVendorButton() {
  const t = useTranslations("resources.vendors");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const canCreate = hasAnyPermission(user?.permissions ?? [], [Permissions.Resources.Create, Permissions.Resources.Manage]);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<VendorForm>(emptyForm);
  const [nameError, setNameError] = useState(false);
  const [categoryError, setCategoryError] = useState(false);
  const [emailError, setEmailError] = useState(false);
  const [createdVendor, setCreatedVendor] = useState<VendorDto | null>(null);
  const create = useCreateVendor();
  const categoriesQuery = useVendorCategories(false);

  const activeCategories = useMemo(
    () => (categoriesQuery.data ?? []).filter((category) => category.isActive),
    [categoriesQuery.data]
  );

  if (authStatus === "loading") {
    return <Button size="sm" disabled><Plus className="mr-2 h-4 w-4" />{t("addVendor")}</Button>;
  }

  if (!canCreate) {
    return null;
  }

  const setField = <K extends keyof VendorForm>(field: K, value: VendorForm[K]) =>
    setForm((current) => ({ ...current, [field]: value }));

  const close = () => {
    setOpen(false);
    setForm(emptyForm);
    setNameError(false);
    setCategoryError(false);
    setEmailError(false);
    setCreatedVendor(null);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const name = form.name.trim();
    const contactEmail = form.contactEmail.trim();
    let invalid = false;

    if (!name) {
      setNameError(true);
      invalid = true;
    } else {
      setNameError(false);
    }

    if (!form.vendorCategoryId) {
      setCategoryError(true);
      invalid = true;
    } else {
      setCategoryError(false);
    }

    if (!isValidEmail(contactEmail)) {
      setEmailError(true);
      invalid = true;
    } else {
      setEmailError(false);
    }

    if (invalid) return;

    try {
      const created = await create.mutateAsync({
        name,
        contactName: form.contactName.trim() || undefined,
        contactEmail: contactEmail || undefined,
        contactPhone: form.contactPhone.trim() || undefined,
        vendorCategoryId: form.vendorCategoryId,
      });
      // Keep the sheet open so the system-assigned vendor code is shown to the user.
      setCreatedVendor(created ?? null);
    } catch {
      /* surfaced via create.error below */
    }
  };

  return (
    <>
      <Button size="sm" onClick={() => setOpen(true)} type="button">
        <Plus className="mr-2 h-4 w-4" />
        {t("addVendor")}
      </Button>
      <SideSheet
        open={open}
        onOpenChange={(next) => { if (!next) close(); else setOpen(next); }}
        title={createdVendor ? t("createdTitle") : t("createTitle")}
        description={createdVendor ? createdVendor.name : t("formDescription")}
        size="lg"
        closeLabel={createdVendor ? t("close") : t("cancel")}
      >
        {createdVendor ? (
          <>
            <SideSheetBody className="space-y-4">
              <div className="rounded-md border border-border bg-muted/40 p-4">
                <p className="text-sm text-muted-foreground">{t("createdCodeLabel")}</p>
                <p className="mt-1 font-mono text-2xl font-semibold tracking-wide text-foreground">
                  {createdVendor.code ?? "—"}
                </p>
              </div>
              <p className="text-sm text-muted-foreground">{t("createdHint")}</p>
            </SideSheetBody>
            <SideSheetFooter>
              <Button type="button" onClick={close}>
                {t("done")}
              </Button>
            </SideSheetFooter>
          </>
        ) : (
          <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void submit(event)}>
            <SideSheetBody className="space-y-6">
            <section aria-labelledby="vendor-basic-heading" className="space-y-3">
              <h3 id="vendor-basic-heading" className="text-sm font-semibold text-foreground">
                {t("basicInformation")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5 sm:col-span-2">
                  <Label htmlFor="vendor-name">{t("name")}</Label>
                  <Input
                    id="vendor-name"
                    required
                    value={form.name}
                    onChange={(event) => setField("name", event.target.value)}
                  />
                  {nameError ? <p className="text-xs text-destructive">{t("nameRequired")}</p> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="vendor-code">{t("code")}</Label>
                  <Input
                    id="vendor-code"
                    value=""
                    readOnly
                    disabled
                    placeholder={t("autoGenerated")}
                    aria-describedby="vendor-code-hint"
                  />
                  <p id="vendor-code-hint" className="text-xs text-muted-foreground">{t("autoGeneratedHint")}</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="vendor-category">{t("category")}</Label>
                  <select
                    id="vendor-category"
                    className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={form.vendorCategoryId}
                    onChange={(event) => setField("vendorCategoryId", event.target.value)}
                    required
                    aria-invalid={categoryError || undefined}
                  >
                    <option value="">{t("selectCategory")}</option>
                    {activeCategories.map((category) => (
                      <option key={category.id} value={category.id}>
                        {locale === "ar" ? category.nameAr : category.nameEn}
                      </option>
                    ))}
                  </select>
                  {categoryError ? <p className="text-xs text-destructive">{t("categoryRequired")}</p> : null}
                </div>
              </div>
            </section>

            <section aria-labelledby="vendor-contact-heading" className="space-y-3">
              <h3 id="vendor-contact-heading" className="text-sm font-semibold text-foreground">
                {t("contactInformation")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="vendor-contact-name">{t("contactName")}</Label>
                  <Input id="vendor-contact-name" value={form.contactName} onChange={(event) => setField("contactName", event.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="vendor-contact-email">{t("contactEmail")}</Label>
                  <Input
                    id="vendor-contact-email"
                    type="email"
                    value={form.contactEmail}
                    onChange={(event) => setField("contactEmail", event.target.value)}
                    aria-invalid={emailError || undefined}
                  />
                  {emailError ? <p className="text-xs text-destructive">{t("emailInvalid")}</p> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="vendor-contact-phone">{t("contactPhone")}</Label>
                  <Input id="vendor-contact-phone" value={form.contactPhone} onChange={(event) => setField("contactPhone", event.target.value)} />
                </div>
              </div>
            </section>

            <section aria-labelledby="vendor-business-heading" className="space-y-3">
              <h3 id="vendor-business-heading" className="text-sm font-semibold text-foreground">
                {t("businessInformation")}
              </h3>
              <p className="text-xs text-muted-foreground">{t("businessInformationHint")}</p>
            </section>

            {create.error ? (
              <p className="text-sm text-destructive" role="alert">
                {create.error instanceof Error ? create.error.message : t("mutationError")}
              </p>
            ) : null}
            </SideSheetBody>
            <SideSheetFooter>
              <Button type="button" variant="outline" onClick={close} disabled={create.isPending}>
                {t("cancel")}
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending ? t("saving") : t("create")}
              </Button>
            </SideSheetFooter>
          </form>
        )}
      </SideSheet>
    </>
  );
}
