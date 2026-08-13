"use client";

import { useRef, useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Coins, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import {
  useCreateCurrency,
  useCurrencies,
  useDeleteCurrency,
  useUpdateCurrency,
  type CurrencyDto,
  type SaveCurrencyDto,
} from "@/hooks/useCurrencies";

const EMPTY_DRAFT: SaveCurrencyDto = {
  code: "",
  name: "",
  symbol: "",
  isBase: false,
};

export default function CurrenciesPage() {
  const t = useTranslations("administration.currencies");
  const { data: currencies, isLoading } = useCurrencies();
  const createMutation = useCreateCurrency();
  const updateMutation = useUpdateCurrency();
  const deleteMutation = useDeleteCurrency();
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState<SaveCurrencyDto>(EMPTY_DRAFT);
  const [formError, setFormError] = useState<string | null>(null);
  const isSubmitInFlight = useRef(false);

  const isSaving = createMutation.isPending || updateMutation.isPending;

  function beginCreate() {
    setIsFormOpen(true);
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
    setFormError(null);
  }

  function beginEdit(currency: CurrencyDto) {
    setIsFormOpen(true);
    setEditingId(currency.id);
    setDraft({
      code: currency.code,
      name: currency.name,
      symbol: currency.symbol,
      isBase: currency.isBase,
    });
    setFormError(null);
  }

  function cancelEdit() {
    setIsFormOpen(false);
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
    setFormError(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitInFlight.current) return;

    isSubmitInFlight.current = true;
    setFormError(null);

    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, data: draft });
      } else {
        await createMutation.mutateAsync(draft);
      }
      cancelEdit();
    } catch (error) {
      setFormError(error instanceof Error ? error.message : t("saveError"));
    } finally {
      isSubmitInFlight.current = false;
    }
  }

  async function handleDelete(currency: CurrencyDto) {
    if (currency.isBase || !window.confirm(t("deleteConfirm", { code: currency.code }))) return;
    try {
      await deleteMutation.mutateAsync(currency.id);
    } catch (error) {
      setFormError(error instanceof Error ? error.message : t("deleteError"));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={<Button size="sm" onClick={beginCreate}><Plus className="mr-2 h-4 w-4" />{t("add")}</Button>}
      />

      {isFormOpen && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{editingId ? t("edit") : t("add")}</CardTitle>
            <CardDescription>{t("formDescription")}</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4 md:grid-cols-4" onSubmit={handleSubmit}>
              <div className="space-y-2">
                <Label htmlFor="currency-code">{t("code")}</Label>
                <Input id="currency-code" value={draft.code} maxLength={3} onChange={(event) => setDraft((current) => ({ ...current, code: event.target.value.toUpperCase() }))} required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="currency-name">{t("name")}</Label>
                <Input id="currency-name" value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="currency-symbol">{t("symbol")}</Label>
                <Input id="currency-symbol" value={draft.symbol} onChange={(event) => setDraft((current) => ({ ...current, symbol: event.target.value }))} required />
              </div>
              <div className="flex items-center gap-3 pt-7">
                <Switch id="currency-base" checked={draft.isBase} onCheckedChange={(isBase) => setDraft((current) => ({ ...current, isBase }))} />
                <Label htmlFor="currency-base">{t("baseCurrency")}</Label>
              </div>
              {formError && <p className="text-sm text-destructive md:col-span-4">{formError}</p>}
              <div className="flex gap-2 md:col-span-4">
                <Button type="submit" disabled={isSaving}>{isSaving ? t("saving") : t("save")}</Button>
                <Button type="button" variant="outline" onClick={cancelEdit}>{t("cancel")}</Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">{t("listTitle")}</CardTitle></CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-3"><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /></div>
          ) : currencies && currencies.length > 0 ? (
            <div className="space-y-2">
              {currencies.map((currency) => (
                <div key={currency.id} className="flex flex-col gap-3 rounded-lg border p-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex min-w-0 items-center gap-3">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-muted text-sm font-semibold"><Coins className="h-4 w-4" /></div>
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{currency.code} · {currency.name}</p>
                      <p className="mt-1 text-xs text-muted-foreground">{currency.symbol}{currency.isBase ? ` · ${t("baseCurrency")}` : ""}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button size="sm" variant="outline" onClick={() => beginEdit(currency)}><Pencil className="mr-1.5 h-3.5 w-3.5" />{t("edit")}</Button>
                    <Button size="icon" variant="ghost" disabled={currency.isBase || deleteMutation.isPending} onClick={() => void handleDelete(currency)} aria-label={t("delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="py-8 text-center text-sm text-muted-foreground">{t("empty")}</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
