"use client";

import { useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import {
  Plus,
  Pencil,
  Send,
  Check,
  X,
  Trash2,
  Clock,
  FileText,
  AlertTriangle,
  RotateCcw,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import {
  useBudgetRevisionsByProject,
  useBudgetRevisionMutations,
  useProjectBudgets,
  type BudgetRevisionDto,
  type CreateBudgetRevisionDto,
} from "@/lib/api/costs";
import { formatCurrency, formatDate, isFiniteNumber } from "@/lib/utils";
import { TablePagination } from "@/components/ui/table-pagination";

type Props = {
  projectId: string;
  currency: string;
  originalApprovedBudget?: number | null;
  currentApprovedBudget?: number | null;
  enabled: boolean;
  canManage: boolean;
};

function money(value: number | null | undefined, currency: string) {
  return isFiniteNumber(value) ? formatCurrency(value, currency) : "\u2014";
}

function RevisionHistoryTableRow({ rev, budgetName, canManage, isMutating, onEdit, onSubmit, onReview, onCancel, onDelete, tr, common }: { rev: BudgetRevisionDto; budgetName: string; canManage: boolean; isMutating: boolean; onEdit: (_rev: BudgetRevisionDto) => void; onSubmit: (_rev: BudgetRevisionDto) => void; onReview: (_rev: BudgetRevisionDto, _mode: "approve" | "reject") => void; onCancel: (_rev: BudgetRevisionDto) => void; onDelete: (_rev: BudgetRevisionDto) => void; tr: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations> }) {
  const hasDetails = Boolean(rev.justification || rev.reviewedByName || rev.reviewedBy || rev.reviewedAt || rev.reviewComment);
  return <TableRow><TableCell><p className="font-medium">{budgetName}</p><p className="text-xs text-muted-foreground">{rev.reason}</p></TableCell><TableCell className="text-end tabular-nums">{money(rev.previousApprovedAmount, rev.currency)}</TableCell><TableCell className="text-end font-semibold tabular-nums">{money(rev.proposedApprovedAmount, rev.currency)}</TableCell><TableCell><Badge variant={statusVariant[rev.status] as never} className="text-[10px]">{tr(rev.status.toLowerCase() as never)}</Badge></TableCell><TableCell className="whitespace-nowrap text-xs">{rev.requestedByName || "—"}<br />{formatDate(rev.requestedAt)}</TableCell><TableCell className="max-w-xs text-xs">{hasDetails ? <details><summary className="cursor-pointer text-muted-foreground hover:text-foreground">{tr("details")}</summary><dl className="mt-2 space-y-1">{rev.justification ? <div><dt className="font-medium text-muted-foreground">{tr("justification")}</dt><dd className="whitespace-pre-wrap">{rev.justification}</dd></div> : null}{rev.reviewedByName || rev.reviewedBy ? <div><dt className="font-medium text-muted-foreground">{tr("reviewedBy")}</dt><dd>{rev.reviewedByName || rev.reviewedBy}</dd></div> : null}{rev.reviewedAt ? <div><dt className="font-medium text-muted-foreground">{tr("reviewedAt")}</dt><dd>{formatDate(rev.reviewedAt)}</dd></div> : null}{rev.reviewComment ? <div><dt className="font-medium text-muted-foreground">{tr("reviewComment")}</dt><dd className="whitespace-pre-wrap">{rev.reviewComment}</dd></div> : null}</dl></details> : <span className="text-muted-foreground">—</span>}</TableCell><TableCell><div className="nexus-table-actions flex justify-end gap-1">{canManage && rev.status === "Draft" ? <><Button type="button" size="icon" variant="ghost" aria-label={common("edit")} onClick={() => onEdit(rev)} disabled={isMutating}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={tr("submit")} onClick={() => onSubmit(rev)} disabled={isMutating}><Send className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={common("delete")} onClick={() => onDelete(rev)} disabled={isMutating}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button></> : null}{canManage && rev.status === "Submitted" ? <><Button type="button" size="icon" variant="ghost" aria-label={tr("approve")} onClick={() => onReview(rev, "approve")} disabled={isMutating}><Check className="h-3.5 w-3.5 text-green-600" /></Button><Button type="button" size="icon" variant="ghost" aria-label={tr("reject")} onClick={() => onReview(rev, "reject")} disabled={isMutating}><X className="h-3.5 w-3.5 text-destructive" /></Button><Button type="button" size="icon" variant="ghost" aria-label={tr("cancel")} onClick={() => onCancel(rev)} disabled={isMutating}><RotateCcw className="h-3.5 w-3.5" /></Button></> : null}</div></TableCell></TableRow>;
}

function RevisionPagination({ page, totalPages, previous, next, pageOf, onPageChange }: { page: number; totalPages: number; previous: string; next: string; pageOf: string; onPageChange: (_page: number) => void }) { return <TablePagination page={page} totalPages={totalPages} summary={pageOf} previousLabel={previous} nextLabel={next} onPageChange={onPageChange} />; }

const statusVariant: Record<string, string> = {
  Draft: "secondary",
  Submitted: "default",
  Approved: "status-green",
  Rejected: "destructive",
  Cancelled: "outline",
};

export function BudgetRevisionSection({
  projectId,
  currency,
  originalApprovedBudget,
  currentApprovedBudget,
  enabled,
  canManage,
}: Props) {
  const tr = useTranslations("projects.budgetRevisions");
  const common = useTranslations("common");

  const revisionsQuery = useBudgetRevisionsByProject(
    projectId,
    { page: 1, pageSize: 5 },
    enabled
  );
  const revisions = revisionsQuery.data?.items ?? [];
  const mutations = useBudgetRevisionMutations(projectId);
  const budgetsQuery = useProjectBudgets(projectId, enabled, { page: 1, pageSize: 5 });
  const budgetItems = budgetsQuery.data?.items ?? [];

  const budgetNameMap = useMemo(() => {
    const map: Record<string, string> = {};
    for (const b of budgetItems) map[b.id] = b.name;
    return map;
  }, [budgetItems]);

  const [createOpen, setCreateOpen] = useState(false);
  const [editingRevision, setEditingRevision] =
    useState<BudgetRevisionDto | null>(null);
  const [reviewRevision, setReviewRevision] =
    useState<BudgetRevisionDto | null>(null);
  const [reviewMode, setReviewMode] = useState<"approve" | "reject">("approve");
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const historyQuery = useBudgetRevisionsByProject(
    projectId,
    { page: historyPage, pageSize: 20 },
    historyOpen && enabled
  );
  const budgetSelectorQuery = useProjectBudgets(projectId, createOpen && enabled, { page: 1, pageSize: 200 });
  const budgetSelectorItems = budgetSelectorQuery.data?.items ?? budgetItems;

  const [createForm, setCreateForm] = useState<CreateBudgetRevisionDto>({
    budgetId: "",
    proposedApprovedAmount: 0,
    reason: "",
    justification: "",
  });
  const [reviewComment, setReviewComment] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const pendingRevision = revisions.find(
    (r) => r.status === "Draft" || r.status === "Submitted"
  );

  const isMutating =
    mutations.create.isPending ||
    mutations.update.isPending ||
    mutations.submit.isPending ||
    mutations.approve.isPending ||
    mutations.reject.isPending ||
    mutations.cancel.isPending ||
    mutations.delete.isPending;

  const openCreate = () => {
    setEditingRevision(null);
    setCreateForm({
      budgetId: "",
      proposedApprovedAmount: 0,
      reason: "",
      justification: "",
    });
    setActionError(null);
    setCreateOpen(true);
  };

  const openEdit = (rev: BudgetRevisionDto) => {
    setEditingRevision(rev);
    setCreateForm({
      budgetId: rev.budgetId,
      proposedApprovedAmount: rev.proposedApprovedAmount,
      reason: rev.reason,
      justification: rev.justification,
    });
    setActionError(null);
    setCreateOpen(true);
  };

  const openReview = (rev: BudgetRevisionDto, mode: "approve" | "reject") => {
    setReviewRevision(rev);
    setReviewMode(mode);
    setReviewComment("");
    setActionError(null);
  };

  const submitCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createForm.budgetId) return;
    setActionError(null);
    try {
      if (editingRevision) {
        await mutations.update.mutateAsync({
          id: editingRevision.id,
          body: {
            ...createForm,
            rowVersion: editingRevision.rowVersion,
          },
        });
      } else {
        await mutations.create.mutateAsync(createForm);
      }
      setCreateOpen(false);
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  const handleSubmit = async (rev: BudgetRevisionDto) => {
    if (
      !window.confirm(
        tr("confirmSubmit", { amount: money(rev.proposedApprovedAmount, rev.currency) })
      )
    )
      return;
    setActionError(null);
    try {
      await mutations.submit.mutateAsync({
        id: rev.id,
        body: { rowVersion: rev.rowVersion },
      });
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  const handleApprove = async () => {
    if (!reviewRevision) return;
    if (
      !window.confirm(
        tr("confirmApprove", {
          amount: money(reviewRevision.proposedApprovedAmount, reviewRevision.currency),
        })
      )
    )
      return;
    setActionError(null);
    try {
      await mutations.approve.mutateAsync({
        id: reviewRevision.id,
        body: {
          reviewComment: reviewComment || undefined,
          rowVersion: reviewRevision.rowVersion,
        },
      });
      setReviewRevision(null);
      setReviewComment("");
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  const handleReject = async () => {
    if (!reviewRevision || !reviewComment) return;
    if (
      !window.confirm(
        tr("confirmReject", {
          amount: money(reviewRevision.proposedApprovedAmount, reviewRevision.currency),
        })
      )
    )
      return;
    setActionError(null);
    try {
      await mutations.reject.mutateAsync({
        id: reviewRevision.id,
        body: { reviewComment, rowVersion: reviewRevision.rowVersion },
      });
      setReviewRevision(null);
      setReviewComment("");
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  const handleCancel = async (rev: BudgetRevisionDto) => {
    if (
      !window.confirm(
        tr("confirmCancel", {
          amount: money(rev.proposedApprovedAmount, rev.currency),
        })
      )
    )
      return;
    setActionError(null);
    try {
      await mutations.cancel.mutateAsync(rev.id);
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  const handleDelete = async (rev: BudgetRevisionDto) => {
    if (
      !window.confirm(
        tr("confirmDelete", {
          amount: money(rev.proposedApprovedAmount, rev.currency),
        })
      )
    )
      return;
    setActionError(null);
    try {
      await mutations.delete.mutateAsync({
        id: rev.id,
        rowVersion: rev.rowVersion,
      });
    } catch (err) {
      setActionError(
        err instanceof Error ? err.message : tr("revisionError")
      );
    }
  };

  if (!enabled) return null;

  if (revisionsQuery.isLoading) {
    return (
      <Card className="shadow-none">
        <CardHeader>
          <Skeleton className="h-5 w-40" />
          <Skeleton className="mt-1 h-3 w-64" />
        </CardHeader>
        <CardContent className="space-y-3">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-24 w-full" />
        </CardContent>
      </Card>
    );
  }

  if (revisionsQuery.isError) {
    return (
      <Card className="shadow-none">
        <CardHeader>
          <h2 className="text-sm font-semibold">{tr("title")}</h2>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-6 text-center">
            <AlertTriangle className="h-5 w-5 text-destructive" />
            <p className="text-sm text-destructive">{common("error")}</p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => revisionsQuery.refetch()}
            >
              <RotateCcw className="me-1.5 h-3.5 w-3.5" />
              {common("retry")}
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="shadow-none">
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">{tr("title")}</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {tr("description")}
          </p>
        </div>
        {canManage && (
          <Button size="sm" onClick={openCreate} disabled={isMutating}>
            <Plus className="me-1.5 h-3.5 w-3.5" />
            {tr("proposeRevision")}
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="rounded-md border border-primary/15 bg-primary/5 px-3 py-2 text-xs text-muted-foreground">
          {tr("pendingRevisionDescription")}
        </p>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="rounded-lg border bg-muted/20 p-3">
            <p className="text-[10px] uppercase tracking-wide text-muted-foreground">
              {tr("originalApprovedBudget")}
            </p>
            <p className="mt-1 text-lg font-semibold">
              {money(originalApprovedBudget, currency)}
            </p>
          </div>
          <div className="rounded-lg border bg-muted/20 p-3">
            <p className="text-[10px] uppercase tracking-wide text-muted-foreground">
              {tr("currentApprovedBudget")}
            </p>
            <p className="mt-1 text-lg font-semibold">
              {money(currentApprovedBudget, currency)}
            </p>
          </div>
        </div>

        {pendingRevision && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 dark:border-amber-800 dark:bg-amber-950/30">
            <div className="flex items-center gap-2 text-sm font-medium">
              <Clock className="h-4 w-4 text-amber-600" />
              {tr("pendingRevision")}
            </div>
            <p className="mt-1 text-xs text-muted-foreground">
              {money(pendingRevision.proposedApprovedAmount, pendingRevision.currency)}{" "}
              &mdash; {pendingRevision.reason}
            </p>
          </div>
        )}

        {actionError && (
          <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-xs text-destructive">
            {actionError}
          </div>
        )}

        {revisions.length > 0 ? (
          <div className="space-y-2">
            <div className="flex items-center justify-between gap-2"><h3 className="text-xs font-semibold">{tr("revisionHistory")}</h3>{(revisionsQuery.data?.totalCount ?? 0) > 5 ? <Button type="button" size="sm" variant="outline" onClick={() => { setHistoryPage(1); setHistoryOpen(true); }}>{tr("viewAllCount", { count: revisionsQuery.data?.totalCount ?? 0 })}</Button> : null}</div>
            <div className="divide-y rounded-lg border">
              {revisions.map((rev) => (
                <div key={rev.id} className="flex items-center gap-3 px-3 py-2.5">
                  <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">
                        {money(rev.previousApprovedAmount, rev.currency)}{" "}
                        &rarr; {money(rev.proposedApprovedAmount, rev.currency)}
                      </span>
                      <Badge
                        variant={statusVariant[rev.status] as never}
                        className="text-[10px]"
                      >
                        {tr(rev.status.toLowerCase() as never)}
                      </Badge>
                    </div>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      {budgetNameMap[rev.budgetId] ?? rev.budgetName} &middot;{" "}
                      {rev.reason} &middot; {rev.requestedByName || "\u2014"}{" "}
                      &middot; {formatDate(rev.requestedAt)}
                    </p>
                    {(rev.justification ||
                      rev.reviewedByName ||
                      rev.reviewedBy ||
                      rev.reviewedAt ||
                      rev.reviewComment) && (
                      <details className="mt-2 text-xs">
                        <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                          {tr("details")}
                        </summary>
                        <dl className="mt-2 grid gap-x-4 gap-y-1 sm:grid-cols-2">
                          {rev.justification && (
                            <div className="sm:col-span-2">
                              <dt className="font-medium text-muted-foreground">
                                {tr("justification")}
                              </dt>
                              <dd className="mt-0.5 whitespace-pre-wrap">
                                {rev.justification}
                              </dd>
                            </div>
                          )}
                          {(rev.reviewedByName || rev.reviewedBy) && (
                            <div>
                              <dt className="font-medium text-muted-foreground">
                                {tr("reviewedBy")}
                              </dt>
                              <dd className="mt-0.5">
                                {rev.reviewedByName || rev.reviewedBy}
                              </dd>
                            </div>
                          )}
                          {rev.reviewedAt && (
                            <div>
                              <dt className="font-medium text-muted-foreground">
                                {tr("reviewedAt")}
                              </dt>
                              <dd className="mt-0.5">{formatDate(rev.reviewedAt)}</dd>
                            </div>
                          )}
                          {rev.reviewComment && (
                            <div className="sm:col-span-2">
                              <dt className="font-medium text-muted-foreground">
                                {tr("reviewComment")}
                              </dt>
                              <dd className="mt-0.5 whitespace-pre-wrap">
                                {rev.reviewComment}
                              </dd>
                            </div>
                          )}
                        </dl>
                      </details>
                    )}
                  </div>
                  <div className="flex shrink-0 gap-1">
                    {canManage && rev.status === "Draft" && (
                      <>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => openEdit(rev)}
                              disabled={isMutating}
                            >
                              <Pencil className="h-3.5 w-3.5" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{common("edit")}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => handleSubmit(rev)}
                              disabled={isMutating}
                            >
                              <Send className="h-3.5 w-3.5" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{tr("submit")}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => void handleDelete(rev)}
                              disabled={isMutating}
                            >
                              <Trash2 className="h-3.5 w-3.5 text-destructive" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{common("delete")}</TooltipContent>
                        </Tooltip>
                      </>
                    )}
                    {canManage && rev.status === "Submitted" && (
                      <>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => openReview(rev, "approve")}
                              disabled={isMutating}
                            >
                              <Check className="h-3.5 w-3.5 text-green-600" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{tr("approve")}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => openReview(rev, "reject")}
                              disabled={isMutating}
                            >
                              <X className="h-3.5 w-3.5 text-destructive" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{tr("reject")}</TooltipContent>
                        </Tooltip>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-7 w-7"
                              onClick={() => void handleCancel(rev)}
                              disabled={isMutating}
                            >
                              <RotateCcw className="h-3.5 w-3.5" />
                            </Button>
                          </TooltipTrigger>
                          <TooltipContent>{tr("cancel")}</TooltipContent>
                        </Tooltip>
                      </>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="rounded-lg border border-dashed p-6 text-center">
            <p className="text-sm font-medium">{tr("noRevisions")}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {tr("noRevisionsDescription")}
            </p>
          </div>
        )}

        <Sheet open={createOpen} onOpenChange={setCreateOpen}>
          <SheetContent>
            <SheetHeader>
              <SheetTitle>
                {editingRevision ? tr("editDraft") : tr("proposeRevision")}
              </SheetTitle>
              <SheetDescription>{tr("description")}</SheetDescription>
            </SheetHeader>
            <form onSubmit={submitCreate} className="mt-6 space-y-4">
              <div className="space-y-1.5">
                <Label className="text-xs">{tr("budgetLine")}</Label>
                <select
                  required
                  value={createForm.budgetId}
                  onChange={(e) =>
                    setCreateForm({ ...createForm, budgetId: e.target.value })
                  }
                  className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <option value="">{tr("selectBudgetLine")}</option>
                  {budgetSelectorItems.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name} &mdash; {money(b.approvedAmount, b.currency)}
                    </option>
                  ))}
                </select>
                {createForm.budgetId && (
                  <p className="text-[10px] text-muted-foreground">
                    {tr("currentApproved")}:{" "}
                    {money(
                      budgetSelectorItems.find((b) => b.id === createForm.budgetId)
                        ?.approvedAmount,
                      currency
                    )}{" "}
                    {currency}
                  </p>
                )}
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">{tr("proposedAmount")}</Label>
                <Input
                  type="number"
                  required
                  min="0"
                  step="0.01"
                  value={createForm.proposedApprovedAmount}
                  onChange={(e) =>
                    setCreateForm({
                      ...createForm,
                      proposedApprovedAmount: Number(e.target.value),
                    })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">{tr("reason")}</Label>
                <Input
                  required
                  value={createForm.reason}
                  onChange={(e) =>
                    setCreateForm({ ...createForm, reason: e.target.value })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">{tr("justification")}</Label>
                <Textarea
                  value={createForm.justification ?? ""}
                  onChange={(e) =>
                    setCreateForm({
                      ...createForm,
                      justification: e.target.value,
                    })
                  }
                />
              </div>
              {actionError && (
                <p className="text-xs text-destructive">{actionError}</p>
              )}
              <SheetFooter>
                <Button
                  type="submit"
                  disabled={
                    mutations.create.isPending ||
                    mutations.update.isPending ||
                    !createForm.budgetId
                  }
                >
                  {common("save")}
                </Button>
              </SheetFooter>
            </form>
          </SheetContent>
        </Sheet>

        <Sheet open={!!reviewRevision} onOpenChange={() => setReviewRevision(null)}>
          <SheetContent>
            <SheetHeader>
              <SheetTitle>
                {reviewMode === "approve" ? tr("approve") : tr("reject")}
              </SheetTitle>
              <SheetDescription>{reviewRevision?.reason}</SheetDescription>
            </SheetHeader>
            <div className="mt-6 space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-lg border p-3">
                  <p className="text-[10px] uppercase tracking-wide text-muted-foreground">
                    {tr("previousApproved")}
                  </p>
                  <p className="mt-1 text-lg font-semibold">
                    {money(reviewRevision?.previousApprovedAmount, currency)}
                  </p>
                </div>
                <div className="rounded-lg border p-3">
                  <p className="text-[10px] uppercase tracking-wide text-muted-foreground">
                    {tr("proposedAmount")}
                  </p>
                  <p className="mt-1 text-lg font-semibold">
                    {money(reviewRevision?.proposedApprovedAmount, currency)}
                  </p>
                </div>
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">{tr("reviewComment")}</Label>
                <Textarea
                  value={reviewComment}
                  onChange={(e) => setReviewComment(e.target.value)}
                  placeholder={
                    reviewMode === "reject"
                      ? tr("rejectReasonRequired")
                      : tr("reviewCommentOptional")
                  }
                />
              </div>
              {actionError && (
                <p className="text-xs text-destructive">{actionError}</p>
              )}
              <div className="flex gap-2">
                <Button
                  onClick={handleApprove}
                  disabled={
                    mutations.approve.isPending || mutations.reject.isPending
                  }
                  className="flex-1"
                >
                  <Check className="me-1.5 h-4 w-4" />
                  {tr("approve")}
                </Button>
                <Button
                  variant="destructive"
                  onClick={handleReject}
                  disabled={
                    mutations.reject.isPending ||
                    mutations.approve.isPending ||
                    !reviewComment
                  }
                  className="flex-1"
                >
                  <X className="me-1.5 h-4 w-4" />
                  {tr("reject")}
                </Button>
              </div>
            </div>
          </SheetContent>
        </Sheet>

        {historyOpen ? <Sheet open={historyOpen} onOpenChange={setHistoryOpen}>
          <SheetContent side="end" className="flex w-full flex-col overflow-hidden p-0 sm:max-w-5xl">
            <SheetHeader className="shrink-0 border-b px-6 py-5 text-start">
              <SheetTitle>{tr("revisionHistory")}</SheetTitle>
              <SheetDescription>{tr("description")}</SheetDescription>
              <p className="text-xs text-muted-foreground">{tr("totalRecords", { count: historyQuery.data?.totalCount ?? revisionsQuery.data?.totalCount ?? 0 })}</p>
            </SheetHeader>
            <div className="min-h-0 flex-1 overflow-y-auto px-6 py-5">
              {historyQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /></div> : historyQuery.isError ? <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-center"><AlertTriangle className="h-7 w-7 text-destructive/70" /><p role="alert" className="text-sm text-destructive">{common("error")}</p><Button variant="outline" size="sm" onClick={() => void historyQuery.refetch()}><RotateCcw className="me-1.5 h-3.5 w-3.5" />{common("retry")}</Button></div> : historyQuery.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{tr("budgetLine")}</TableHead><TableHead className="text-end">{tr("previousApproved")}</TableHead><TableHead className="text-end">{tr("proposedAmount")}</TableHead><TableHead>{tr("status")}</TableHead><TableHead>{tr("requestedBy")} / {tr("requestedAt")}</TableHead><TableHead>{tr("details")}</TableHead><TableHead className="text-end">{common("actions")}</TableHead></TableRow></TableHeader><TableBody>{historyQuery.data.items.map(rev => <RevisionHistoryTableRow key={rev.id} rev={rev} budgetName={budgetNameMap[rev.budgetId] ?? rev.budgetName} canManage={canManage} isMutating={isMutating} onEdit={openEdit} onSubmit={handleSubmit} onReview={openReview} onCancel={handleCancel} onDelete={handleDelete} tr={tr} common={common} />)}</TableBody></Table></div> : <div className="flex min-h-40 items-center justify-center"><p className="text-sm text-muted-foreground">{tr("noRevisions")}</p></div>}
            </div>
            {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.totalPages ?? 0) > 1 ? <div className="shrink-0 border-t px-6 py-4"><RevisionPagination page={historyPage} totalPages={historyQuery.data?.totalPages ?? 1} previous={common("previous")} next={common("next")} pageOf={tr("pageOf", { page: historyPage, totalPages: historyQuery.data?.totalPages ?? 1 })} onPageChange={setHistoryPage} /></div> : null}
          </SheetContent>
        </Sheet> : null}
      </CardContent>
    </Card>
  );
}
