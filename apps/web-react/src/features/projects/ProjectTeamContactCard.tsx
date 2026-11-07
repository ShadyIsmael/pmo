import * as React from "react";
import { Briefcase, Building2, Mail, Phone, UserRound, X, type LucideIcon } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Dialog, DialogDescription, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { useI18n } from "@i18n/messages";
import { useResourceReferenceData } from "@features/resources/api/resources";
import { cn } from "@lib/utils";
import { getResourceTypeTextClass } from "@lib/status-presentation";
import type { ProjectPersonDto } from "./api/projects";
import { useProjectTeamContactResource } from "./api/project-team-contact";

type ProjectTeamContactCardProps = {
  person: ProjectPersonDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

function InformationRow({
  icon: Icon,
  label,
  value,
  href,
  emptyValue,
  iconToneClassName,
  valueToneClassName,
}: {
  icon: LucideIcon;
  label: string;
  value?: string | null;
  href?: string;
  emptyValue: string;
  iconToneClassName?: string;
  valueToneClassName?: string;
}) {
  const content = value?.trim()
    ? href
      ? <a dir="ltr" className="inline-block max-w-full break-all text-start text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" href={href}>{value}</a>
      : <span className="break-words">{value}</span>
    : <span className="font-normal text-muted-foreground">{emptyValue}</span>;

  return (
    <div className="flex min-w-0 items-start gap-3 py-3 first:pt-0 last:pb-0">
      <span className={cn("mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/8 text-primary", iconToneClassName)} aria-hidden="true">
        <Icon className="h-4 w-4" />
      </span>
      <dl className="min-w-0 flex-1">
        <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
        <dd className={cn("mt-1 break-words text-sm font-medium leading-5 text-foreground", valueToneClassName)}>{content}</dd>
      </dl>
    </div>
  );
}

function InformationSection({ icon: Icon, title, children }: { icon: LucideIcon; title: string; children: React.ReactNode }) {
  return (
    <section className="min-w-0 rounded-2xl border bg-card p-4 shadow-sm sm:p-5">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-foreground">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-muted text-muted-foreground" aria-hidden="true">
          <Icon className="h-4 w-4" />
        </span>
        {title}
      </h3>
      <div className="mt-3 divide-y">
        {children}
      </div>
    </section>
  );
}

export function ProjectTeamContactCard({ person, open, onOpenChange }: ProjectTeamContactCardProps) {
  const { t, locale } = useI18n();
  const resourceId = open ? person.resourceId ?? "" : "";
  const resourceQuery = useProjectTeamContactResource(resourceId, open);
  const resourceTypeQuery = useResourceReferenceData(open && Boolean(resourceId));
  const resource = resourceQuery.data;
  const hasAdministrativeAssociation = Boolean(
    resource?.primaryOrganizationalUnitId && resource.primaryOrganizationalUnitName?.trim(),
  );
  const managerResourceId = hasAdministrativeAssociation ? resource?.primaryManagerResourceId ?? "" : "";
  const managerQuery = useProjectTeamContactResource(managerResourceId, open && Boolean(managerResourceId));
  const manager = managerQuery.data?.isActive ? managerQuery.data : undefined;
  const titleId = `project-team-contact-title-${person.id}`;
  const descriptionId = `project-team-contact-description-${person.id}`;
  const resourceType = resourceTypeQuery.data?.resourceTypes.find(
    (option) => option.code.toLowerCase() === (resource?.type ?? person.resourceType).toLowerCase(),
  );
  const resourceTypeLabel = resourceType
    ? locale === "ar" ? resourceType.nameAr : resourceType.nameEn
    : t("projects.contactCard.resourceTypeUnavailable");
  const resourceTypeTone = getResourceTypeTextClass(resource?.type ?? person.resourceType);
  const functionalRoles = (person.functionalRoles ?? [])
    .map((role) => locale === "ar" ? role.nameAr : role.nameEn)
    .filter((role) => role.trim().length > 0);
  const roleKeys: Record<string, string> = {
    projectmanager: "projects.projectManager",
    teammember: "projects.teamMember",
    sponsor: "projects.sponsor",
    stakeholder: "projects.stakeholder",
    other: "projects.otherRelationship",
  };
  const normalizedRole = person.role?.trim();
  const projectRole = functionalRoles.length > 0
    ? functionalRoles.join(" · ")
    : normalizedRole
      ? roleKeys[normalizedRole.replace(/[\s_-]/g, "").toLowerCase()]
        ? t(roleKeys[normalizedRole.replace(/[\s_-]/g, "").toLowerCase()]!)
        : normalizedRole
      : null;
  const phone = resource?.phone?.trim();
  const dialablePhone = phone?.replace(/[^\d+#*;,]/g, "");
  const phoneHref = dialablePhone ? `tel:${dialablePhone}` : undefined;
  const displayName = resource?.name ?? person.name;
  const displayEmail = resourceQuery.isError ? null : resource?.email ?? person.email;

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      ariaLabelledBy={titleId}
      ariaDescribedBy={descriptionId}
      contentClassName="max-h-[88vh] w-[calc(100vw-2rem)] max-w-3xl gap-0 overflow-y-auto rounded-2xl p-0 sm:max-w-3xl"
    >
        <div className="relative overflow-hidden border-b bg-gradient-to-br from-primary/10 via-background to-muted/40 px-5 py-6 sm:px-7">
          <div className="pointer-events-none absolute -end-12 -top-16 h-48 w-48 rounded-full bg-primary/5 blur-3xl" aria-hidden="true" />
          <div className="relative flex items-start gap-4 sm:gap-5">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-1 ring-primary/15 sm:h-16 sm:w-16" aria-hidden="true">
              <UserRound className="h-7 w-7 sm:h-8 sm:w-8" />
            </div>
            <div className="min-w-0 flex-1">
              <DialogHeader className="text-start">
                <DialogTitle id={titleId} className="text-xs font-semibold uppercase tracking-wide text-primary">{t("projects.contactCard.title")}</DialogTitle>
                <DialogDescription id={descriptionId} className="sr-only">{t("projects.contactCard.description")}</DialogDescription>
              </DialogHeader>
              <p className="mt-2 break-words text-xl font-semibold leading-tight text-foreground sm:text-2xl">{displayName}</p>
              <p className="mt-1 break-words text-sm text-muted-foreground">{resource?.jobTitle || person.jobTitle || t("projects.contactCard.notProvided")}</p>
              <div className="mt-4 flex flex-wrap items-center gap-2">
                {projectRole ? <Badge variant="secondary" className="max-w-full whitespace-normal text-start">{t("projects.contactCard.projectRole")}: {projectRole}</Badge> : null}
                {person.resourceId ? (
                  <Badge variant="outline" className={resourceTypeTone}><span className="text-muted-foreground">{t("projects.contactCard.resourceType")}:</span> {resourceTypeLabel}</Badge>
                ) : person.hasSystemUser ? (
                  <Badge variant="outline" className="text-success">{t("projects.contactCard.systemUser")}</Badge>
                ) : null}
              </div>
            </div>
            <Button type="button" variant="ghost" size="sm" className="h-9 w-9 shrink-0 p-0" onClick={() => onOpenChange(false)} aria-label={t("common.close")} title={t("common.close")}>
              <X className="h-4 w-4" aria-hidden="true" />
            </Button>
          </div>
        </div>

        <div className="space-y-5 p-5 sm:p-7">
          {resourceQuery.isLoading ? (
            <p className="rounded-lg border bg-muted/30 px-4 py-3 text-sm text-muted-foreground" role="status">{t("projects.contactCard.loading")}</p>
          ) : null}
          {resourceQuery.isError ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-4" role="alert">
              <p className="text-sm text-destructive">{t("projects.contactCard.loadError")}</p>
              <Button type="button" variant="outline" size="sm" onClick={() => void resourceQuery.refetch()}>{t("common.retry")}</Button>
            </div>
          ) : null}

          <div className="grid gap-4 lg:grid-cols-2">
            <InformationSection icon={Mail} title={t("projects.contactCard.contactInformation")}>
              <InformationRow icon={Mail} label={t("projects.contactCard.email")} value={displayEmail} href={displayEmail ? `mailto:${displayEmail}` : undefined} emptyValue={resourceQuery.isError ? t("projects.contactCard.detailsUnavailable") : t("projects.contactCard.notProvided")} />
              <InformationRow icon={Phone} label={t("projects.contactCard.mobilePhone")} value={resourceQuery.isError ? null : phone} href={phoneHref} emptyValue={resourceQuery.isError ? t("projects.contactCard.detailsUnavailable") : t("projects.contactCard.notProvided")} />
            </InformationSection>

            <InformationSection icon={Building2} title={t("projects.contactCard.administrativeInformation")}>
              {resourceType?.requiresVendor ? (
                <InformationRow
                  icon={Briefcase}
                  label={t("projects.contactCard.vendor")}
                  value={resourceQuery.isError ? null : resource?.vendorName}
                  emptyValue={resourceQuery.isError ? t("projects.contactCard.detailsUnavailable") : t("projects.contactCard.notProvided")}
                  iconToneClassName="bg-warning/10 text-warning"
                  valueToneClassName="text-warning"
                />
              ) : null}
              {!person.resourceId ? (
                <p className="py-3 text-sm leading-6 text-muted-foreground">{t("projects.contactCard.resourceProfileUnavailable")}</p>
              ) : resourceQuery.isLoading ? (
                <p className="py-3 text-sm text-muted-foreground" role="status">{t("projects.contactCard.loading")}</p>
              ) : resourceQuery.isError ? (
                <p className="py-3 text-sm text-muted-foreground">{t("projects.contactCard.detailsUnavailable")}</p>
              ) : !hasAdministrativeAssociation ? (
                <p className="py-3 text-sm leading-6 text-muted-foreground">{t("projects.contactCard.noAdministrativeEntity")}</p>
              ) : (
                <>
                  <InformationRow icon={Building2} label={t("projects.contactCard.administrativeEntity")} value={resourceQuery.isError ? null : resource?.primaryOrganizationalUnitName} emptyValue={resourceQuery.isError ? t("projects.contactCard.detailsUnavailable") : t("projects.contactCard.notProvided")} />
                  {managerResourceId && managerQuery.isLoading ? (
                    <p className="py-3 text-sm text-muted-foreground" role="status">{t("projects.contactCard.loading")}</p>
                  ) : managerResourceId && manager ? (
                    <>
                      <InformationRow icon={UserRound} label={t("projects.contactCard.reportingManager")} value={manager.name} emptyValue={t("projects.contactCard.notProvided")} />
                      <InformationRow icon={Mail} label={t("projects.contactCard.managerEmail")} value={manager.email} href={manager.email ? `mailto:${manager.email}` : undefined} emptyValue={t("projects.contactCard.notProvided")} />
                    </>
                  ) : (
                    <p className="py-3 text-sm text-muted-foreground">{managerResourceId ? t("projects.contactCard.managerUnavailable") : t("projects.contactCard.noReportingManager")}</p>
                  )}
                </>
              )}
            </InformationSection>
          </div>
        </div>
    </Dialog>
  );
}
