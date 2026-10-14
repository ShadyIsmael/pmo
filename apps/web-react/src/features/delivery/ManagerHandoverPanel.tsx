import * as React from "react";
import { ArrowRightLeft, History, Loader2, Plus } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { getStatusTextClass } from "@lib/status-presentation";
import {
  useCancelManagerHandover,
  useCreateManagerHandover,
  useEligibleManagerHandoverManagers,
  useManagerAssignmentHistory,
  useManagerHandoverRequests,
  useManagerHandoverSnapshots,
  type ManagerHandoverScope,
} from "./api/managerHandovers";

interface ManagerHandoverPanelProps {
  scope: ManagerHandoverScope;
  entityId: string;
  entityName: string;
  currentManagerId?: string;
  currentManagerName?: string;
  cacheScope: string;
  canView: boolean;
  canManage: boolean;
  canMutate: boolean;
}

function localDateValue() {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

export function ManagerHandoverPanel({ scope, entityId, entityName, currentManagerId, currentManagerName, cacheScope, canView, canManage, canMutate }: ManagerHandoverPanelProps) {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const isProgram = scope === "programs";
  const entityLabel = t(isProgram ? "managerHandover.program" : "managerHandover.portfolio");
  const currentUserIsManager = Boolean(user?.id && currentManagerId && user.id === currentManagerId);
  const canRequest = canManage && canMutate && currentUserIsManager;
  const requests = useManagerHandoverRequests(scope, entityId, canView, cacheScope);
  const history = useManagerAssignmentHistory(scope, entityId, canView, cacheScope);
  const snapshots = useManagerHandoverSnapshots(scope, entityId, canView, cacheScope);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [managerId, setManagerId] = React.useState("");
  const [error, setError] = React.useState("");
  const [effectiveDate, setEffectiveDate] = React.useState(localDateValue);
  const eligibleManagers = useEligibleManagerHandoverManagers(scope, entityId, sheetOpen && canRequest, cacheScope);
  const create = useCreateManagerHandover(scope, entityId, cacheScope);
  const cancel = useCancelManagerHandover(scope, entityId, cacheScope);
  const items = requests.data ?? [];
  const pendingRequest = items.find((item) => item.status === "Pending");
  const managers = (eligibleManagers.data ?? []).map((manager) => ({
    value: manager.id,
    label: `${manager.fullName} · ${manager.managerTypes.map((type) => t(type === "PortfolioManager" ? "projects.handoverManagerTypePortfolio" : type === "ProgramManager" ? "projects.handoverManagerTypeProgram" : "projects.handoverManagerTypeProject")).join(" / ")} · ${manager.email}`,
    keywords: [manager.jobTitle ?? ""],
  }));

  React.useEffect(() => {
    if (!sheetOpen) return;
    setManagerId("");
    setEffectiveDate(localDateValue());
    setError("");
  }, [sheetOpen]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const reason = String(data.get("reason") ?? "").trim();
    if (!managerId || !effectiveDate || !reason) {
      setError(t("managerHandover.requiredFields"));
      return;
    }
    setError("");
    try {
      await create.mutateAsync({
        newManagerId: managerId,
        effectiveHandoverDate: effectiveDate,
        reason,
        notes: String(data.get("notes") ?? "").trim() || undefined,
      });
      setSheetOpen(false);
    } catch {
      setError(t("managerHandover.saveFailed"));
    }
  }

  async function cancelRequest(requestId: string) {
    if (!window.confirm(t("managerHandover.cancelConfirm"))) return;
    setError("");
    try {
      await cancel.mutateAsync(requestId);
    } catch {
      setError(t("managerHandover.cancelFailed"));
    }
  }

  const isLoading = requests.isLoading || history.isLoading || snapshots.isLoading;
  const hasError = requests.isError || history.isError || snapshots.isError;

  return <div className="space-y-4">
    {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
    {hasError ? <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{t("managerHandover.loadFailed")}</span><Button size="sm" variant="outline" onClick={() => { void requests.refetch(); void history.refetch(); void snapshots.refetch(); }}>{t("common.retry")}</Button></div> : null}

    {canView ? <>
      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div><CardTitle className="flex items-center gap-2 text-base"><ArrowRightLeft className="h-4 w-4" />{t("managerHandover.handover")}</CardTitle><p className="text-sm text-muted-foreground">{t("managerHandover.handoverDescription", { entity: entityLabel })}</p></div>
          {canRequest && !pendingRequest ? <Button size="sm" onClick={() => setSheetOpen(true)} disabled={requests.isLoading || requests.isError}><Plus className="me-1.5 h-4 w-4" />{t("managerHandover.request")}</Button> : null}
        </CardHeader>
        <CardContent className="p-0">
          {requests.isLoading ? <div className="flex justify-center py-10"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : items.length === 0 ? <p className="p-6 text-sm text-muted-foreground">{t("managerHandover.noRequests")}</p>
              : <div className="divide-y">{items.map((request) => <div key={request.id} className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0 space-y-1">
                  <p className="text-sm font-medium">{request.currentManagerName} → {request.newManagerName}</p>
                  <p className="text-xs text-muted-foreground">{t("managerHandover.effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("approvals.requestedBy")}: {request.requestedByName} · {formatDate(request.requestedAt)}</p>
                  <p className="text-sm">{request.reason}</p>
                  {request.notes ? <p className="text-xs text-muted-foreground">{t("managerHandover.notes")}: {request.notes}</p> : null}
                  {request.rejectionReason ? <p className="text-xs text-destructive">{t("projects.rejectionReason")}: {request.rejectionReason}</p> : null}
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <span className={`text-xs font-semibold ${getStatusTextClass(request.status)}`}>{t(`projects.handoverStatus.${request.status}`)}</span>
                  {canManage && canMutate && request.status === "Pending" && request.requestedBy === user?.id ? <Button size="sm" variant="outline" onClick={() => void cancelRequest(request.id)} disabled={cancel.isPending}>{cancel.isPending ? t("common.saving") : t("managerHandover.cancel")}</Button> : null}
                </div>
              </div>)}</div>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2 text-base"><History className="h-4 w-4" />{t("managerHandover.managerHistory")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {history.isLoading ? <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : (history.data?.length ?? 0) === 0 ? <p className="p-6 text-sm text-muted-foreground">{t("managerHandover.noHistory")}</p>
              : <div className="divide-y">{history.data?.map((assignment) => <div key={assignment.id} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                <div><p className="text-sm font-medium">{assignment.managerName}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(assignment.startDate)} – {assignment.endDate ? formatDate(assignment.endDate) : t("managerHandover.current")}</p>{assignment.handoverReason ? <p className="mt-1 text-xs text-muted-foreground">{assignment.handoverReason}</p> : null}</div>
                {assignment.isCurrent ? <span className={`w-fit text-xs font-semibold ${getStatusTextClass("Active")}`}>{t("managerHandover.current")}</span> : null}
              </div>)}</div>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">{t("managerHandover.snapshots")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {snapshots.isLoading ? <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : (snapshots.data?.length ?? 0) === 0 ? <p className="p-6 text-sm text-muted-foreground">{t("managerHandover.noSnapshots")}</p>
              : <div className="divide-y">{snapshots.data?.map((snapshot) => <div key={snapshot.id} className="space-y-1 px-4 py-3">
                <p className="text-sm font-medium">{snapshot.name}</p>
                <p className="text-xs text-muted-foreground">{formatDate(snapshot.snapshotDate)} · {snapshot.previousManagerName} → {snapshot.newManagerName}</p>
                <p className="text-xs text-muted-foreground">{snapshot.handoverReason}</p>
              </div>)}</div>}
        </CardContent>
      </Card>
    </> : <Card><CardContent className="flex flex-col items-start gap-3 p-6 text-sm text-muted-foreground"><span>{isLoading ? t("common.loading") : t("managerHandover.viewPermissionRequired")}</span>{canRequest ? <Button size="sm" onClick={() => setSheetOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("managerHandover.request")}</Button> : null}</CardContent></Card>}

    <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={t("managerHandover.requestTitle")} description={t("managerHandover.requestDescription", { entity: entityLabel })}>
      <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
          <div><Label>{t("managerHandover.entity")}</Label><Input className="mt-1" value={entityName} readOnly /></div>
          <div><Label>{t("managerHandover.currentManager")}</Label><Input className="mt-1" value={currentManagerName || "—"} readOnly /></div>
          <div><Label>{t("managerHandover.newManager")} *</Label><div className="mt-1"><SearchableSelect options={managers} value={managerId} onValueChange={setManagerId} placeholder={t("managerHandover.selectManager")} searchPlaceholder={t("managerHandover.searchManager")} loading={eligibleManagers.isLoading} loadingText={t("projects.loadingManagers")} emptyText={t("managerHandover.noManagers")} disabled={eligibleManagers.isError} /></div>
            {eligibleManagers.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("managerHandover.loadFailed")}</p> : null}
            {!eligibleManagers.isLoading && !eligibleManagers.isError && managers.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}
          </div>
          <div><Label htmlFor="manager-handover-effective-date">{t("managerHandover.effectiveDate")} *</Label><Input id="manager-handover-effective-date" className="mt-1" type="date" value={effectiveDate} onChange={(event) => setEffectiveDate(event.target.value)} required /></div>
          <div><Label htmlFor="manager-handover-reason">{t("managerHandover.reason")} *</Label><Textarea id="manager-handover-reason" className="mt-1" name="reason" maxLength={500} required placeholder={t("managerHandover.reasonPlaceholder")} /></div>
          <div><Label htmlFor="manager-handover-notes">{t("managerHandover.notes")}</Label><Textarea id="manager-handover-notes" className="mt-1" name="notes" rows={3} maxLength={2000} placeholder={t("managerHandover.notesPlaceholder")} /></div>
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => setSheetOpen(false)} disabled={create.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={create.isPending || eligibleManagers.isLoading || eligibleManagers.isError || !managerId || Boolean(pendingRequest)}>{create.isPending ? t("projects.submitting") : t("managerHandover.submit")}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  </div>;
}
