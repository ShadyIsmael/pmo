import * as React from "react";
import { AlertTriangle, Check, Pencil, Plus, RotateCcw, Send, Trash2, X, FileText, Clock } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardHeader } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { formatDate } from "@lib/utils";
import { useProjectBudgets, useBudgetRevisionsByProject, useBudgetRevisionMutations, type BudgetRevisionDto } from "@features/finance/api/finance";

function money(value: number | null | undefined, currency: string, formatCurrency: (value: number, currency?: string) => string) {
  return typeof value === "number" && Number.isFinite(value) ? formatCurrency(value, currency) : "—";
}

export function BudgetRevisionSection({ projectId, currency, originalApprovedBudget, currentApprovedBudget, enabled, canManage }: { projectId: string; currency: string; originalApprovedBudget?: number | null; currentApprovedBudget?: number | null; enabled: boolean; canManage: boolean }) {
  const { t, formatCurrency } = useI18n();
  const confirm = useConfirm();
  const tr = (key: string, values?: Record<string, string | number>) => t(`projects.budgetRevisions.${key}`, values);
  const revisionsQuery = useBudgetRevisionsByProject(projectId, { page: 1, pageSize: 5 }, enabled);
  const revisions = revisionsQuery.data?.items ?? [];
  const mutations = useBudgetRevisionMutations(projectId);
  const budgetsQuery = useProjectBudgets(projectId, enabled);
  const budgetItems = React.useMemo(() => budgetsQuery.data?.items ?? [], [budgetsQuery.data?.items]);
  const budgetNameMap = React.useMemo(() => Object.fromEntries(budgetItems.map((b) => [b.id, b.name])), [budgetItems]);

  const [createOpen, setCreateOpen] = React.useState(false);
  const [editingRevision, setEditingRevision] = React.useState<BudgetRevisionDto | null>(null);
  const [reviewRevision, setReviewRevision] = React.useState<BudgetRevisionDto | null>(null);
  const [reviewMode, setReviewMode] = React.useState<"approve" | "reject">("approve");
  const [reviewComment, setReviewComment] = React.useState("");
  const [historyOpen, setHistoryOpen] = React.useState(false);
  const [historyPage, setHistoryPage] = React.useState(1);
  const historyQuery = useBudgetRevisionsByProject(projectId, { page: historyPage, pageSize: 20 }, historyOpen && enabled);
  const [createForm, setCreateForm] = React.useState({ budgetId: "", proposedApprovedAmount: 0, reason: "", justification: "" });
  const [actionError, setActionError] = React.useState<string | null>(null);

  const pendingRevision = revisions.find((r) => r.status === "Draft" || r.status === "Submitted");
  const isMutating = mutations.create.isPending || mutations.update.isPending || mutations.submit.isPending || mutations.approve.isPending || mutations.reject.isPending || mutations.cancel.isPending || mutations.delete.isPending;

  const openCreate = () => { setEditingRevision(null); setCreateForm({ budgetId: "", proposedApprovedAmount: 0, reason: "", justification: "" }); setActionError(null); setCreateOpen(true); };
  const openEdit = (rev: BudgetRevisionDto) => { setEditingRevision(rev); setCreateForm({ budgetId: rev.budgetId, proposedApprovedAmount: rev.proposedApprovedAmount, reason: rev.reason, justification: rev.justification ?? "" }); setActionError(null); setCreateOpen(true); };
  const openReview = (rev: BudgetRevisionDto, mode: "approve" | "reject") => { setReviewRevision(rev); setReviewMode(mode); setReviewComment(""); setActionError(null); };

  const submitCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.budgetId) return;
    setActionError(null);
    try {
      if (editingRevision) await mutations.update.mutateAsync({ id: editingRevision.id, body: { ...createForm, rowVersion: editingRevision.rowVersion } });
      else await mutations.create.mutateAsync(createForm);
      setCreateOpen(false);
    } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); }
  };
  const guard = async (message: string, destructive = false) => { if (!await confirm({ description: message, destructive })) return false; setActionError(null); return true; };
  const handleSubmit = async (rev: BudgetRevisionDto) => { if (!await guard(tr("confirmSubmit", { amount: money(rev.proposedApprovedAmount, rev.currency, formatCurrency) }))) return; try { await mutations.submit.mutateAsync({ id: rev.id, body: { rowVersion: rev.rowVersion } }); } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); } };
  const handleApprove = async () => { if (!reviewRevision) return; if (!await guard(tr("confirmApprove", { amount: money(reviewRevision.proposedApprovedAmount, reviewRevision.currency, formatCurrency) }))) return; try { await mutations.approve.mutateAsync({ id: reviewRevision.id, body: { reviewComment: reviewComment || undefined, rowVersion: reviewRevision.rowVersion } }); setReviewRevision(null); setReviewComment(""); } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); } };
  const handleReject = async () => { if (!reviewRevision || !reviewComment) return; if (!await guard(tr("confirmReject", { amount: money(reviewRevision.proposedApprovedAmount, reviewRevision.currency, formatCurrency) }), true)) return; try { await mutations.reject.mutateAsync({ id: reviewRevision.id, body: { reviewComment, rowVersion: reviewRevision.rowVersion } }); setReviewRevision(null); setReviewComment(""); } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); } };
  const handleCancel = async (rev: BudgetRevisionDto) => { if (!await guard(tr("confirmCancel", { amount: money(rev.proposedApprovedAmount, rev.currency, formatCurrency) }), true)) return; try { await mutations.cancel.mutateAsync(rev.id); } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); } };
  const handleDelete = async (rev: BudgetRevisionDto) => { if (!await guard(tr("confirmDelete", { amount: money(rev.proposedApprovedAmount, rev.currency, formatCurrency) }), true)) return; try { await mutations.delete.mutateAsync({ id: rev.id, rowVersion: rev.rowVersion }); } catch (err) { setActionError(err instanceof Error ? err.message : tr("revisionError")); } };

  if (!enabled) return null;
  if (revisionsQuery.isLoading) {
    return <Card className="shadow-none"><CardHeader><Skeleton className="h-5 w-40" /><Skeleton className="mt-1 h-3 w-64" /></CardHeader><CardContent className="space-y-3"><Skeleton className="h-16 w-full" /><Skeleton className="h-24 w-full" /></CardContent></Card>;
  }
  if (revisionsQuery.isError) {
    return <Card className="shadow-none"><CardHeader><h2 className="text-sm font-semibold">{tr("title")}</h2></CardHeader><CardContent><div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-6 text-center"><AlertTriangle className="h-5 w-5 text-destructive" /><p className="text-sm text-destructive">{t("common.error")}</p><Button variant="outline" size="sm" onClick={() => void revisionsQuery.refetch()}><RotateCcw className="me-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button></div></CardContent></Card>;
  }

  return (
    <Card className="shadow-none">
      <CardHeader className="flex !flex-row items-center justify-between gap-3">
        <div><h2 className="text-sm font-semibold">{tr("title")}</h2><p className="mt-1 text-xs text-muted-foreground">{tr("description")}</p></div>
        {canManage ? <Button size="sm" onClick={openCreate} disabled={isMutating}><Plus className="me-1.5 h-3.5 w-3.5" />{tr("proposeRevision")}</Button> : null}
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="rounded-md border border-primary/15 bg-primary/5 px-3 py-2 text-xs text-muted-foreground">{tr("pendingRevisionDescription")}</p>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-lg border bg-muted/20 p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{tr("originalApprovedBudget")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{money(originalApprovedBudget, currency, formatCurrency)}</p></div>
          <div className="rounded-lg border bg-muted/20 p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{tr("currentApprovedBudget")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{money(currentApprovedBudget, currency, formatCurrency)}</p></div>
        </div>
        {pendingRevision ? <div className="rounded-lg border border-warning/40 bg-warning/10 p-3"><div className="flex items-center gap-2 text-sm font-medium"><Clock className="h-4 w-4 text-warning" />{tr("pendingRevision")}</div><p className="mt-1 text-xs text-muted-foreground">{money(pendingRevision.proposedApprovedAmount, pendingRevision.currency, formatCurrency)} — {pendingRevision.reason}</p></div> : null}
        {actionError ? <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-xs text-destructive">{actionError}</div> : null}
        {revisions.length > 0 ? (
          <div className="space-y-2">
            <div className="flex items-center justify-between gap-2"><h3 className="text-xs font-semibold">{tr("revisionHistory")}</h3>{(revisionsQuery.data?.totalCount ?? 0) > 5 ? <Button type="button" size="sm" variant="outline" onClick={() => { setHistoryPage(1); setHistoryOpen(true); }}>{tr("viewAllCount", { count: revisionsQuery.data?.totalCount ?? 0 })}</Button> : null}</div>
            <div className="divide-y rounded-lg border">
              {revisions.map((rev) => (
                <div key={rev.id} className="flex items-center gap-3 px-3 py-2.5">
                  <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium tabular-nums">{money(rev.previousApprovedAmount, rev.currency, formatCurrency)} → {money(rev.proposedApprovedAmount, rev.currency, formatCurrency)}</span>
                      <Badge status={rev.status} className="text-[10px]">{tr(rev.status.toLowerCase())}</Badge>
                    </div>
                    <p className="mt-0.5 truncate text-xs text-muted-foreground">{budgetNameMap[rev.budgetId] ?? rev.budgetName} · {rev.reason} · {rev.requestedByName || "—"} · {formatDate(rev.requestedAt)}</p>
                    {(rev.justification || rev.reviewedByName || rev.reviewedAt || rev.reviewComment) ? <details className="mt-2 text-xs"><summary className="cursor-pointer text-muted-foreground hover:text-foreground">{tr("details")}</summary><dl className="mt-2 grid gap-x-4 gap-y-1 sm:grid-cols-2">{rev.justification ? <div className="sm:col-span-2"><dt className="font-medium text-muted-foreground">{tr("justification")}</dt><dd className="mt-0.5 whitespace-pre-wrap">{rev.justification}</dd></div> : null}{rev.reviewedByName || rev.reviewedBy ? <div><dt className="font-medium text-muted-foreground">{tr("reviewedBy")}</dt><dd className="mt-0.5">{rev.reviewedByName || rev.reviewedBy}</dd></div> : null}{rev.reviewedAt ? <div><dt className="font-medium text-muted-foreground">{tr("reviewedAt")}</dt><dd className="mt-0.5">{formatDate(rev.reviewedAt)}</dd></div> : null}{rev.reviewComment ? <div className="sm:col-span-2"><dt className="font-medium text-muted-foreground">{tr("reviewComment")}</dt><dd className="mt-0.5 whitespace-pre-wrap">{rev.reviewComment}</dd></div> : null}</dl></details> : null}
                  </div>
                  <div className="flex shrink-0 gap-1">
                    {canManage && rev.status === "Draft" ? <><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={t("common.edit")} onClick={() => openEdit(rev)} disabled={isMutating}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={tr("submit")} onClick={() => void handleSubmit(rev)} disabled={isMutating}><Send className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={t("common.delete")} onClick={() => void handleDelete(rev)} disabled={isMutating}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button></> : null}
                    {canManage && rev.status === "Submitted" ? <><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={tr("approve")} onClick={() => openReview(rev, "approve")} disabled={isMutating}><Check className="h-3.5 w-3.5 text-success" /></Button><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={tr("reject")} onClick={() => openReview(rev, "reject")} disabled={isMutating}><X className="h-3.5 w-3.5 text-destructive" /></Button><Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={tr("cancel")} onClick={() => void handleCancel(rev)} disabled={isMutating}><RotateCcw className="h-3.5 w-3.5" /></Button></> : null}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="rounded-lg border border-dashed p-6 text-center"><p className="text-sm font-medium">{tr("noRevisions")}</p><p className="mt-1 text-xs text-muted-foreground">{tr("noRevisionsDescription")}</p></div>
        )}

        <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={editingRevision ? tr("editDraft") : tr("proposeRevision")} description={tr("description")}>
          <form onSubmit={(e) => void submitCreate(e)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4">
            <div><Label>{tr("budgetLine")}</Label><select required value={createForm.budgetId} onChange={(e) => setCreateForm({ ...createForm, budgetId: e.target.value })} className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="">{tr("selectBudgetLine")}</option>{budgetItems.map((b) => <option key={b.id} value={b.id}>{b.name} — {money(b.approvedAmount, b.currency, formatCurrency)}</option>)}</select></div>
            <div><Label>{tr("proposedAmount")}</Label><Input type="number" required min="0" step="0.01" value={createForm.proposedApprovedAmount} onChange={(e) => setCreateForm({ ...createForm, proposedApprovedAmount: Number(e.target.value) })} /></div>
            <div><Label>{tr("reason")}</Label><Input required value={createForm.reason} onChange={(e) => setCreateForm({ ...createForm, reason: e.target.value })} /></div>
            <div><Label>{tr("justification")}</Label><Textarea value={createForm.justification} onChange={(e) => setCreateForm({ ...createForm, justification: e.target.value })} /></div>
            {actionError ? <p className="text-xs text-destructive">{actionError}</p> : null}
          </SideSheetBody><SideSheetFooter><Button type="submit" disabled={mutations.create.isPending || mutations.update.isPending || !createForm.budgetId}>{t("common.save")}</Button></SideSheetFooter></form>
        </SideSheet>

        <SideSheet open={Boolean(reviewRevision)} onOpenChange={(next) => { if (!next) setReviewRevision(null); }} title={reviewMode === "approve" ? tr("approve") : tr("reject")} description={reviewRevision?.reason}>
          <SideSheetBody className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="rounded-lg border p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{tr("previousApproved")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{money(reviewRevision?.previousApprovedAmount, currency, formatCurrency)}</p></div>
              <div className="rounded-lg border p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{tr("proposedAmount")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{money(reviewRevision?.proposedApprovedAmount, currency, formatCurrency)}</p></div>
            </div>
            <div><Label>{tr("reviewComment")}</Label><Textarea value={reviewComment} onChange={(e) => setReviewComment(e.target.value)} placeholder={reviewMode === "reject" ? tr("rejectReasonRequired") : tr("reviewCommentOptional")} /></div>
            {actionError ? <p className="text-xs text-destructive">{actionError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter><Button onClick={handleApprove} disabled={mutations.approve.isPending || mutations.reject.isPending} className="flex-1"><Check className="me-1.5 h-4 w-4" />{tr("approve")}</Button><Button variant="destructive" onClick={handleReject} disabled={mutations.reject.isPending || mutations.approve.isPending || !reviewComment} className="flex-1"><X className="me-1.5 h-4 w-4" />{tr("reject")}</Button></SideSheetFooter>
        </SideSheet>

        <SideSheet open={historyOpen} onOpenChange={setHistoryOpen} title={tr("revisionHistory")} description={tr("description")} size="xl">
          <SideSheetBody>
            <p className="text-xs text-muted-foreground">{tr("totalRecords", { count: historyQuery.data?.totalCount ?? revisionsQuery.data?.totalCount ?? 0 })}</p>
            {historyQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /></div>
            : historyQuery.isError ? <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-center"><AlertTriangle className="h-7 w-7 text-destructive/70" /><p role="alert" className="text-sm text-destructive">{t("common.error")}</p><Button variant="outline" size="sm" onClick={() => void historyQuery.refetch()}><RotateCcw className="me-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button></div>
            : historyQuery.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{tr("budgetLine")}</TableHead><TableHead className="text-end">{tr("previousApproved")}</TableHead><TableHead className="text-end">{tr("proposedAmount")}</TableHead><TableHead>{tr("status")}</TableHead><TableHead>{tr("requestedBy")} / {tr("requestedAt")}</TableHead><TableHead>{tr("details")}</TableHead></TableRow></TableHeader><TableBody>{historyQuery.data.items.map((rev) => <TableRow key={rev.id}><TableCell>{budgetNameMap[rev.budgetId] ?? rev.budgetName}</TableCell><TableCell className="text-end tabular-nums">{money(rev.previousApprovedAmount, rev.currency, formatCurrency)}</TableCell><TableCell className="text-end tabular-nums">{money(rev.proposedApprovedAmount, rev.currency, formatCurrency)}</TableCell><TableCell><Badge status={rev.status} className="text-[10px]">{tr(rev.status.toLowerCase())}</Badge></TableCell><TableCell className="whitespace-nowrap text-xs">{rev.requestedByName || "—"}<br />{formatDate(rev.requestedAt)}</TableCell><TableCell className="max-w-xs text-xs text-muted-foreground">{rev.reason}</TableCell></TableRow>)}</TableBody></Table></div>
            : <div className="flex min-h-40 items-center justify-center"><p className="text-sm text-muted-foreground">{tr("noRevisions")}</p></div>}
          </SideSheetBody>
          {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.totalPages ?? 0) > 1 ? <SideSheetFooter><div className="w-full"><Pagination page={historyPage} totalPages={historyQuery.data?.totalPages ?? 1} total={historyQuery.data?.totalCount} pageSize={20} onChange={setHistoryPage} /></div></SideSheetFooter> : null}
        </SideSheet>
      </CardContent>
    </Card>
  );
}
