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
  const [form, setForm] = useState<SavePortfolioDto>({ name: "", description: "", ownerId: "", status: "Active", currency: "USD", startDate: "", endDate: "", strategicObjective: "", priority: "" });
  const [error, setError] = useState("");
  const users = useUserLookup("portfolio.manager");
  const { data: currencies } = useCurrencies();
  const create = useCreatePortfolio();
  const update = useUpdatePortfolio();

  useEffect(() => {
    const status: SavePortfolioDto["status"] = portfolio?.status === "Archived" ? "Archived" : "Active";
    const emptyGuid = "00000000-0000-0000-0000-000000000000";
    const ownerId = portfolio?.ownerId && portfolio.ownerId !== emptyGuid ? portfolio.ownerId : "";
    setForm(portfolio
      ? { name: portfolio.name, description: portfolio.description, ownerId, status, currency: portfolio.currency || "USD", startDate: portfolio.startDate?.slice(0, 10) ?? "", endDate: portfolio.endDate?.slice(0, 10) ?? "", strategicObjective: portfolio.strategicObjective ?? "", priority: portfolio.priority ?? "" }
      : { name: "", description: "", ownerId: "", status: "Active", currency: "USD", startDate: "", endDate: "", strategicObjective: "", priority: "" });
    setError("");
  }, [open, portfolio]);

  const options = (users.data || []).map((x) => ({ value: x.id, label: `${x.fullName} · ${x.email}` }));
  const currencyOptions = (currencies || []).map((c) => ({ value: c.code, label: `${c.code} - ${c.name}` }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim()) {
      setError("Name is required.");
      return;
    }
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setError("End date must not be before start date");
      return;
    }
    try {
      const payload: SavePortfolioDto = { ...form, ownerId: form.ownerId || undefined, currency: form.currency || "USD", startDate: form.startDate || undefined, endDate: form.endDate || undefined, strategicObjective: form.strategicObjective?.trim() ? form.strategicObjective.trim() : undefined, priority: form.priority?.trim() ? form.priority.trim() : undefined };
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
          <div className="grid grid-cols-2 gap-4">
            <div><Label>Start Date</Label><Input className="mt-2" type="date" value={form.startDate ?? ""} onChange={(e) => setForm((x) => ({ ...x, startDate: e.target.value }))} /></div>
            <div><Label>End Date</Label><Input className="mt-2" type="date" value={form.endDate ?? ""} onChange={(e) => setForm((x) => ({ ...x, endDate: e.target.value }))} /></div>
          </div>
          <div><Label>Strategic Objective</Label><Textarea className="mt-2" value={form.strategicObjective ?? ""} onChange={(e) => setForm((x) => ({ ...x, strategicObjective: e.target.value }))} /></div>
          <div><Label>Priority</Label><Input className="mt-2" value={form.priority ?? ""} onChange={(e) => setForm((x) => ({ ...x, priority: e.target.value }))} /></div>
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
