"use client";

/**
 * Team detail side-sheet: read-only master fields plus the effective-dated membership
 * editor. Membership is fetched with includeMembers=true so the panel always renders
 * from the server, not from the possibly stale list row.
 */

import * as React from "react";
import { useTranslations } from "next-intl";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Button } from "@/components/ui/button";
import { ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { useTeam, type TeamDto } from "@/hooks/resources/useTeams";
import { TeamMembersPanel } from "@/components/resources/TeamMembersPanel";
import { getStatusTextClass } from "@/lib/status-presentation";

export function TeamDetailSheet({
  team,
  open,
  onOpenChange,
}: {
  team: TeamDto;
  open: boolean;
  // eslint-disable-next-line no-unused-vars
  onOpenChange: (next: boolean) => void;
}) {
  const t = useTranslations("resources.teams");
  const teamQuery = useTeam(open ? team.id : null, true);
  const detail = teamQuery.data ?? team;

  return (
    <SideSheet
      open={open}
      onOpenChange={onOpenChange}
      title={detail.name}
      description={detail.code ?? undefined}
      size="lg"
      closeLabel={t("close")}
    >
      <SideSheetBody className="space-y-6">
        {teamQuery.isLoading ? (
          <LoadingSkeleton rows={4} />
        ) : teamQuery.isError ? (
          <ErrorState
            title={t("loadError")}
            description={teamQuery.error instanceof Error ? teamQuery.error.message : undefined}
            onRetry={() => void teamQuery.refetch()}
          />
        ) : (
          <>
            <section aria-labelledby="team-detail-heading" className="space-y-3">
              <h3 id="team-detail-heading" className="text-sm font-semibold text-foreground">
                {t("details")}
              </h3>
              <dl className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-0.5">
                  <dt className="text-xs text-muted-foreground">{t("code")}</dt>
                  <dd className="font-mono text-sm font-medium">{detail.code ?? "—"}</dd>
                </div>
                <div className="space-y-0.5">
                  <dt className="text-xs text-muted-foreground">{t("status")}</dt>
                  <dd className={`text-sm font-medium ${getStatusTextClass(detail.isActive ? "Active" : "Inactive")}`}>
                      {detail.isActive ? t("active") : t("inactive")}
                  </dd>
                </div>
                <div className="space-y-0.5">
                  <dt className="text-xs text-muted-foreground">{t("department")}</dt>
                  <dd className="text-sm">{detail.departmentName ?? t("noDepartment")}</dd>
                </div>
                <div className="space-y-0.5">
                  <dt className="text-xs text-muted-foreground">{t("teamLead")}</dt>
                  <dd className="text-sm">{detail.teamLeadName ?? t("noTeamLead")}</dd>
                </div>
                <div className="space-y-0.5">
                  <dt className="text-xs text-muted-foreground">{t("emailAlias")}</dt>
                  <dd className="text-sm">{detail.emailAlias ?? "—"}</dd>
                </div>
                <div className="space-y-0.5 sm:col-span-2">
                  <dt className="text-xs text-muted-foreground">{t("description")}</dt>
                  <dd className="text-sm">{detail.description ?? "—"}</dd>
                </div>
              </dl>
            </section>

            <TeamMembersPanel team={detail} />
          </>
        )}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
          {t("close")}
        </Button>
      </SideSheetFooter>
    </SideSheet>
  );
}
