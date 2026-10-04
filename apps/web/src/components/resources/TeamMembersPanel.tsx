"use client";

/**
 * Effective-dated membership editor.
 *
 * Membership is never hard-deleted: removing a member closes the row with an EffectiveTo,
 * and a membership already closed cannot be re-opened, so the multi-select only offers
 * Resources that are not current members. "Show history" reveals the closed rows.
 */

import * as React from "react";
import { useLocale, useTranslations } from "next-intl";
import { UserMinus, Users, History } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/utils";
import { getResourceTypeTextClass } from "@/lib/status-presentation";
import { hasAnyPermission, Permissions } from "@/lib/permissions";
import { useResources } from "@/hooks/resources/useResources";
import {
  useRemoveTeamMember,
  useSetTeamMembers,
  type TeamDto,
  type TeamMemberDto,
} from "@/hooks/resources/useTeams";

type ResourceOption = { id: string; name: string; type?: string };

function toResourceOptions(data: { items?: readonly ResourceOption[] } | undefined): ResourceOption[] {
  return (data?.items ?? []).map((resource) => ({
    id: resource.id,
    name: resource.name,
    type: resource.type,
  }));
}

export function TeamMembersPanel({ team }: { team: TeamDto }) {
  const t = useTranslations("resources.teams");
  const locale = useLocale();
  const { user } = useAuth();
  const canEdit = hasAnyPermission(user?.permissions ?? [], [Permissions.Team.Edit, Permissions.Team.Manage]);

  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const setMembers = useSetTeamMembers();
  const removeMember = useRemoveTeamMember();

  const [selected, setSelected] = React.useState<string[]>([]);
  const [showHistory, setShowHistory] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const members = React.useMemo(() => team.members ?? [], [team.members]);
  const currentMembers = React.useMemo(
    () => members.filter((member) => member.isActive && !member.effectiveTo),
    [members],
  );
  const historicalMembers = React.useMemo(
    () => members.filter((member) => !member.isActive || member.effectiveTo),
    [members],
  );
  const currentIds = React.useMemo(() => new Set(currentMembers.map((member) => member.resourceId)), [currentMembers]);

  const resourceOptions = React.useMemo(() => toResourceOptions(resourcesQuery.data), [resourcesQuery.data]);

  // Hydrate the draft from the server whenever the membership set changes, so the
  // multi-select is never stale relative to what was actually saved.
  React.useEffect(() => {
    setSelected(currentMembers.map((member) => member.resourceId));
  }, [currentMembers]);

  // A Resource that is already a current member must not be offered again: re-adding
  // would create a second overlapping membership, which the API rejects with 409.
  const addableOptions = React.useMemo(
    () => resourceOptions.filter((resource) => !currentIds.has(resource.id)),
    [resourceOptions, currentIds],
  );

  const pending = setMembers.isPending || removeMember.isPending;

  const toggle = (resourceId: string) => {
    setSelected((current) =>
      current.includes(resourceId)
        ? current.filter((id) => id !== resourceId)
        : [...current, resourceId],
    );
  };

  const additions = React.useMemo(
    () => selected.filter((id) => !currentIds.has(id)),
    [selected, currentIds],
  );
  const removals = React.useMemo(
    () => currentMembers.map((member) => member.resourceId).filter((id) => !selected.includes(id)),
    [currentMembers, selected],
  );
  const hasChanges = additions.length > 0 || removals.length > 0;

  const applyChanges = async () => {
    setError(null);
    try {
      await setMembers.mutateAsync({ teamId: team.id, resourceIds: selected });
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : t("membersSaveError"));
    }
  };

  const remove = async (resourceId: string) => {
    setError(null);
    try {
      await removeMember.mutateAsync({ teamId: team.id, resourceId });
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : t("membersSaveError"));
    }
  };

  const formatDate = (value?: string) => {
    if (!value) return "—";
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return "—";
    return locale === "ar"
      ? parsed.toLocaleDateString("ar-EG", { year: "numeric", month: "short", day: "numeric" })
      : parsed.toLocaleDateString("en-GB", { year: "numeric", month: "short", day: "numeric" });
  };

  return (
    <div className="space-y-6">
      <section aria-labelledby="team-members-heading" className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 id="team-members-heading" className="text-sm font-semibold text-foreground">
            {t("members")}
          </h3>
          <span className="text-xs text-muted-foreground">
            {t("memberCountLabel", { count: currentMembers.length })}
          </span>
        </div>

        {currentMembers.length === 0 ? (
          <p className="rounded-md border border-dashed border-border bg-muted/20 px-3 py-4 text-sm text-muted-foreground">
            {t("noMembers")}
          </p>
        ) : (
          <ul className="space-y-2">
            {currentMembers.map((member) => (
              <li
                key={member.id}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border bg-background px-3 py-2"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{member.resourceName ?? member.resourceId}</p>
                  <p className="text-xs text-muted-foreground">
                    {t("memberSince", { date: formatDate(member.effectiveFrom) })}
                  </p>
                </div>
                {canEdit ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="text-destructive focus:text-destructive"
                    disabled={pending}
                    onClick={() => void remove(member.resourceId)}
                  >
                    <UserMinus className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />
                    {t("removeMember")}
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </section>

      {canEdit ? (
        <section aria-labelledby="team-add-members-heading" className="space-y-3">
          <h3 id="team-add-members-heading" className="text-sm font-semibold text-foreground">
            {t("addMembers")}
          </h3>

          {resourcesQuery.isLoading ? (
            <LoadingSkeleton rows={2} />
          ) : resourcesQuery.isError ? (
            <ErrorState
              title={t("resourcesLoadError")}
              onRetry={() => void resourcesQuery.refetch()}
            />
          ) : addableOptions.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("noResourcesToAdd")}</p>
          ) : (
            <div className="max-h-64 space-y-1 overflow-y-auto rounded-md border border-border p-2">
              {addableOptions.map((resource) => {
                const checked = selected.includes(resource.id);
                return (
                  <label
                    key={resource.id}
                    className={cn(
                      "flex cursor-pointer items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-muted/50",
                      !checked && "opacity-70",
                    )}
                  >
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-input"
                      checked={checked}
                      onChange={() => toggle(resource.id)}
                    />
                    <span className="truncate">{resource.name}</span>
                    {resource.type ? (
                      <span className={`ms-auto shrink-0 text-xs font-medium ${getResourceTypeTextClass(resource.type)}`}>
                        {resource.type}
                      </span>
                    ) : null}
                  </label>
                );
              })}
            </div>
          )}

          {additions.length > 0 || removals.length > 0 ? (
            <p className="text-xs text-muted-foreground">
              {t("pendingChanges", { added: additions.length, removed: removals.length })}
            </p>
          ) : null}

          <Button
            type="button"
            size="sm"
            disabled={!hasChanges || pending}
            onClick={() => void applyChanges()}
          >
            <Users className="me-1.5 h-4 w-4" aria-hidden="true" />
            {setMembers.isPending ? t("savingMembers") : t("saveMembers")}
          </Button>
        </section>
      ) : null}

      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}

      <section aria-labelledby="team-member-history-heading" className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 id="team-member-history-heading" className="text-sm font-semibold text-foreground">
            {t("memberHistory")}
          </h3>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setShowHistory((value) => !value)}
          >
            <History className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />
            {showHistory ? t("hideHistory") : t("showHistory")}
          </Button>
        </div>

        {showHistory ? (
          historicalMembers.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("noHistory")}</p>
          ) : (
            <ul className="space-y-1.5">
              {historicalMembers.map((member) => (
                <li
                  key={member.id}
                  className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border/60 bg-muted/20 px-3 py-2 text-sm"
                >
                  <span className="truncate text-muted-foreground">{member.resourceName ?? member.resourceId}</span>
                  <span className="text-xs text-muted-foreground">
                    {t("memberPeriod", {
                      from: formatDate(member.effectiveFrom),
                      to: member.effectiveTo ? formatDate(member.effectiveTo) : t("openEnded"),
                    })}
                  </span>
                </li>
              ))}
            </ul>
          )
        ) : null}
      </section>
    </div>
  );
}

export type { TeamMemberDto };
