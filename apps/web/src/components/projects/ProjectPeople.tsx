"use client";

import { FormEvent, useMemo, useState } from "react";
import Link from "next/link";
import { Edit3, Mail, Plus, UserRound, UserX, AlertCircle, RotateCcw, Crown } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { EmptyState, ErrorState, LoadingSkeleton, PermissionDenied } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ApiError } from "@/lib/api";
import { useAddProjectPerson, useProjectPeople, useRemoveProjectPerson, useUpdateProjectPerson, type CreateProjectPersonDto, type ProjectPersonDto } from "@/hooks/projects/useProjects";
import { useProjectRoles } from "@/hooks/resources/useResources";
import { useRevokeStakeholderInvitation, useSendStakeholderInvitation, useStakeholderInvitation } from "@/hooks/projects/useStakeholderInvitations";
import { SearchableResourceSelect } from "./SearchableResourceSelect";
import { useManagementPerspective } from "@/lib/perspective-context";

type Relationship = "TeamMember" | "Stakeholder" | "Sponsor" | "Other";
type FormState = CreateProjectPersonDto & { rowVersion?: string; source?: string };
const emptyForm: FormState = { resourceId: "", relationshipType: "TeamMember", role: "", allocationPercentage: 0, hoursPerWeek: undefined, startDate: "", endDate: "", isBillable: true, interest: "", influence: "", projectRoleIds: [] };

const relationshipOptions: Relationship[] = ["TeamMember", "Stakeholder", "Sponsor", "Other"];

function relationshipLabel(value: string, t: ReturnType<typeof useTranslations<"projects">>) {
  return value === "TeamMember" ? t("teamMember") : value === "Stakeholder" ? t("stakeholder") : value === "Sponsor" ? t("sponsor") : value === "Other" ? t("otherRelationship") : value;
}

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : "";
}

function getSubmitButtonText(
  sheet: "add" | "edit" | null,
  form: FormState,
  t: ReturnType<typeof useTranslations<"projects">>,
  historicalRelationships: { relationshipType: string; status: string }[]
) {
  if (sheet === "edit") return t("saveProjectPerson");
  if (!form.resourceId) return t("addToProject");

  const hasHistorical = historicalRelationships.some(
    (h) => h.relationshipType === form.relationshipType && h.status !== "Active"
  );

  if (hasHistorical) {
    if (form.relationshipType === "TeamMember") return t("reactivate");
    if (form.relationshipType === "Stakeholder" || form.relationshipType === "Other") return t("addBackToProject");
    if (form.relationshipType === "Sponsor") return t("assignSponsor");
  }

  if (form.relationshipType === "Sponsor") return t("assignSponsor");
  return t("addToProject");
}

export function ProjectPeople({ projectId, enabled = true, canManage = false }: { projectId: string; enabled?: boolean; canManage?: boolean }) {
  const t = useTranslations("projects");
  const locale = useLocale();
  const { effectiveReadOnly } = useManagementPerspective();
  const query = useProjectPeople(projectId, enabled);
  const [filter, setFilter] = useState<"All" | Relationship>("All");
  const [sheet, setSheet] = useState<"add" | "edit" | null>(null);
  const [selected, setSelected] = useState<ProjectPersonDto | null>(null);
  const [inviteSelected, setInviteSelected] = useState<ProjectPersonDto | null>(null);
  const [removeCandidate, setRemoveCandidate] = useState<ProjectPersonDto | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [frontendError, setFrontendError] = useState<string | null>(null);

  const add = useAddProjectPerson(projectId);
  const update = useUpdateProjectPerson(projectId, selected?.id ?? "");
  const remove = useRemoveProjectPerson(projectId);
  const projectRoles = useProjectRoles();
  const invitation = useStakeholderInvitation(projectId, inviteSelected?.id ?? "", !!inviteSelected);
  const sendInvitation = useSendStakeholderInvitation(projectId, inviteSelected?.id ?? "");
  const revokeInvitation = useRevokeStakeholderInvitation(projectId, inviteSelected?.id ?? "");
  const items = query.data ?? [];
  const visibleItems = filter === "All" ? items : items.filter((item) => item.relationshipType === filter);
  const mutationError = add.error ?? update.error ?? remove.error ?? sendInvitation.error ?? revokeInvitation.error;
  const errorMessage = mutationError instanceof ApiError ? mutationError.message : mutationError instanceof Error ? mutationError.message : null;

  const activeProjectPeople = useMemo(() =>
    items
      .filter((item) => item.status === "Active" && item.resourceId)
      .map((item) => ({
        resourceId: item.resourceId!,
        relationshipType: item.relationshipType,
      })),
    [items]
  );

  const currentSponsor = useMemo(() =>
    items.find((item) => item.relationshipType === "Sponsor" && item.status === "Active" && item.resourceId),
    [items]
  );

  const selectedResource = useMemo(() =>
    form.resourceId ? items.find((item) => item.resourceId === form.resourceId) : null,
    [form.resourceId, items]
  );

  const historicalRelationships = useMemo(() => {
    if (!form.resourceId) return [];
    return items
      .filter((item) => item.resourceId === form.resourceId && item.status !== "Active")
      .map((item) => ({ relationshipType: item.relationshipType, status: item.status }));
  }, [form.resourceId, items]);

  const hasHistoricalForSelectedRelationship = useMemo(() =>
    historicalRelationships.some((h) => h.relationshipType === form.relationshipType),
    [historicalRelationships, form.relationshipType]
  );

  const isSponsorConflict = useMemo(() =>
    form.relationshipType === "Sponsor" && currentSponsor !== undefined,
    [form.relationshipType, currentSponsor]
  );

  const isActiveDuplicate = useMemo(() =>
    form.resourceId && activeProjectPeople.some(
      (p) => p.resourceId === form.resourceId && p.relationshipType === form.relationshipType
    ),
    [form.resourceId, activeProjectPeople, form.relationshipType]
  );

  const closeSheet = () => {
    setSheet(null);
    setSelected(null);
    setForm(emptyForm);
    setFrontendError(null);
  };

  const closeInviteSheet = () => {
    setInviteSelected(null);
    sendInvitation.reset();
    revokeInvitation.reset();
  };

  const openAdd = () => {
    if (effectiveReadOnly) return;
    setSelected(null);
    setForm({ ...emptyForm, startDate: new Date().toISOString().slice(0, 10) });
    setSheet("add");
    setFrontendError(null);
  };

  const openEdit = (item: ProjectPersonDto) => {
    if (!item.resourceId || item.source === "Governance" || item.isLegacy) return;
    setSelected(item);
    setForm({
      resourceId: item.resourceId,
      relationshipType: item.relationshipType as Relationship,
      role: item.role ?? "",
      allocationPercentage: item.allocationPercentage ?? 0,
      hoursPerWeek: item.hoursPerWeek ?? undefined,
      startDate: dateValue(item.startDate),
      endDate: dateValue(item.endDate),
      isBillable: item.isBillable ?? true,
      interest: item.interest ?? "",
      influence: item.influence ?? "",
      projectRoleIds: item.functionalRoles?.map(r => r.id) ?? [],
      rowVersion: item.rowVersion,
      source: item.source,
    });
    setSheet("edit");
    setFrontendError(null);
  };

  const setField = <K extends keyof FormState>(field: K, value: FormState[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
    if (field === "resourceId" || field === "relationshipType") {
      setFrontendError(null);
    }
  };

  const validateForm = (): boolean => {
    if (!form.resourceId) {
      setFrontendError(t("selectExistingResource"));
      return false;
    }
    if (isActiveDuplicate) {
      setFrontendError(t("alreadyInProject"));
      return false;
    }
    if (isSponsorConflict) {
      setFrontendError(t("sponsorAlreadyAssigned"));
      return false;
    }
    if (form.relationshipType === "Sponsor") {
      const resource = selectedResource;
      if (resource && !resource.hasSystemUser) {
        setFrontendError(t("sponsorRequiresSystemUser"));
        return false;
      }
    }
    if (form.relationshipType === "TeamMember") {
      const allocation = form.allocationPercentage ?? 0;
      if (allocation < 0 || allocation > 100) {
        setFrontendError("Allocation must be between 0 and 100");
        return false;
      }
      if (form.startDate && form.endDate && new Date(form.endDate) <= new Date(form.startDate)) {
        setFrontendError("End date must be after start date");
        return false;
      }
    }
    return true;
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFrontendError(null);

    if (!validateForm()) return;

    if (sheet === "add") {
      await add.mutateAsync({
        ...form,
        relationshipType: form.relationshipType,
        role: form.role || undefined,
        allocationPercentage: form.relationshipType === "TeamMember" ? Number(form.allocationPercentage ?? 0) : undefined,
        hoursPerWeek: form.relationshipType === "TeamMember" ? (form.hoursPerWeek === undefined ? undefined : Number(form.hoursPerWeek)) : undefined,
        startDate: form.relationshipType === "TeamMember" ? form.startDate || undefined : undefined,
        endDate: form.relationshipType === "TeamMember" ? form.endDate || undefined : undefined,
        isBillable: form.relationshipType === "TeamMember" ? form.isBillable : undefined,
        projectRoleIds: form.projectRoleIds && form.projectRoleIds.length > 0 ? form.projectRoleIds : undefined,
      });
    } else if (sheet === "edit" && selected) {
      await update.mutateAsync({
        source: selected.source,
        relationshipType: selected.relationshipType,
        role: form.role || undefined,
        allocationPercentage: selected.relationshipType === "TeamMember" ? Number(form.allocationPercentage ?? 0) : undefined,
        hoursPerWeek: selected.relationshipType === "TeamMember" && form.hoursPerWeek !== undefined ? Number(form.hoursPerWeek) : undefined,
        startDate: selected.relationshipType === "TeamMember" ? form.startDate || undefined : undefined,
        endDate: selected.relationshipType === "TeamMember" ? form.endDate || undefined : undefined,
        isBillable: selected.relationshipType === "TeamMember" ? form.isBillable : undefined,
        interest: form.interest || undefined,
        influence: form.influence || undefined,
        rowVersion: selected.rowVersion,
        projectRoleIds: form.projectRoleIds,
      });
    }
    closeSheet();
  };

  const removeItem = (item: ProjectPersonDto) => setRemoveCandidate(item);
  const confirmRemove = async () => {
    if (!removeCandidate) return;
    await remove.mutateAsync({ personId: removeCandidate.id, source: removeCandidate.source, rowVersion: removeCandidate.rowVersion });
    setRemoveCandidate(null);
  };

  if (!enabled) return <Card className="shadow-none"><CardContent className="py-8"><PermissionDenied title={t("projectPeoplePermissionRequired")} /></CardContent></Card>;
  if (query.isLoading) return <Card className="shadow-none"><CardContent className="p-4"><LoadingSkeleton rows={4} /></CardContent></Card>;
  if (query.isError) return <Card className="shadow-none"><CardContent className="p-4"><ErrorState title={t("projectPeopleLoadError")} onRetry={() => void query.refetch()} /></CardContent></Card>;

  return (
    <>
      <Card className="shadow-none">
        <CardHeader className="flex flex-row items-start justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold">{t("projectPeople")}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{t("projectPeopleDescription")}</p>
          </div>
          {(canManage && !effectiveReadOnly) ? (
            <Button size="sm" onClick={openAdd} disabled={effectiveReadOnly}>
              <Plus className="me-1.5 h-3.5 w-3.5" />{t("addProjectPerson")}
            </Button>
          ) : null}
        </CardHeader>
        <CardContent>
          <div className="mb-3 flex flex-wrap gap-2">
            {(["All", ...relationshipOptions] as const).map((value) => (
              <Button
                key={value}
                size="sm"
                variant={filter === value ? "default" : "outline"}
                onClick={() => setFilter(value)}
              >
                {value === "All" ? t("all") : relationshipLabel(value, t)}
              </Button>
            ))}
          </div>
          {visibleItems.length === 0 ? (
            <EmptyState icon={UserRound} title={t("noProjectPeople")} description={t("noProjectPeopleDescription")} />
          ) : (
            <Table containerClassName="rounded-lg">
              <TableHeader>
                <TableRow>
                  <TableHead>{t("name")}</TableHead>
                  <TableHead>{t("projectRelationship")}</TableHead>
                  <TableHead>{t("resourceType")}</TableHead>
                  <TableHead>{t("systemUser")}</TableHead>
                  <TableHead>{t("allocationPercentage")}</TableHead>
                  <TableHead>{t("status")}</TableHead>
                  {canManage ? <TableHead className="text-end">{t("actions")}</TableHead> : null}
                </TableRow>
              </TableHeader>
              <TableBody>
                {visibleItems.map((item) => {
                  const canRemove = item.source === "TeamMember"
                    ? item.status === "Active" && item.relationshipType !== "ProjectManager"
                    : item.source === "Stakeholder" && item.status !== "Revoked";
                  return (
                    <TableRow key={`${item.source}-${item.id}`} className={item.status === "Revoked" ? "opacity-60" : undefined}>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary">
                            <UserRound className="h-3.5 w-3.5" />
                          </div>
                          <div>
                            <div className="font-medium">{item.name}</div>
                            <div className="text-xs text-muted-foreground">{item.email || "—"}</div>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{relationshipLabel(item.relationshipType, t)}</Badge>
                        <div className="mt-1 text-xs text-muted-foreground">{item.role || "—"}</div>
                        {item.functionalRoles && item.functionalRoles.length > 0 && (
                          <div className="mt-1 flex flex-wrap gap-1">
                            {item.functionalRoles.map(fr => (
                              <Badge key={fr.id} variant="secondary" className="text-[10px] px-1.5 py-0">{fr.nameEn}</Badge>
                            ))}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{item.resourceType || "—"}</TableCell>
                      <TableCell>{item.hasSystemUser ? t("yes") : t("no")}</TableCell>
                      <TableCell>
                        {item.relationshipType === "TeamMember" && item.allocationPercentage !== null && item.allocationPercentage !== undefined
                          ? `${item.allocationPercentage}%`
                          : t("notApplicable")}
                      </TableCell>
                      <TableCell><Badge status={item.status}>{item.status}</Badge></TableCell>
                      {canManage ? (
                        <TableCell>
                          <div className="flex justify-end gap-1">
                            {item.source === "Stakeholder" &&
                              item.relationshipType === "Stakeholder" &&
                              item.status === "Active" &&
                              !item.hasSystemUser &&
                              !!item.email ? (
                              <Button
                                size="icon"
                                variant="ghost"
                                onClick={() => { setInviteSelected(item); }}
                                aria-label={t("inviteStakeholder")}
                              >
                                <Mail className="h-3.5 w-3.5" />
                              </Button>
                            ) : null}
                            {canRemove ? (
                              <Button
                                size="icon"
                                variant="ghost"
                                onClick={() => removeItem(item)}
                                aria-label={t("removeProjectPerson")}
                              >
                                <UserX className="h-3.5 w-3.5" />
                              </Button>
                            ) : null}
                            {item.source === "TeamMember" && item.status === "Active" ? (
                              <Button
                                size="icon"
                                variant="ghost"
                                onClick={() => openEdit(item)}
                                aria-label={t("editProjectPerson")}
                              >
                                <Edit3 className="h-3.5 w-3.5" />
                              </Button>
                            ) : null}
                          </div>
                        </TableCell>
                      ) : null}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
          {errorMessage ? <p className="mt-3 text-sm text-destructive" role="alert">{errorMessage}</p> : null}
        </CardContent>
      </Card>

      <Dialog open={!!removeCandidate} onOpenChange={(open) => { if (!open && !remove.isPending) setRemoveCandidate(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("removeProjectPerson")}</DialogTitle>
            <DialogDescription>{t("removeProjectPersonDescription", { name: removeCandidate?.name ?? "" })}</DialogDescription>
          </DialogHeader>
          <DialogBody>
            <p className="rounded-lg bg-muted/30 p-3 text-sm text-muted-foreground">{t("removeProjectPersonHistoryHint")}</p>
            {remove.error ? <p className="mt-3 whitespace-pre-line text-sm text-destructive" role="alert">{errorMessage}</p> : null}
          </DialogBody>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setRemoveCandidate(null)} disabled={remove.isPending}>{t("cancel")}</Button>
            <Button type="button" variant="destructive" onClick={() => void confirmRemove()} disabled={remove.isPending}>{remove.isPending ? t("submitting") : t("removeProjectPerson")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <SideSheet
        open={sheet !== null}
        onOpenChange={(open) => { if (!open) closeSheet(); }}
        title={sheet === "add" ? t("addProjectPerson") : t("editProjectPerson")}
        description={t("projectPeopleDescription")}
        size="lg"
      >
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void submit(event)}>
          <SideSheetBody className="space-y-5">
            {sheet === "add" ? (
              <section className="space-y-3">
                <div>
                  <h3 className="text-sm font-semibold">{t("selectExistingResource")}</h3>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {t("managePeopleInResources")} <Link className="font-medium text-primary underline" href={`/${locale}/resources`}>{t("resource")}</Link>
                  </p>
                </div>
                <SearchableResourceSelect
                  value={form.resourceId}
                  onValueChange={(value) => setField("resourceId", value)}
                  placeholder={t("selectExistingResource")}
                  searchPlaceholder={t("searchPeople")}
                  emptyText={t("noMatchingPeople")}
                  clearable={true}
                  activeProjectPeople={activeProjectPeople}
                  historicalRelationships={historicalRelationships}
                  currentSponsor={currentSponsor ? { resourceId: currentSponsor.resourceId! } : null}
                  selectedRelationshipType={form.relationshipType}
                  disabled={effectiveReadOnly}
                />
              </section>
            ) : (
              <section className="rounded-lg border bg-muted/20 p-3">
                <p className="font-medium">{selected?.name}</p>
                <p className="text-xs text-muted-foreground">{selected?.email || "—"} · {selected?.resourceType || "—"}</p>
                <p className="mt-2 text-xs text-muted-foreground">{t("projectPeopleEditIdentityHint")}</p>
              </section>
            )}

            {form.resourceId && (
              <section className="space-y-3">
                <div className="rounded-lg border bg-muted/20 p-3">
                  <p className="text-xs font-semibold text-muted-foreground">{t("selectedPerson")}</p>
                  <div className="flex items-center gap-3 mt-2">
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary">
                      <UserRound className="h-4 w-4" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">{selectedResource?.name || "—"}</p>
                      <p className="text-xs text-muted-foreground truncate">{selectedResource?.email || "—"}</p>
                      <div className="flex flex-wrap gap-2 mt-1">
                        {selectedResource?.resourceType && <Badge variant="outline" className="text-xs">{selectedResource.resourceType}</Badge>}
                        {selectedResource?.jobTitle && <Badge variant="secondary" className="text-xs">{selectedResource.jobTitle}</Badge>}
                        {selectedResource?.hasSystemUser && <Badge variant="outline" className="text-xs text-primary">{t("systemUser")}</Badge>}
                        {hasHistoricalForSelectedRelationship && <Badge variant="secondary" className="text-xs text-amber-600 dark:text-amber-400"><RotateCcw className="me-1 h-2.5 w-2.5" />{t("previouslyInProjectBadge")}</Badge>}
                        {isActiveDuplicate && <Badge variant="destructive" className="text-xs">{t("alreadyInProject")}</Badge>}
                      </div>
                    </div>
                  </div>
                </div>
              </section>
            )}

            {hasHistoricalForSelectedRelationship && (
              <section className="space-y-2 rounded-lg border border-amber-200 bg-amber-50 p-3">
                <div className="flex items-start gap-2">
                  <AlertCircle className="h-4 w-4 mt-0.5 text-amber-600 dark:text-amber-400 shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium text-amber-800 dark:text-amber-200">{t("previouslyInProject")}</p>
                    <p className="mt-1 text-xs text-amber-700 dark:text-amber-300">{t("previouslyInProjectDescription")}</p>
                  </div>
                </div>
              </section>
            )}

            {isSponsorConflict && (
              <section className="space-y-2 rounded-lg border border-destructive/20 bg-destructive/5 p-3">
                <div className="flex items-start gap-2">
                  <Crown className="h-4 w-4 mt-0.5 text-destructive shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium text-destructive">{t("sponsorAlreadyAssigned")}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{t("sponsorAlreadyAssignedDescription")}</p>
                  </div>
                </div>
              </section>
            )}

            <section className="space-y-3">
              <Label>{t("projectRelationship")}</Label>
              <Select
                value={form.relationshipType}
                onValueChange={(value) => setField("relationshipType", value)}
                disabled={sheet === "edit" || effectiveReadOnly}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {relationshipOptions.map((value) => <SelectItem key={value} value={value}>{relationshipLabel(value, t)}</SelectItem>)}
                </SelectContent>
              </Select>
            </section>

            <section className="space-y-3">
              <Label htmlFor="project-person-role">{t("role")}</Label>
              <Input
                id="project-person-role"
                value={form.role ?? ""}
                onChange={(event) => setField("role", event.target.value)}
                placeholder={relationshipLabel(form.relationshipType, t)}
                disabled={effectiveReadOnly}
              />
            </section>

            {form.relationshipType === "TeamMember" && projectRoles.data && projectRoles.data.length > 0 && (
              <section className="space-y-3">
                <Label>{t("functionalRoles", { defaultValue: "Functional Roles" })}</Label>
                <p className="text-xs text-muted-foreground">{t("functionalRolesDescription", { defaultValue: "Select one or more functional project roles" })}</p>
                <div className="flex flex-wrap gap-2">
                  {projectRoles.data.map((role) => {
                    const isSelected = form.projectRoleIds?.includes(role.id) ?? false;
                    return (
                      <button
                        key={role.id}
                        type="button"
                        disabled={effectiveReadOnly}
                        onClick={() => {
                          const currentIds = form.projectRoleIds ?? [];
                          const newIds = isSelected
                            ? currentIds.filter(id => id !== role.id)
                            : [...currentIds, role.id];
                          setField("projectRoleIds", newIds);
                        }}
                        className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                          isSelected
                            ? "border-primary bg-primary text-primary-foreground"
                            : "border-border bg-background text-foreground hover:bg-muted"
                        } ${effectiveReadOnly ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
                      >
                        {role.nameEn}
                      </button>
                    );
                  })}
                </div>
                {form.projectRoleIds && form.projectRoleIds.length > 0 && (
                  <p className="text-xs text-muted-foreground">
                    {form.projectRoleIds.length} role{form.projectRoleIds.length !== 1 ? "s" : ""} selected
                  </p>
                )}
              </section>
            )}

            {form.relationshipType === "TeamMember" ? (
              <section className="space-y-3 rounded-lg border p-3">
                <p className="text-xs font-semibold">{t("allocationPercentage")}</p>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1">
                    <Label htmlFor="project-person-allocation">{t("allocationPercentage")}</Label>
                    <Input
                      id="project-person-allocation"
                      type="number"
                      min="0"
                      max="100"
                      step="0.01"
                      value={form.allocationPercentage ?? 0}
                      onChange={(event) => setField("allocationPercentage", Number(event.target.value))}
                      disabled={effectiveReadOnly}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="project-person-hours">{t("hoursPerWeek")}</Label>
                    <Input
                      id="project-person-hours"
                      type="number"
                      min="0"
                      step="0.25"
                      value={form.hoursPerWeek ?? ""}
                      onChange={(event) => setField("hoursPerWeek", event.target.value === "" ? undefined : Number(event.target.value))}
                      disabled={effectiveReadOnly}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="project-person-start">{t("startDate")}</Label>
                    <Input
                      id="project-person-start"
                      type="date"
                      value={form.startDate ?? ""}
                      onChange={(event) => setField("startDate", event.target.value)}
                      disabled={effectiveReadOnly}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="project-person-end">{t("endDate")}</Label>
                    <Input
                      id="project-person-end"
                      type="date"
                      value={form.endDate ?? ""}
                      onChange={(event) => setField("endDate", event.target.value)}
                      disabled={effectiveReadOnly}
                    />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label htmlFor="project-person-billable" className="flex items-center gap-2 cursor-pointer">
                    <Input
                      id="project-person-billable"
                      type="checkbox"
                      checked={form.isBillable ?? true}
                      onChange={(event) => setField("isBillable", event.target.checked)}
                      disabled={effectiveReadOnly}
                    />
                    <span className="text-sm">{t("isBillable")}</span>
                  </Label>
                </div>
              </section>
            ) : (
              <p className="rounded-lg bg-muted/30 p-3 text-xs text-muted-foreground">
                {form.relationshipType === "Stakeholder" ? t("relationshipStakeholderDescription") :
                 form.relationshipType === "Sponsor" ? t("relationshipSponsorDescription") :
                 t("relationshipOtherDescription")}
              </p>
            )}

            {(form.relationshipType === "Stakeholder" || form.relationshipType === "Other") && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="project-person-interest">{t("stakeholderInterest")}</Label>
                  <Input
                    id="project-person-interest"
                    value={form.interest ?? ""}
                    onChange={(event) => setField("interest", event.target.value)}
                    disabled={effectiveReadOnly}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="project-person-influence">{t("stakeholderInfluence")}</Label>
                  <Input
                    id="project-person-influence"
                    value={form.influence ?? ""}
                    onChange={(event) => setField("influence", event.target.value)}
                    disabled={effectiveReadOnly}
                  />
                </div>
              </div>
            )}

            {frontendError && (
              <section className="rounded-lg border border-destructive bg-destructive/5 p-3">
                <p className="text-sm text-destructive" role="alert">{frontendError}</p>
              </section>
            )}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={closeSheet} disabled={add.isPending || update.isPending}>{t("cancel")}</Button>
            <Button
              type="submit"
              disabled={add.isPending || update.isPending || !form.resourceId || effectiveReadOnly || isActiveDuplicate || isSponsorConflict}
            >
              {add.isPending || update.isPending ? t("submitting") : getSubmitButtonText(sheet, form, t, historicalRelationships)}
            </Button>
          </SideSheetFooter>
        </form>
      </SideSheet>

      <SideSheet open={!!inviteSelected} onOpenChange={(open) => { if (!open) closeInviteSheet(); }} title={t("inviteStakeholder")} description={t("inviteStakeholderDescription")} size="md">
        <div className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-4">
            <section className="space-y-3 rounded-lg border bg-muted/20 p-4">
              <div><p className="text-xs text-muted-foreground">{t("name")}</p><p className="font-medium">{inviteSelected?.name}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("email")}</p><p className="font-medium">{inviteSelected?.email}</p></div>
              <div className="grid gap-3 sm:grid-cols-2"><div><p className="text-xs text-muted-foreground">{t("resourceType")}</p><p>{inviteSelected?.resourceType}</p></div><div><p className="text-xs text-muted-foreground">{t("projectRelationship")}</p><p>{t("stakeholder")}</p></div></div>
            </section>
            <p className="text-xs text-muted-foreground">{t("inviteCanonicalResourceHint")}</p>
            {invitation.isLoading ? <LoadingSkeleton rows={2} /> : invitation.data?.status === "Pending" ? <section className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm"><p className="font-medium">{t("invitationPending")}</p><p className="text-xs">{new Date(invitation.data.expiresAt).toLocaleString()}</p>{invitation.data.invitationUrl ? <div className="break-all rounded bg-background p-2 text-xs">{invitation.data.invitationUrl}</div> : null}</section> : invitation.data ? <p className="text-sm text-muted-foreground">{t("invitationStatus", { status: invitation.data.status })}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            {invitation.data?.status === "Pending" ? <Button type="button" variant="outline" onClick={() => { if (window.confirm(t("revokeInvitationConfirm"))) void revokeInvitation.mutateAsync(); }}>{revokeInvitation.isPending ? t("submitting") : t("revokeInvitation")}</Button> : null}
            <Button type="button" onClick={() => void sendInvitation.mutateAsync()} disabled={sendInvitation.isPending || !inviteSelected}>{sendInvitation.isPending ? t("submitting") : invitation.data?.status === "Pending" ? t("resendInvitation") : t("inviteStakeholder")}</Button>
          </SideSheetFooter>
        </div>
      </SideSheet>
    </>
  );
}