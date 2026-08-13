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
import { PortfolioDto, SavePortfolioDto, useCreatePortfolio, useUpdatePortfolio } from "@/hooks/portfolios/usePortfolios";

export function PortfolioSheet({ open, onOpenChange, portfolio }: { open: boolean; onOpenChange: (open: boolean) => void; portfolio?: PortfolioDto }) {
  const [form, setForm] = useState<SavePortfolioDto>({ name: "", description: "", ownerId: "", status: "Active" });
  const [error, setError] = useState("");
  const users = useUserLookup("portfolio.manager");
  const create = useCreatePortfolio();
  const update = useUpdatePortfolio();

  useEffect(() => {
    const status: SavePortfolioDto["status"] = portfolio?.status === "Archived" ? "Archived" : "Active";
    setForm(portfolio
      ? { name: portfolio.name, description: portfolio.description, ownerId: portfolio.ownerId, status }
      : { name: "", description: "", ownerId: "", status: "Active" });
    setError("");
  }, [open, portfolio]);

  const options = (users.data || []).map((x) => ({ value: x.id, label: `${x.fullName} · ${x.email}` }));

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim() || !form.ownerId) {
      setError("Name and Portfolio Manager are required.");
      return;
    }
    try {
      portfolio ? await update.mutateAsync({ id: portfolio.id, dto: form }) : await create.mutateAsync({ dto: form });
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
