"use client";

import { useEffect, useState } from "react";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useUserLookup, usePortfolioLookup } from "@/hooks/projects/useProjectLookups";
import { useCurrencies } from "@/hooks/useCurrencies";
import { ProgramDto, SaveProgramDto, useCreateProgram, useUpdateProgram } from "@/hooks/portfolios/usePrograms";

const STATUSES = ["Planning", "Active", "On Hold", "Completed", "Archived"] as const;
const HEALTHS = ["Green", "Amber", "Red", "Blue"] as const;

export function ProgramSheet({ open, onOpenChange, program }: { open: boolean; onOpenChange: (open: boolean) => void; program?: ProgramDto }) {
  const [form, setForm] = useState<SaveProgramDto>({ name: "", description: "", portfolioId: "", programManagerId: "", status: "Planning", startDate: "", endDate: "", currency: "USD", budget: undefined, health: undefined });
  const [error, setError] = useState("");
  const users = useUserLookup();
  const portfolios = usePortfolioLookup();
  const { data: currencies } = useCurrencies();
  const create = useCreateProgram();
  const update = useUpdateProgram();

  useEffect(() => {
    setForm(program
      ? {
          name: program.name,
          description: program.description || "",
          portfolioId: program.portfolioId,
          programManagerId: program.programManagerId || "",
          status: program.status || "Planning",
          startDate: program.startDate ? program.startDate.substring(0, 10) : "",
          endDate: program.endDate ? program.endDate.substring(0, 10) : "",
          currency: program.currency || "USD",
          budget: program.budget,
          health: program.health,
          rowVersion: program.rowVersion,
        }
      : { name: "", description: "", portfolioId: "", programManagerId: "", status: "Planning", startDate: "", endDate: "", currency: "USD", budget: undefined, health: undefined });
    setError("");
  }, [open, program]);

  const userOptions = (users.data || []).map((x) => ({ value: x.id, label: `${x.fullName} · ${x.email}` }));
  const portfolioOptions = (portfolios.data?.items || []).map((x) => ({ value: x.id, label: `${x.name}${x.code ? ` (${x.code})` : ""}` }));
  const currencyOptions = (currencies || []).map((c) => ({ value: c.code, label: `${c.code} - ${c.name}` }));

  const portfolioCurrencyMap = (portfolios.data?.items || []).reduce<Record<string, string>>((map, p) => { if (p.currency) map[p.id] = p.currency; return map; }, {});

  function handlePortfolioChange(portfolioId: string) {
    setForm((x) => {
      const portfolioCurrency = portfolioCurrencyMap[portfolioId] || "USD";
      const isCreate = !program;
      return { ...x, portfolioId, currency: isCreate ? portfolioCurrency : x.currency };
    });
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim()) { setError("Name is required."); return; }
    if (!form.portfolioId) { setError("Portfolio is required."); return; }
    try {
      const payload: SaveProgramDto = {
        ...form,
        name: form.name.trim(),
        description: form.description?.trim() || undefined,
        programManagerId: form.programManagerId || undefined,
        startDate: form.startDate || undefined,
        endDate: form.endDate || undefined,
        currency: form.currency || "USD",
        budget: form.budget || undefined,
        health: form.health || undefined,
      };
      program ? await update.mutateAsync({ id: program.id, dto: payload }) : await create.mutateAsync(payload);
      onOpenChange(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unable to save program");
    }
  }

  return (
    <FormSheet open={open} onOpenChange={onOpenChange} size="xl" title={program ? "Edit Program" : "Create Program"} description="A program groups related projects for strategic alignment and governance.">
      <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col overflow-hidden">
        <FormSheetBody className="space-y-4">
          {error && <p className="text-sm text-destructive">{error}</p>}
          <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(14rem,0.55fr)]">
            <div>
              <Label>Name</Label>
              <Input className="mt-2" value={form.name} onChange={(e) => setForm((x) => ({ ...x, name: e.target.value }))} required />
            </div>
            <div>
              <Label>Code</Label>
              <p className="mt-2 flex min-h-10 items-center rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{program?.code || "Generated after creation (PGM-0001 format)"}</p>
            </div>
          </div>
          <div>
            <Label>Description</Label>
            <Textarea className="mt-2" value={form.description || ""} onChange={(e) => setForm((x) => ({ ...x, description: e.target.value }))} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label>Portfolio</Label>
              <SearchableSelect
                className="mt-2"
                options={portfolioOptions}
                value={form.portfolioId}
                onValueChange={handlePortfolioChange}
                placeholder="Select portfolio"
                searchPlaceholder="Search portfolios..."
                loading={portfolios.isLoading}
                emptyText="No active portfolios found"
              />
            </div>
            <div>
              <Label>Program Manager</Label>
              <SearchableSelect
                className="mt-2"
                options={userOptions}
                value={form.programManagerId || ""}
                onValueChange={(programManagerId) => setForm((x) => ({ ...x, programManagerId }))}
                placeholder="Select program manager"
                searchPlaceholder="Search managers..."
                loading={users.isLoading}
                emptyText="No active users found"
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <Label>Start Date</Label>
              <Input className="mt-2" type="date" value={form.startDate || ""} onChange={(e) => setForm((x) => ({ ...x, startDate: e.target.value }))} />
            </div>
            <div>
              <Label>End Date</Label>
              <Input className="mt-2" type="date" value={form.endDate || ""} onChange={(e) => setForm((x) => ({ ...x, endDate: e.target.value }))} />
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label>Currency</Label>
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
              <p className="mt-1 text-xs text-muted-foreground">Defaults from the selected Portfolio; you can override it.</p>
            </div>
            <div>
              <Label>Budget</Label>
              <Input className="mt-2" type="number" step="0.01" min="0" value={form.budget ?? ""} onChange={(e) => setForm((x) => ({ ...x, budget: e.target.value ? parseFloat(e.target.value) : undefined }))} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <Label>Status</Label>
              <div className="mt-2">
                <Select value={form.status} onValueChange={(status) => setForm((x) => ({ ...x, status }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label>Health</Label>
              <div className="mt-2">
                <Select value={form.health || ""} onValueChange={(health) => setForm((x) => ({ ...x, health: health || undefined }))}>
                  <SelectTrigger><SelectValue placeholder="Select health" /></SelectTrigger>
                  <SelectContent>
                    {HEALTHS.map((h) => <SelectItem key={h} value={h}>{h}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
        </FormSheetBody>
        <FormSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" disabled={create.isPending || update.isPending}>{create.isPending || update.isPending ? "Saving..." : "Save Program"}</Button>
        </FormSheetFooter>
      </form>
    </FormSheet>
  );
}
