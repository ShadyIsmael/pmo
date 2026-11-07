import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, CheckCircle2, Loader2, XCircle } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Badge } from "@components/ui/Badge";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { useApprovalReview, useAcceptHandoverRequest, useRejectHandoverRequest, useAcceptManagerHandover, useRejectManagerHandover, useApproveProjectChangeRequest, useRejectProjectChangeRequest } from "./api/myWork";
import type { ApprovalReviewType } from "@lib/approval-navigation";
import { MyWorkTabs } from "./MyWorkTabs";

const approvalReviewTitleKeys: Record<ApprovalReviewType, string> = {
  "project-handover": "approvals.projectHandoverReviewTitle",
  "program-handover": "approvals.programHandoverReviewTitle",
  "portfolio-handover": "approvals.portfolioHandoverReviewTitle",
  "change-request": "approvals.changeRequestReviewTitle",
};

export function ApprovalReviewPage({ type }: { type: ApprovalReviewType }) {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { t, formatDate, formatCurrency } = useI18n();
  const { refreshSnapshot, effectiveReadOnly } = useManagement();
  const reviewQuery = useApprovalReview(type, id);
  const acceptProjectHandover = useAcceptHandoverRequest();
  const rejectProjectHandover = useRejectHandoverRequest();
  const acceptProgramHandover = useAcceptManagerHandover("programs");
  const acceptPortfolioHandover = useAcceptManagerHandover("portfolios");
  const rejectProgramHandover = useRejectManagerHandover("programs");
  const rejectPortfolioHandover = useRejectManagerHandover("portfolios");
  const approveChangeRequest = useApproveProjectChangeRequest();
  const rejectChangeRequest = useRejectProjectChangeRequest();
  const [decisionNotes, setDecisionNotes] = useState("");
  const [decisionError, setDecisionError] = useState<string | null>(null);

  const review = reviewQuery.data;
  const pageTitle = t(approvalReviewTitleKeys[type]);
  const isPending = acceptProjectHandover.isPending || rejectProjectHandover.isPending
    || acceptProgramHandover.isPending || acceptPortfolioHandover.isPending
    || rejectProgramHandover.isPending || rejectPortfolioHandover.isPending
    || approveChangeRequest.isPending || rejectChangeRequest.isPending;

  const decide = async (approve: boolean) => {
    if (!review || !id) return;
    setDecisionError(null);
    try {
      switch (type) {
        case "project-handover":
          if (approve) await acceptProjectHandover.mutateAsync(id);
          else await rejectProjectHandover.mutateAsync({ requestId: id, rejectionReason: decisionNotes.trim() });
          break;
        case "program-handover":
        case "portfolio-handover": {
          const accept = type === "program-handover" ? acceptProgramHandover : acceptPortfolioHandover;
          const reject = type === "program-handover" ? rejectProgramHandover : rejectPortfolioHandover;
          if (approve) await accept.mutateAsync(id);
          else await reject.mutateAsync({ requestId: id, rejectionReason: decisionNotes.trim() });
          break;
        }
        case "change-request":
          if (approve) await approveChangeRequest.mutateAsync({ id, dto: {} });
          else await rejectChangeRequest.mutateAsync({ id, decisionNotes: decisionNotes.trim() });
          break;
        default:
          setDecisionError(t("approvals.reviewUnavailable"));
          return;
      }
      await refreshSnapshot();
      navigate("/my-work/approvals", { replace: true });
    } catch {
      setDecisionError(t("approvals.reviewDecisionFailed"));
    }
  };

  if (reviewQuery.isLoading) {
    return (
      <div className="space-y-5">
        <PageHeader title={pageTitle} />
        <MyWorkTabs />
        <div role="status" aria-label={t("common.loading")} className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
      </div>
    );
  }

  if (!review || reviewQuery.isError) {
    return (
      <section className="space-y-4">
        <PageHeader title={pageTitle} />
        <MyWorkTabs />
        <Button asChild size="sm" variant="ghost"><Link to="/my-work/approvals"><ArrowLeft className="mr-2 h-4 w-4" />{t("approvals.backToApprovals")}</Link></Button>
        <div role="alert" className="rounded-lg border p-6 text-sm text-muted-foreground">{t("approvals.reviewUnavailable")}</div>
      </section>
    );
  }

  const isRoleAcceptance = type === "project-handover" || type === "program-handover" || type === "portfolio-handover";
  const hasDecisionFlow = isRoleAcceptance || type === "change-request";
  const approveLabel = isRoleAcceptance ? t("approvals.acceptAssignment") : type === "change-request" ? t("approvals.approve") : t("approvals.reviewUnavailable");
  const businessObjectLabel = review.businessObjectType === "Project" ? t("approvals.businessProject")
    : review.businessObjectType === "Program" ? t("approvals.businessProgram")
      : review.businessObjectType === "Portfolio" ? t("approvals.businessPortfolio") : review.businessObjectType;

  return (
    <div className="space-y-5">
      <Button asChild size="sm" variant="ghost"><Link to="/my-work/approvals"><ArrowLeft className="mr-2 h-4 w-4" />{t("approvals.backToApprovals")}</Link></Button>
      <PageHeader title={pageTitle} description={`${businessObjectLabel}: ${review.businessObjectName}`} />
      <MyWorkTabs />
      <section className="rounded-xl border bg-card p-5 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-4">
          <div className="min-w-0">
            <p className="text-sm font-semibold">{review.businessObjectName}{review.businessObjectCode ? ` · ${review.businessObjectCode}` : ""}</p>
            {review.businessObjectStatus ? <p className="mt-1 text-xs text-muted-foreground">{t("approvals.objectStatus")}: {review.businessObjectStatus}</p> : null}
          </div>
          <Badge status={review.status}>{review.status}</Badge>
        </div>
        <div className="grid gap-x-8 gap-y-5 py-5 sm:grid-cols-2">
          <ReviewField label={t("approvals.reason")} value={review.reason} />
          {review.justification ? <ReviewField label={t("approvals.justification")} value={review.justification} /> : null}
          {review.impact ? <ReviewField label={t("approvals.impact")} value={review.impact} /> : null}
          {review.changeType ? <ReviewField label={t("approvals.changeType")} value={review.changeType} /> : null}
          {review.changeNumber != null ? <ReviewField label={t("approvals.changeNumber")} value={`#${review.changeNumber}`} /> : null}
          {review.currentAssigneeName ? <ReviewField label={t("approvals.currentAssignee")} value={review.currentAssigneeName} /> : null}
          {review.proposedAssigneeName ? <ReviewField label={t("approvals.proposedAssignee")} value={review.proposedAssigneeName} /> : null}
          {review.effectiveDate ? <ReviewField label={t("approvals.effectiveDate")} value={formatDate(review.effectiveDate)} /> : null}
          <ReviewField label={t("approvals.requestedBy")} value={review.requestedByName} />
          <ReviewField label={t("approvals.requestedAt")} value={formatDate(review.requestedAt)} />
          {review.currentAmount != null ? <ReviewField label={t("approvals.currentAmount")} value={formatCurrency(review.currentAmount, review.currency ?? undefined)} /> : null}
          {review.amountChange != null ? <ReviewField label={t("approvals.amountChange")} value={formatCurrency(review.amountChange, review.currency ?? undefined)} /> : null}
          {review.proposedAmount != null ? <ReviewField label={t("approvals.proposedAmount")} value={formatCurrency(review.proposedAmount, review.currency ?? undefined)} /> : null}
          {review.notes ? <ReviewField label={t("approvals.requestNotes")} value={review.notes} /> : null}
        </div>
        {review.isReadOnly ? <p className="border-t pt-3 text-xs text-muted-foreground">{t("approvals.readOnlyReview")}</p> : null}
      </section>

      {review.canDecide && hasDecisionFlow && !effectiveReadOnly ? (
        <section className="space-y-3 rounded-xl border bg-card p-5">
          {decisionError ? <p role="alert" className="text-sm text-destructive">{decisionError}</p> : null}
          <Textarea value={decisionNotes} onChange={(event) => setDecisionNotes(event.target.value)} rows={3} placeholder={t("approvals.decisionNotesPlaceholder")} aria-label={t("approvals.decisionNotesPlaceholder")} />
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="destructive" disabled={isPending || !decisionNotes.trim()} onClick={() => void decide(false)}>
              {(rejectProjectHandover.isPending || rejectProgramHandover.isPending || rejectPortfolioHandover.isPending || rejectChangeRequest.isPending) ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <XCircle className="mr-2 h-4 w-4" />}
              {t("approvals.reject")}
            </Button>
            <Button disabled={isPending} onClick={() => void decide(true)}>
              {(acceptProjectHandover.isPending || acceptProgramHandover.isPending || acceptPortfolioHandover.isPending || approveChangeRequest.isPending) ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle2 className="mr-2 h-4 w-4" />}
              {approveLabel}
            </Button>
          </div>
        </section>
      ) : null}
    </div>
  );
}

function ReviewField({ label, value }: { label: string; value?: string | null }) {
  if (!value) return null;
  return <div className="min-w-0"><p className="text-xs font-medium text-muted-foreground">{label}</p><p className="mt-1 whitespace-pre-wrap break-words text-sm">{value}</p></div>;
}
