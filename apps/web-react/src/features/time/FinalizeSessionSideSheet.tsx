import * as React from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useEligibleProjects, useFinalizeTimer, type FinalizeTimerInput, type TimerSessionDto } from "@api/timer";
import { toast } from "@hooks/use-toast";

interface FinalizeSessionSideSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  session: TimerSessionDto | null;
}

export function FinalizeSessionSideSheet({ open, onOpenChange, session }: FinalizeSessionSideSheetProps) {
  const { t } = useI18n();
  const finalize = useFinalizeTimer();
  const eligible = useEligibleProjects(session?.id ?? "", session?.status);
  const [form, setForm] = React.useState<FinalizeTimerInput>({ projectId: "", description: "" });

  React.useEffect(() => {
    if (open) setForm({ projectId: "", description: "" });
  }, [open ]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!session || !form.projectId) return;
    try {
      await finalize.mutateAsync({ id: session.id, input: form });
      toast({ title: t("timeTracking.finalized") });
      onOpenChange(false);
    } catch (err) {
      toast({ title: t("common.error"), description: err instanceof Error ? err.message : undefined, variant: "destructive" });
    }
  }

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("timeTracking.finalizeTitle")} description={t("timeTracking.finalizeDescription")}>
      <form onSubmit={(e) => void submit(e)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="finalize-project">{t("timeTracking.project")}</Label>
            <select
              id="finalize-project"
              className="input-field"
              value={form.projectId}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setForm((f) => ({ ...f, projectId: e.target.value }))}
              required
            >
              <option value="">{t("timeTracking.selectProject")}</option>
              {(eligible.data ?? []).map((p: { id: string; code: string; name: string }) => (
                <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
              ))}
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="finalize-description">{t("timeTracking.descriptionField")}</Label>
            <Textarea id="finalize-description" value={form.description ?? ""} onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setForm((f) => ({ ...f, description: e.target.value }))} />
          </div>
          {finalize.isError ? <p role="alert" className="text-sm text-destructive">{t("timeTracking.saveError")}</p> : null}
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={finalize.isPending || !form.projectId}>
            {finalize.isPending ? t("common.saving") : t("timeTracking.finalize")}
          </Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

export function useTimeEntryProjects(enabled = true) {
  return useQuery({
    queryKey: ["projects", "lookup", "time-entry"],
    queryFn: () => api.get<{ items: { id: string; code: string; name: string }[] }>(endpoints.projects.root, { page: 1, pageSize: 200 }),
    enabled,
    staleTime: 60_000,
  });
}
