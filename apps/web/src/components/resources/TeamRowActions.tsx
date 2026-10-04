"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import { MoreHorizontal, Users } from "lucide-react";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth";
import { hasAnyPermission, Permissions } from "@/lib/permissions";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import {
  useDeactivateTeam,
  useDeleteTeam,
  useReactivateTeam,
  type TeamDto,
} from "@/hooks/resources/useTeams";
import { EditTeamButton } from "@/components/resources/TeamFormSheets";
import { TeamDetailSheet } from "@/components/resources/TeamDetailSheet";

type LifecycleDialog = "deactivate" | "reactivate" | "delete" | null;

function apiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export function TeamRowActions({ team }: { team: TeamDto }) {
  const t = useTranslations("resources.teams");
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const canEdit = hasAnyPermission(permissions, [Permissions.Team.Edit, Permissions.Team.Manage]);
  const canDelete = hasAnyPermission(permissions, [Permissions.Team.Delete, Permissions.Team.Manage]);

  const [editOpen, setEditOpen] = React.useState(false);
  const [detailOpen, setDetailOpen] = React.useState(false);
  const [dialog, setDialog] = React.useState<LifecycleDialog>(null);
  const [error, setError] = React.useState<string | null>(null);

  const deactivate = useDeactivateTeam();
  const reactivate = useReactivateTeam();
  const remove = useDeleteTeam();

  if (!canEdit && !canDelete) return null;

  const pending = deactivate.isPending || reactivate.isPending || remove.isPending;
  const closeDialog = () => {
    if (pending) return;
    setDialog(null);
    setError(null);
  };

  const runDeactivate = async () => {
    setError(null);
    try {
      await deactivate.mutateAsync(team.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  const runReactivate = async () => {
    setError(null);
    try {
      await reactivate.mutateAsync(team.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  const runDelete = async () => {
    setError(null);
    try {
      await remove.mutateAsync(team.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={t("actions")}>
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" sideOffset={4} className="min-w-44">
          <DropdownMenuItem onClick={() => setDetailOpen(true)} disabled={pending}>
            <Users className="me-2 h-3.5 w-3.5" aria-hidden="true" />
            {t("manageMembers")}
          </DropdownMenuItem>
          {canEdit ? (
            <DropdownMenuItem onClick={() => setEditOpen(true)} disabled={pending}>
              {t("edit")}
            </DropdownMenuItem>
          ) : null}
          {canDelete ? <DropdownMenuSeparator /> : null}
          {canDelete && team.isActive ? (
            <DropdownMenuItem
              onClick={() => {
                setError(null);
                setDialog("deactivate");
              }}
              disabled={pending}
              className="text-warning focus:text-warning"
            >
              {t("deactivate")}
            </DropdownMenuItem>
          ) : canDelete ? (
            <DropdownMenuItem
              onClick={() => {
                setError(null);
                setDialog("reactivate");
              }}
              disabled={pending}
              className="text-success focus:text-success"
            >
              {t("reactivate")}
            </DropdownMenuItem>
          ) : null}
          {canDelete ? (
            <DropdownMenuItem
              onClick={() => {
                setError(null);
                setDialog("delete");
              }}
              disabled={pending}
              className="text-destructive focus:text-destructive"
            >
              {t("delete")}
            </DropdownMenuItem>
          ) : null}
        </DropdownMenuContent>
      </DropdownMenu>

      <EditTeamButton team={team} open={editOpen} onOpenChange={setEditOpen} hideTrigger />
      <TeamDetailSheet team={team} open={detailOpen} onOpenChange={setDetailOpen} />

      {dialog === "deactivate" ? (
        <ConfirmDialog
          title={t("deactivateTeamTitle", { name: team.name })}
          description={t("deactivateTeamDescription")}
          confirmLabel={pending ? t("deactivating") : t("deactivate")}
          cancelLabel={t("cancel")}
          pendingLabel={t("deactivating")}
          variant="warning"
          pending={pending}
          error={error}
          onConfirm={runDeactivate}
          onCancel={closeDialog}
        />
      ) : null}

      {dialog === "reactivate" ? (
        <ConfirmDialog
          title={t("reactivateTeamTitle", { name: team.name })}
          description={t("reactivateTeamDescription")}
          confirmLabel={pending ? t("reactivating") : t("reactivate")}
          cancelLabel={t("cancel")}
          pendingLabel={t("reactivating")}
          variant="default"
          pending={pending}
          error={error}
          onConfirm={runReactivate}
          onCancel={closeDialog}
        />
      ) : null}

      {dialog === "delete" ? (
        <ConfirmDialog
          title={t("deleteTeamTitle", { name: team.name })}
          description={t("deleteTeamDescription")}
          details={
            <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
              {t("deleteTeamHint")}
            </p>
          }
          confirmLabel={pending ? t("deleting") : t("delete")}
          cancelLabel={t("cancel")}
          pendingLabel={t("deleting")}
          variant="destructive"
          pending={pending}
          error={error}
          onConfirm={runDelete}
          onCancel={closeDialog}
        />
      ) : null}
    </>
  );
}
