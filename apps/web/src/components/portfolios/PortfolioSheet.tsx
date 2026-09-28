"use client";

import { useEffect, useState } from "react";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import { useCurrencies } from "@/hooks/useCurrencies";
import { PortfolioDto, SavePortfolioDto, useCreatePortfolio, useUpdatePortfolio } from "@/hooks/portfolios/usePortfolios";

export function PortfolioSheet({ open, onOpenChange, portfolio }: { open: boolean; onOpenChange: (open: boolean) => void; portfolio?: PortfolioDto }) {
  const [form, setForm] = useState<SavePortfolioDto>({ name: "", description: "", ownerId: "", status: "Active", currency: "USD" });
  const [error, setError] = useState("");
  const users = useUserLookup("portfolio.manager");
  const { data: currencies } = useCurrencies();
  const create = useCreatePortfolio();
  const update = useUpdatePortfolio();

  useEffect(() => {
    const status: SavePortfolioDto["status"] = portfolio?.status === "Archived" ? "Archived" : "Active";
    setForm(portfolio
      ? { name: portfolio.name, description: portfolio.description, ownerId: portfolio.ownerId, status, currency: portfolio.currency || "USD" }
      : { name: "", description: "", ownerId: "", status: "Active", currency: "USD" });
    setError("");
  }, [open, portfolio]);

  const options = (users.data || []).map((x) => ({ value: x.id, label: `${x.fullName} · ${x.email}` }));
  const currencyOptions = (currencies || []).map((c) => ({ value: c.code, label: `${c.code} - ${c.name}` }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim() || !form.ownerId) {
      setError("Name and Portfolio Manager are required.");
      return;
    }
    try {
      const payload: SavePortfolioDto = { ...form, currency: form.currency || "USD" };
      portfolio ? await update.mutateAsync({ id: portfolio.id, dto: payload }) : await create.mutateAsync({ dto: payload });
      onOpenChange(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unable to save portfolio");
    }
  }

  return (
    <FormSheet open={open} onOpenChange={onOpenChange} title={portfolio ? "Edit Portfolio" : "Create Portfolio"} description="A portfolio groups related projects for reporting and governance.">
      <form onSubmit={submit}>
        <FormSheetBody className="space-y-5">
          {error && <p className="text-sm text-destructive">{error}</p>}
          <div><Label>Name</Label><Input className="mt-2" value={form.name} onChange={(e) => setForm((x) => ({ ...x, name: e.target.value }))} required /></div>
          <div><Label>Code</Label><p className="mt-2 rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{portfolio?.code || "Generated after creation (PF-0001 format)"}</p></div>
          <div><Label>Description</Label><Textarea className="mt-2" value={form.description || ""} onChange={(e) => setForm((x) => ({ ...x, description: e.target.value }))} /></div>
          <div>
            <Label>Portfolio Manager</Label>
            <div className="mt-2"><SearchableSelect options={options} value={form.ownerId} onValueChange={(ownerId) => setForm((x) => ({ ...x, ownerId }))} placeholder="Select portfolio manager" searchPlaceholder="Search portfolio managers…" loading={users.isLoading} emptyText="No active portfolio managers found" /></div>
          </div>
          <div>
            <Label>Currency</Label>
            <div className="mt-2">
              {currencyOptions.length > 0 ? (
                <Select value={form.currency || "USD"} onValueChange={(currency) => setForm((x) => ({ ...x, currency }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((c) => <SelectItem key={c.value} value={c.value}>{c.label}</SelectItem>)}
                  </SelectContent>
                </Select>
              ) : (
                <Input value={form.currency || "USD"} onChange={(e) => setForm((x) => ({ ...x, currency: e.target.value.toUpperCase() }))} />
              )}
            </div>
          </div>
          <div>
            <Label>Status</Label>
            <div className="mt-2">
              <Select value={form.status} onValueChange={(status: "Active" | "Archived") => setForm((x) => ({ ...x, status }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent><SelectItem value="Active">Active</SelectItem><SelectItem value="Archived">Archived</SelectItem></SelectContent>
              </Select>
            </div>
          </div>
        </FormSheetBody>
        <FormSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button><Button type="submit" disabled={create.isPending || update.isPending}>{create.isPending || update.isPending ? "Saving…" : "Save Portfolio"}</Button></FormSheetFooter>
      </form>
    </FormSheet>
  );
}
