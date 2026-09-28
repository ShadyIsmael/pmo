"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useAuth } from "@/lib/auth";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import { useCancelProjectChangeRequest, useProjectChangeRequests } from "@/hooks/projects/useProjectChangeRequests";
import type { ProjectChangeRequestDto } from "@/lib/api/project-change-requests";
import { ProjectChangeRequestStatus, ProjectChangeRequestType } from "@/lib/enums";
import { formatDate } from "@/lib/utils";
import { getStatusBadgeVariant, type StatusBadgeVariant } from "@/lib/status-presentation";
import { AlertTriangle, Ban, GitBranch, Loader2, RefreshCw } from "lucide-react";
import { useEnumLabel } from "@/hooks/useEnumLabels";

interface ChangeTrackerProps {
  projectId: string;
  currency?: string | null;
}

function getStatusVariant(status: ProjectChangeRequestStatus): StatusBadgeVariant {
  return getStatusBadgeVariant(status);
}

function ChangeTrackerTable({ t, requests, userId, onCancel, getStatusLabel, currency }: { t: ReturnType<typeof useTranslations>; requests: ProjectChangeRequestDto[]; userId: string | undefined; onCancel: (_request: ProjectChangeRequestDto) => void; getStatusLabel: (_status: ProjectChangeRequestStatus) => string; currency?: string | null }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t("changeType")}</TableHead>
          <TableHead>{t("changeNumber")}</TableHead>
          <TableHead>{t("changeReason")}</TableHead>
          <TableHead>{t("budgetChangeAmount")}</TableHead>
          <TableHead>{t("status")}</TableHead>
          <TableHead>{t("requestedBy")}</TableHead>
          <TableHead>{t("requestedAt")}</TableHead>
          <TableHead>{t("reviewedBy")}</TableHead>
          <TableHead>{t("reviewedAt")}</TableHead>
          <TableHead className="text-right">{t("actions")}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {requests.map((request) => (
          <TableRow key={request.id}>
            <TableCell className="font-medium">
              {request.type === ProjectChangeRequestType.Administrative ? t("changeTypeAdministrative") : t("changeTypeBaseline")}
            </TableCell>
            <TableCell>#{request.number}</TableCell>
            <TableCell className="max-w-[260px] truncate">{request.reason}</TableCell>
            <TableCell>
              {request.budgetChangeAmount != null ? (
                <div>
                  <span className={`font-medium ${request.budgetChangeAmount >= 0 ? "text-green-600" : "text-destructive"}`}>
                    {request.budgetChangeAmount >= 0 ? "+" : ""}{request.budgetChangeAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                  {request.proposedProjectBudget != null && (
                    <p className="text-[10px] text-muted-foreground">
                      {t("proposedBudget")}: {request.proposedProjectBudget.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} {currency ?? ""}
                    </p>
                  )}
                </div>
              ) : "—"}
            </TableCell>
            <TableCell>
              <Badge variant={getStatusVariant(request.status)} className="w-fit text-[10px]">
                {getStatusLabel(request.status)}
              </Badge>
            </TableCell>
            <TableCell>{request.requestedByName}</TableCell>
            <TableCell>{formatDate(request.requestedAt)}</TableCell>
            <TableCell>{request.reviewedByName || "—"}</TableCell>
            <TableCell>{request.reviewedAt ? formatDate(request.reviewedAt) : "—"}</TableCell>
            <TableCell className="text-right">
              {canCancelRequest(request, userId) && (
                <Button type="button" variant="outline" size="sm" onClick={() => onCancel(request)}>
                  <Ban className="mr-1.5 h-3.5 w-3.5" />
                  {t("cancelChangeRequest")}
                </Button>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

export function ChangeTracker({ projectId, currency }: ChangeTrackerProps) {
  const t = useTranslations("projects");
  const { user } = useAuth();
  const getStatusLabel = useEnumLabel("projectChangeRequestStatus");
  const { data: requests, isLoading, isError, refetch, isRefetching } = useProjectChangeRequests(projectId);
  const cancelMutation = useCancelProjectChangeRequest();
  const [cancellingRequest, setCancellingRequest] = useState<ProjectChangeRequestDto | null>(null);

  const handleCancel = async () => {
    if (!cancellingRequest) return;
    await cancelMutation.mutateAsync(cancellingRequest.id);
    setCancellingRequest(null);
  };

  if (isLoading) {
    return <Card className="shadow-none"><CardContent className="p-0"><Skeleton className="h-40 w-full rounded-none" /></CardContent></Card>;
  }

  if (isError) {
    return (
      <Card className="shadow-none">
        <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
          <AlertTriangle className="h-10 w-10 text-status-red" />
          <div className="space-y-1">
            <p className="text-sm font-medium text-foreground">{t("changeTrackerError")}</p>
            <p className="text-xs text-muted-foreground">{t("changeTrackerErrorDescription")}</p>
          </div>
          <Button type="button" variant="outline" size="sm" disabled={isRefetching} onClick={() => void refetch()}>
            <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${isRefetching ? "animate-spin" : ""}`} />
            {t("retry")}
          </Button>
        </CardContent>
      </Card>
    );
  }

  const hasRequests = (requests ?? []).length > 0;

  return (
    <Card className="shadow-none">
      <CardHeader>
        <CardTitle className="text-sm flex items-center gap-2">
          <GitBranch className="h-4 w-4" />
          {t("changeTracker")}
          <span className="text-muted-foreground font-normal">({requests?.length ?? 0})</span>
        </CardTitle>
        <CardDescription>{t("changeTrackerDescription")}</CardDescription>
      </CardHeader>
      <CardContent>
        {hasRequests ? (
          <ChangeTrackerTable t={t} requests={requests ?? []} userId={user?.id} onCancel={setCancellingRequest} getStatusLabel={getStatusLabel} currency={currency} />
        ) : (
          <div className="flex flex-col items-center gap-2 py-12 text-center">
            <GitBranch className="h-10 w-10 text-muted-foreground/40" />
            <p className="text-sm font-medium text-foreground">{t("noChangeRequests")}</p>
            <p className="text-xs text-muted-foreground">{t("noChangeRequestsDescription")}</p>
          </div>
        )}
      </CardContent>

      <Dialog open={!!cancellingRequest} onOpenChange={(open) => { if (!open) { setCancellingRequest(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("cancelChangeRequestTitle")}</DialogTitle>
            <DialogDescription>{t("cancelChangeRequestDescription")}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" disabled={cancelMutation.isPending} onClick={() => setCancellingRequest(null)}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" disabled={cancelMutation.isPending} onClick={() => void handleCancel()}>
              {cancelMutation.isPending && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              {t("confirmCancel")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function canCancelRequest(request: ProjectChangeRequestDto, userId: string | undefined) {
  return request.status === ProjectChangeRequestStatus.Pending && !!userId && request.requestedBy === userId;
}
