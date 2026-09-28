"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Pencil } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useUpdateVendor, useVendor, type VendorDto } from "@/hooks/resources/useVendors";
import { useVendorCategories } from "@/lib/api/reference-data";

type EditForm = {
  name: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  vendorCategoryId: string;
};

const emptyForm: EditForm = {
  name: "",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  vendorCategoryId: "",
};

function formFrom(vendor: VendorDto): EditForm {
  return {
    name: vendor.name ?? "",
    contactName: vendor.contactName ?? "",
    contactEmail: vendor.contactEmail ?? "",
    contactPhone: vendor.contactPhone ?? "",
    vendorCategoryId: vendor.vendorCategoryId ?? "",
  };
}

function isValidEmail(value: string): boolean {
  if (!value) return true;
  try {
    // eslint-disable-next-line no-new
    return Boolean(new URL(`mailto:${value}`));
  } catch {
    return false;
  }
}

export function EditVendorButton({
  vendor,
  open: controlledOpen,
  onOpenChange,
  hideTrigger = false,
}: {
  vendor: VendorDto;
  open?: boolean;
  // eslint-disable-next-line no-unused-vars
  onOpenChange?: (next: boolean) => void;
  hideTrigger?: boolean;
}) {
  const t = useTranslations("resources.vendors");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Manage));
  const [uncontrolledOpen, setUncontrolledOpen] = useState(false);
  const open = controlledOpen ?? uncontrolledOpen;
  const setOpen = onOpenChange ?? setUncontrolledOpen;
  const [form, setForm] = useState<EditForm>(emptyForm);
  const [nameError, setNameError] = useState(false);
  const [categoryError, setCategoryError] = useState(false);
  const [emailError, setEmailError] = useState(false);
  const update = useUpdateVendor();
  const categoriesQuery = useVendorCategories(false);
  const vendorQuery = useVendor(open ? vendor.id : null);

  const categories = useMemo(() => categoriesQuery.data ?? [], [categoriesQuery.data]);

  const activeCategories = useMemo(
    () => categories.filter((category) => category.isActive || category.id === form.vendorCategoryId),
    [categories, form.vendorCategoryId]
  );

  useEffect(() => {
    if (!open) return;
    const detail = vendorQuery.data ?? vendor;
    setForm(formFrom(detail));
    setNameError(false);
    setCategoryError(false);
    setEmailError(false);
  }, [open, vendorQuery.data, vendor]);

  if (authStatus === "loading") {
    return <Button size="icon" variant="ghost" disabled aria-label={t("edit")}><Pencil className="h-3.5 w-3.5" /></Button>;
  }

  if (!canManage) {
    return null;
  }

  const setField = <K extends keyof EditForm>(field: K, value: EditForm[K]) =>
    setForm((current) => ({ ...current, [field]: value }));

  const close = (next: boolean) => {
    setOpen(next);
    if (!next) {
      setNameError(false);
      setCategoryError(false);
      setEmailError(false);
    }
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
      await update.mutateAsync({
        id: vendor.id,
        input: {
          name,
          contactName: contactNameOrUndefined(form.contactName),
          contactEmail: contactEmail || undefined,
          contactPhone: form.contactPhone.trim() || undefined,
          vendorCategoryId: form.vendorCategoryId,
        },
      });
      close(false);
    } catch {
      /* surfaced via update.error below */
    }
  };

  const detail = vendorQuery.data ?? vendor;

  return (
    <>
      {hideTrigger ? null : (
        <Button size="icon" variant="ghost" type="button" onClick={() => setOpen(true)} aria-label={t("edit")}>
          <Pencil className="h-3.5 w-3.5" />
        </Button>
      )}
      <Dialog
        open={open}
        onOpenChange={close}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("editTitle")}</DialogTitle>
            <DialogDescription>{t("editDescription")}</DialogDescription>
          </DialogHeader>
          <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void submit(event)}>
            <DialogBody className="space-y-6">
            <section aria-labelledby="edit-vendor-basic-heading" className="space-y-3">
              <h3 id="edit-vendor-basic-heading" className="text-sm font-semibold text-foreground">
                {t("basicInformation")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5 sm:col-span-2">
                  <Label htmlFor="edit-vendor-name">{t("name")}</Label>
                  <Input
                    id="edit-vendor-name"
                    required
                    value={form.name}
                    onChange={(event) => setField("name", event.target.value)}
                  />
                  {nameError ? <p className="text-xs text-destructive">{t("nameRequired")}</p> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-vendor-code">{t("code")}</Label>
                  <Input id="edit-vendor-code" value={detail.code ?? ""} readOnly disabled />
                  <p className="text-xs text-muted-foreground">{t("codeReadOnly")}</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-vendor-category">{t("category")}</Label>
                  <select
                    id="edit-vendor-category"
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

            <section aria-labelledby="edit-vendor-contact-heading" className="space-y-3">
              <h3 id="edit-vendor-contact-heading" className="text-sm font-semibold text-foreground">
                {t("contactInformation")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="edit-vendor-contact-name">{t("contactName")}</Label>
                  <Input id="edit-vendor-contact-name" value={form.contactName} onChange={(event) => setField("contactName", event.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-vendor-contact-email">{t("contactEmail")}</Label>
                  <Input
                    id="edit-vendor-contact-email"
                    type="email"
                    value={form.contactEmail}
                    onChange={(event) => setField("contactEmail", event.target.value)}
                    aria-invalid={emailError || undefined}
                  />
                  {emailError ? <p className="text-xs text-destructive">{t("emailInvalid")}</p> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-vendor-contact-phone">{t("contactPhone")}</Label>
                  <Input id="edit-vendor-contact-phone" value={form.contactPhone} onChange={(event) => setField("contactPhone", event.target.value)} />
                </div>
              </div>
            </section>

            {update.error ? (
              <p className="text-sm text-destructive" role="alert">
                {update.error instanceof Error ? update.error.message : t("mutationError")}
              </p>
            ) : null}
            </DialogBody>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => close(false)} disabled={update.isPending}>
                {t("cancel")}
              </Button>
              <Button type="submit" disabled={update.isPending}>
                {update.isPending ? t("saving") : t("save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}

function contactNameOrUndefined(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed || undefined;
}
