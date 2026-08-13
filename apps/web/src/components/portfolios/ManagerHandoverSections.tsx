"use client";

import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslations } from "next-intl";
import { AlertCircle, CheckCircle2, Clock3, FileArchive, History, Loader2, UserRoundCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Input } from "@/components/ui/input";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Textarea } from "@/components/ui/textarea";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import { formatDate } from "@/lib/utils";
import {
  useCancelManagerHandover,
  useCreateManagerHandover,
  useManagerHandoverRequests,
  useManagerHistory,
  useManagerSnapshots,
  type ManagerHandoverEntity,
} from "@/hooks/portfolios/useManagerHandovers";

interface ManagerHandoverSectionsProps {
  type: ManagerHandoverEntity;
  entityId: string;
  entityName: string;
  currentManagerId?: string;
  currentManagerName?: string;
  startDate?: string;
  endDate?: string;
}

type FormData = { newManagerId: string; effectiveHandoverDate: string; reason: string; notes?: string };

export function ManagerHandoverSections(props: ManagerHandoverSectionsProps) {
  const t = useTranslations("managerHandover");
  const [open, setOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const requests = useManagerHandoverRequests(props.type, props.entityId);
  const create = useCreateManagerHandover(props.type, props.entityId);
  const cancel = useCancelManagerHandover(props.type);
  const role = props.type === "programs" ? "project.manager" : "portfolio.manager";
  const users = useUserLookup(role);
  const schema = z.object({
    newManagerId: z.string().min(1, t("required")),
    effectiveHandoverDate: z.string().min(1, t("required")),
    reason: z.string().trim().min(1, t("required")).max(500),
    notes: z.string().trim().max(2000).optional(),
  });
  const form = useForm<FormData>({ resolver: zodResolver(schema), defaultValues: { newManagerId: "", effectiveHandoverDate: "", reason: "", notes: "" } });
  const pending = requests.data?.find((request) => request.status === "Pending");
  const options = useMemo(() => (users.data ?? []).filter((user) => user.id !== props.currentManagerId).map((user) => ({ value: user.id, label: `${user.fullName} · ${user.email}`, keywords: [user.jobTitle ?? ""] })), [props.currentManagerId, users.data]);
  const currentManagerName = props.currentManagerName ?? users.data?.find((user) => user.id === props.currentManagerId)?.fullName;

  useEffect(() => {
    if (!open) return;
    form.reset({ newManagerId: "", effectiveHandoverDate: "", reason: "", notes: "" });
    setErrorMessage(null);
  }, [form, open]);

  const submit = async (data: FormData) => {
    setErrorMessage(null);
    try {
      await create.mutateAsync({ ...data, notes: data.notes || undefined });
      setOpen(false);
      form.reset();
    } catch (error: unknown) {
      setErrorMessage(error instanceof Error ? error.message : t("saveFailed"));
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex-row items-start justify-between gap-4 space-y-0">
          <div>
            <CardTitle className="flex items-center gap-2"><UserRoundCheck className="h-4 w-4" />{t("handover")}</CardTitle>
            <CardDescription>{t("handoverDescription", { entity: props.type === "programs" ? t("program") : t("portfolio") })}</CardDescription>
          </div>
          <Button type="button" onClick={() => setOpen(true)} disabled={!props.currentManagerId || !!pending}>{t("request")}</Button>
        </CardHeader>
        <CardContent>
          {pending ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-amber-300/50 bg-amber-50/40 p-3 text-sm dark:bg-amber-950/20">
              <div className="flex items-start gap-2"><Clock3 className="mt-0.5 h-4 w-4 text-amber-700" /><span>{t("pendingDescription", { manager: pending.newManagerName, date: formatDate(pending.effectiveHandoverDate) })}</span></div>
              <Button type="button" variant="outline" size="sm" disabled={cancel.isPending} onClick={() => cancel.mutate(pending.id)}>{cancel.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : t("cancel")}</Button>
            </div>
          ) : requests.isLoading ? <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /> : <p className="text-sm text-muted-foreground">{t("noPending")}</p>}
        </CardContent>
      </Card>

      {errorMessage && <div className="flex items-center gap-2 rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive"><AlertCircle className="h-4 w-4" />{errorMessage}</div>}
      <ManagerHistorySection type={props.type} id={props.entityId} />
      <ManagerSnapshotsSection type={props.type} id={props.entityId} />

      <FormSheet open={open} onOpenChange={setOpen} title={t("requestTitle")} description={t("requestDescription", { entity: props.entityName })}>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(submit)} className="flex min-h-0 flex-1 flex-col">
            <FormSheetBody className="space-y-5">
              <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1.5"><FormLabel>{t("entity")}</FormLabel><Input value={props.entityName} readOnly disabled /></div><div className="space-y-1.5"><FormLabel>{t("currentManager")}</FormLabel><Input value={currentManagerName ?? "—"} readOnly disabled /></div></div>
              <FormField control={form.control} name="newManagerId" render={({ field }) => <FormItem><FormLabel>{t("newManager")}</FormLabel><FormControl><SearchableSelect options={options} value={field.value} onValueChange={field.onChange} placeholder={t("selectManager")} searchPlaceholder={t("searchManager")} loading={users.isLoading} emptyText={t("noManagers")} /></FormControl><FormMessage /></FormItem>} />
              <FormField control={form.control} name="effectiveHandoverDate" render={({ field }) => <FormItem><FormLabel>{t("effectiveDate")}</FormLabel><FormControl><Input type="date" min={props.startDate?.slice(0, 10)} max={props.endDate?.slice(0, 10)} {...field} /></FormControl><FormMessage /></FormItem>} />
              <FormField control={form.control} name="reason" render={({ field }) => <FormItem><FormLabel>{t("reason")}</FormLabel><FormControl><Input {...field} placeholder={t("reasonPlaceholder")} /></FormControl><FormMessage /></FormItem>} />
              <FormField control={form.control} name="notes" render={({ field }) => <FormItem><FormLabel>{t("notes")}</FormLabel><FormControl><Textarea rows={3} {...field} placeholder={t("notesPlaceholder")} /></FormControl><FormMessage /></FormItem>} />
            </FormSheetBody>
            <FormSheetFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("close")}</Button><Button type="submit" disabled={create.isPending}>{create.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}{t("submit")}</Button></FormSheetFooter>
          </form>
        </Form>
      </FormSheet>
    </div>
  );
}

function ManagerHistorySection({ type, id }: { type: ManagerHandoverEntity; id: string }) {
  const t = useTranslations("managerHandover");
  const history = useManagerHistory(type, id);
  return <Card><CardHeader><CardTitle className="flex items-center gap-2"><History className="h-4 w-4" />{t("managerHistory")}</CardTitle></CardHeader><CardContent>{history.isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : history.data?.length ? <div className="space-y-3">{history.data.map((item) => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 border-b pb-3 last:border-0 last:pb-0"><div><p className="text-sm font-medium">{item.managerName}</p><p className="text-xs text-muted-foreground">{formatDate(item.startDate)}{item.endDate ? ` – ${formatDate(item.endDate)}` : ` · ${t("current")}`}</p></div>{item.isCurrent ? <Badge><CheckCircle2 className="mr-1 h-3 w-3" />{t("current")}</Badge> : null}</div>)}</div> : <p className="text-sm text-muted-foreground">{t("noHistory")}</p>}</CardContent></Card>;
}

function ManagerSnapshotsSection({ type, id }: { type: ManagerHandoverEntity; id: string }) {
  const t = useTranslations("managerHandover");
  const snapshots = useManagerSnapshots(type, id);
  return <Card><CardHeader><CardTitle className="flex items-center gap-2"><FileArchive className="h-4 w-4" />{t("snapshots")}</CardTitle></CardHeader><CardContent>{snapshots.isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : snapshots.data?.length ? <div className="space-y-3">{snapshots.data.map((item) => <div key={item.id} className="rounded-md border p-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-medium">{item.name}</p><Badge variant="outline">#{item.snapshotNumber}</Badge></div><p className="mt-1 text-xs text-muted-foreground">{formatDate(item.snapshotDate)} · {item.previousManagerName} → {item.newManagerName}</p><p className="mt-2 text-sm">{item.handoverReason}</p></div>)}</div> : <p className="text-sm text-muted-foreground">{t("noSnapshots")}</p>}</CardContent></Card>;
}
