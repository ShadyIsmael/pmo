import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { useDebouncedCallback } from "use-debounce";
import { ContactRound, Mail, Pencil, Plus, UserMinus, UserRound } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Checkbox } from "@components/ui/Checkbox";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Badge } from "@components/ui/Badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { EmptyState } from "@components/ui/StateDisplay";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { formatDateTime, getLookupTextClass } from "@lib/utils";
import { getResourceTypeTextClass, getSystemUserTextClass } from "@lib/status-presentation";
import { useManagement } from "@api/management";
import { useResources } from "@features/resources/api/resources";
import { useResourceAvailability } from "@features/resources/api/capacity";
import { useTeams } from "@features/teams/api/teams";
import { useProjectRoles } from "@features/administration/api/admin";
import { useAddProjectPerson, useCheckResourceConflicts, useProjectPeople, useRemoveProjectPerson, useUpdateProjectPerson, type ProjectPersonDto } from "./api/projects";
import { ProjectTeamContactCard } from "./ProjectTeamContactCard";
import { useRevokeStakeholderInvitation, useSendStakeholderInvitation, useStakeholderInvitation } from "./api/stakeholder-invitations";
import { getProjectPersonSubmitAction, hasActiveProjectPerson, hasInvalidProjectPersonDateRange } from "./project-member-rules";

type Context = { projectId: string; project?: { sponsorId?: string | null } };
type PersonForm = { targetType: "resource" | "team"; resourceId: string; teamId: string; relationshipType: string; role: string; allocationPercentage: string; startDate: string; endDate: string; isBillable: boolean; interest: string; influence: string; projectRoleIds: string[] };
const blankForm = (): PersonForm => ({ targetType: "resource", resourceId: "", teamId: "", relationshipType: "TeamMember", role: "", allocationPercentage: "", startDate: new Date().toISOString().slice(0, 10), endDate: "", isBillable: true, interest: "", influence: "", projectRoleIds: [] });

const RELATIONSHIP_FILTERS = ["All", "TeamMember", "Stakeholder", "Sponsor", "Other"] as const;

function canEditPerson(person: ProjectPersonDto): boolean {
  return person.source === "TeamMember" && person.relationshipType !== "Sponsor" && person.status === "Active" && Boolean(person.resourceId || person.teamId) && !person.isLegacy;
}

function canInvitePerson(person: ProjectPersonDto): boolean {
  return person.source === "Stakeholder" && person.relationshipType === "Stakeholder" && person.status === "Active" && !person.hasSystemUser && Boolean(person.email);
}

function formatInvitationExpiry(value: string, locale: "en" | "ar"): string {
  return formatDateTime(value, locale);
}

function invitationEmailStatusLabel(status: string, t: (key: string) => string): string {
  const keyByStatus: Record<string, string> = {
    pending: "projects.invitationEmailStatusPending",
    sent: "projects.invitationEmailStatusSent",
    failed: "projects.invitationEmailStatusFailed",
    untracked: "projects.invitationEmailStatusUntracked",
  };
  const key = keyByStatus[String(status ?? "").trim().toLowerCase()];
  return t(key ?? "projects.invitationEmailStatusUnknown");
}

function canRemovePerson(person: ProjectPersonDto): boolean {
  if (person.source === "TeamMember") return person.status === "Active" && person.relationshipType === "TeamMember";
  return person.source === "Stakeholder" && person.status !== "Revoked";
}

function canManagePerson(person: ProjectPersonDto, canManageProjectTeam: boolean, canManageStakeholders: boolean, canViewTeams: boolean): boolean {
  if (person.source === "TeamMember") return canManageProjectTeam && (!person.teamId || canViewTeams);
  return canManageStakeholders;
}

function hasRelationship(person: ProjectPersonDto, relationshipType: string): boolean {
  return person.relationshipType === relationshipType || (person.additionalRelationshipTypes ?? []).includes(relationshipType);
}

function hasContactCard(person: ProjectPersonDto): boolean {
  return (person.source === "TeamMember" && Boolean(person.resourceId)) ||
    person.relationshipType === "ProjectManager" ||
    hasRelationship(person, "Sponsor");
}

function relationshipLabel(relationshipType: string, t: (key: string) => string): string {
  const keyByRelationship: Record<string, string> = {
    ProjectManager: "projects.projectManager",
    TeamMember: "projects.teamMember",
    Stakeholder: "projects.stakeholder",
    Sponsor: "projects.sponsor",
    Other: "projects.otherRelationship",
  };
  const key = keyByRelationship[relationshipType];
  return key ? t(key) : relationshipType;
}

export function ProjectMembersTab() {
  const { projectId } = useOutletContext<Context>();
  const { t, formatNumber, locale } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canViewPeople = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Project.View]));
  const canViewResourceContacts = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Resources.View]));
  const canViewTeams = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Team.View, Permissions.Team.Edit]));
  const canManageChanges = canMutate && !effectiveReadOnly;
  const canManageProjectTeam = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Resources.Assign]) && canManageChanges);
  const canManageStakeholders = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Stakeholders.Manage]) && canManageChanges);
  const canManage = canManageProjectTeam || (canManageStakeholders && canViewResourceContacts);
  const peopleQuery = useProjectPeople(projectId, Boolean(projectId && canViewPeople));
  const addPerson = useAddProjectPerson(projectId);
  const removePerson = useRemoveProjectPerson(projectId);
  const [open, setOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectPersonDto | null>(null);
  const [removeCandidate, setRemoveCandidate] = React.useState<ProjectPersonDto | null>(null);
  const [removeError, setRemoveError] = React.useState<string | null>(null);
  const [inviteSelected, setInviteSelected] = React.useState<ProjectPersonDto | null>(null);
  const [contactPerson, setContactPerson] = React.useState<ProjectPersonDto | null>(null);
  const invitation = useStakeholderInvitation(projectId, inviteSelected?.id ?? "", Boolean(inviteSelected));
  const sendInvitation = useSendStakeholderInvitation(projectId, inviteSelected?.id ?? "");
  const revokeInvitation = useRevokeStakeholderInvitation(projectId, inviteSelected?.id ?? "");
  const invitationMutationError = sendInvitation.error ?? revokeInvitation.error;
  const invitationMutationErrorMessage = invitationMutationError instanceof Error ? invitationMutationError.message : null;
  const [form, setForm] = React.useState<PersonForm>(blankForm);
  const [error, setError] = React.useState<string | null>(null);
  const [relationshipFilter, setRelationshipFilter] = React.useState<string>("All");
  const [resourceSearch, setResourceSearch] = React.useState("");
  const debouncedResourceSearch = useDebouncedCallback(setResourceSearch, 300);
  const [teamSearch, setTeamSearch] = React.useState("");
  const debouncedTeamSearch = useDebouncedCallback(setTeamSearch, 300);
  const resourcesQuery = useResources({ search: resourceSearch || undefined, page: 1, pageSize: 20, isActive: true }, open && !editing && (canManageProjectTeam || (canManageStakeholders && canViewResourceContacts)));
  const teamsQuery = useTeams({ search: teamSearch || undefined, page: 1, pageSize: 50, isActive: true }, open && !editing && form.targetType === "team" && canViewTeams);
  const rolesQuery = useProjectRoles(open);
  const updatePerson = useUpdateProjectPerson(projectId, editing?.id ?? "");
  const people = peopleQuery.data ?? [];
  const visiblePeople = relationshipFilter === "All" ? people : people.filter((p) => hasRelationship(p, relationshipFilter));
  const showActionColumn = visiblePeople.some((person) =>
    (canManagePerson(person, canManageProjectTeam, canManageStakeholders, canViewTeams) && (canRemovePerson(person) || canEditPerson(person)))
    || (canManageStakeholders && canInvitePerson(person))
    || (canViewResourceContacts && hasContactCard(person)));
  const resourceOptions = (resourcesQuery.data?.items ?? []).map((r) => ({ value: r.id, label: `${r.name}${r.jobTitle ? ` · ${r.jobTitle}` : ""}`, keywords: [r.email ?? "", r.type] }));
  const teamOptions = (teamsQuery.data?.items ?? []).map((team) => ({ value: team.id, label: team.name, keywords: [team.code ?? "", team.description ?? ""] }));
  const selectedResource = (resourcesQuery.data?.items ?? []).find((resource) => resource.id === form.resourceId);
  const selectedTeam = (teamsQuery.data?.items ?? []).find((team) => team.id === form.teamId);
  const capacityStartDate = form.startDate || new Date().toISOString().slice(0, 10);
  const capacityEndDate = React.useMemo(() => {
    const start = new Date(`${capacityStartDate}T00:00:00`);
    start.setDate(start.getDate() + 7);
    return start.toISOString().slice(0, 10);
  }, [capacityStartDate]);
  const resourceCapacityQuery = useResourceAvailability(form.targetType === "resource" ? form.resourceId : "", capacityStartDate, capacityEndDate);
  const hasHistoricalForSelectedRelationship = people.some((person) => person.resourceId === form.resourceId && person.relationshipType === form.relationshipType && person.status !== "Active");
  const sponsorTaken = people.some((p) => hasRelationship(p, "Sponsor") && p.status === "Active" && (!editing || p.id !== editing.id));
  const sponsorConflict = !editing && form.relationshipType === "Sponsor" && sponsorTaken;
  const activeDuplicate = !editing && (form.targetType === "resource"
    ? hasActiveProjectPerson(people, form.resourceId, form.relationshipType)
    : Boolean(form.teamId && people.some((person) => person.teamId === form.teamId && person.relationshipType === "TeamMember" && person.status === "Active")));
  const submitAction = getProjectPersonSubmitAction(people, Boolean(editing), form.resourceId, form.relationshipType);
  const proposedAllocation = form.allocationPercentage ? Number(form.allocationPercentage) : 0;
  const conflictCheckEnabled = open && form.relationshipType === "TeamMember" && form.targetType === "resource" && Number.isFinite(proposedAllocation) && proposedAllocation > 0 && Boolean(form.resourceId && form.startDate && form.endDate);
  const conflictsQuery = useCheckResourceConflicts(
    projectId,
    conflictCheckEnabled ? form.resourceId : "",
    conflictCheckEnabled ? form.startDate : "",
    conflictCheckEnabled ? form.endDate : "",
    conflictCheckEnabled ? proposedAllocation : 0,
    editing?.id,
    conflictCheckEnabled,
  );
  const allocationConflicts = conflictsQuery.data ?? [];

  const openForm = (person?: ProjectPersonDto) => {
    debouncedResourceSearch.cancel();
    debouncedTeamSearch.cancel();
    setResourceSearch("");
    setEditing(person ?? null);
    setForm(person ? {
      targetType: person.teamId ? "team" : "resource", resourceId: person.resourceId ?? "", teamId: person.teamId ?? "", relationshipType: person.relationshipType, role: person.role ?? "",
      allocationPercentage: person.allocationPercentage == null ? "" : String(person.allocationPercentage),
      startDate: person.startDate?.slice(0, 10) ?? "", endDate: person.endDate?.slice(0, 10) ?? "", isBillable: person.isBillable ?? true,
      interest: person.interest ?? "", influence: person.influence ?? "", projectRoleIds: (person.functionalRoles ?? []).map((r) => r.id),
    } : { ...blankForm(), relationshipType: canManageProjectTeam ? "TeamMember" : "Stakeholder" });
    setError(null); setOpen(true);
  };

  const closeForm = () => {
    debouncedResourceSearch.cancel();
    debouncedTeamSearch.cancel();
    setOpen(false);
    setEditing(null);
    setForm(blankForm());
    setError(null);
    setResourceSearch("");
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setError(null);
    if (!editing && form.targetType === "resource" && !form.resourceId) { setError(t("projects.selectExistingResource")); return; }
    if (!editing && form.relationshipType === "TeamMember" && form.targetType === "team" && !form.teamId) { setError(t("projects.selectExistingTeam")); return; }
    if (form.relationshipType === "TeamMember" && !form.allocationPercentage.trim()) { setError(t("projects.projectAllocationRequired")); return; }
    const allocation = form.allocationPercentage ? Number(form.allocationPercentage) : undefined;
    if (form.relationshipType === "TeamMember" && allocation !== undefined && (!Number.isFinite(allocation) || allocation < 0 || allocation > 100)) { setError(t("projects.allocationRangeInvalid")); return; }
    if (hasInvalidProjectPersonDateRange(form.relationshipType, form.startDate, form.endDate)) { setError(t("projects.endDateAfterStartDate")); return; }
    if (!editing && form.relationshipType === "Sponsor" && selectedResource && !selectedResource.userId) { setError(t("projects.sponsorRequiresSystemUser")); return; }
    if (sponsorConflict) { setError(t("projects.sponsorAlreadyAssigned")); return; }
    if (activeDuplicate) { setError(t("projects.alreadyInProject")); return; }
    try {
      if (editing) {
        await updatePerson.mutateAsync({ source: editing.source, teamId: editing.teamId ?? undefined, relationshipType: editing.source === "TeamMember" ? "TeamMember" : form.relationshipType, role: form.role || undefined, allocationPercentage: form.relationshipType === "TeamMember" ? allocation : undefined, startDate: form.relationshipType === "TeamMember" ? form.startDate || undefined : undefined, endDate: form.relationshipType === "TeamMember" ? form.endDate || undefined : undefined, isBillable: form.relationshipType === "TeamMember" ? form.isBillable : undefined, interest: form.interest || undefined, influence: form.influence || undefined, projectRoleIds: form.projectRoleIds, rowVersion: editing.rowVersion });
      } else {
        await addPerson.mutateAsync({ resourceId: form.targetType === "resource" ? form.resourceId : undefined, teamId: form.relationshipType === "TeamMember" && form.targetType === "team" ? form.teamId : undefined, relationshipType: form.relationshipType, role: form.role || undefined, allocationPercentage: form.relationshipType === "TeamMember" ? allocation : undefined, startDate: form.relationshipType === "TeamMember" ? form.startDate || undefined : undefined, endDate: form.relationshipType === "TeamMember" ? form.endDate || undefined : undefined, isBillable: form.relationshipType === "TeamMember" ? form.isBillable : undefined, interest: form.interest || undefined, influence: form.influence || undefined, projectRoleIds: form.projectRoleIds.length ? form.projectRoleIds : undefined });
      }
      closeForm();
    } catch (reason) { setError(reason instanceof Error ? reason.message : t("projects.projectPeopleLoadError")); }
  };

  const remove = (person: ProjectPersonDto) => {
    setRemoveCandidate(person);
    setRemoveError(null);
  };

  const confirmRemove = async () => {
    if (!removeCandidate) return;
    try {
      await removePerson.mutateAsync({ personId: removeCandidate.id, source: removeCandidate.source, teamId: removeCandidate.teamId, rowVersion: removeCandidate.rowVersion });
      setRemoveCandidate(null);
    } catch (reason) {
      setRemoveError(reason instanceof Error ? reason.message : t("projects.projectPeopleLoadError"));
    }
  };

  const closeInviteSheet = () => {
    setInviteSelected(null);
    sendInvitation.reset();
    revokeInvitation.reset();
  };

  if (!canViewPeople) return <Card><CardContent className="py-8 text-center text-sm text-muted-foreground">{t("projects.projectPeoplePermissionRequired")}</CardContent></Card>;

  return <>
  <Card>
    <CardHeader className="flex !flex-row items-center justify-between gap-3"><div><CardTitle>{t("projects.projectPeople")}</CardTitle><p className="mt-1 text-sm text-muted-foreground">{t("projects.projectPeopleDescription")}</p></div>{canManage ? <Button size="sm" onClick={() => openForm()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addProjectPerson")}</Button> : null}</CardHeader>
    <CardContent>
      <div className="mb-3 flex flex-wrap gap-1.5" role="group" aria-label={t("projects.projectRelationship")}>
        {RELATIONSHIP_FILTERS.map((rel) => <button key={rel} type="button" onClick={() => setRelationshipFilter(rel)} aria-pressed={relationshipFilter === rel} className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors ${relationshipFilter === rel ? "border-primary bg-primary/10 text-primary" : "border-input text-muted-foreground hover:bg-muted"}`}>{rel === "All" ? t("common.all") : t(`projects.${rel === "TeamMember" ? "teamMember" : rel === "Stakeholder" ? "stakeholder" : rel === "Sponsor" ? "sponsor" : "otherRelationship"}`)}</button>)}
      </div>
      {error ? <p className="mb-3 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
      {peopleQuery.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : peopleQuery.isError ? <div className="flex flex-col items-center gap-3 py-8 text-center" role="alert"><p className="text-sm text-destructive">{t("projects.projectPeopleLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void peopleQuery.refetch()}>{t("common.retry")}</Button></div> : visiblePeople.length === 0 ? <EmptyState icon={<UserRound className="h-8 w-8 text-muted-foreground/40" />} title={t("projects.noProjectPeople")} description={t("projects.noProjectPeopleDescription")} className="min-h-40 rounded-lg border border-dashed border-border bg-muted/20 px-6 py-8" /> : <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("projects.name")}</TableHead>
            <TableHead>{t("projects.projectRelationship")}</TableHead>
            <TableHead>{t("projects.resourceType")}</TableHead>
            <TableHead>{t("projects.systemUser")}</TableHead>
            <TableHead>{t("projects.allocationPercentage")}</TableHead>
            <TableHead>{t("projects.status")}</TableHead>
            {showActionColumn ? <TableHead className="text-end">{t("projects.actions")}</TableHead> : null}
          </TableRow>
        </TableHeader>
        <TableBody>
          {visiblePeople.map((person) => <TableRow key={`${person.source}-${person.id}`} className={person.status === "Revoked" ? "opacity-60" : undefined}>
            <TableCell>
              <div className="flex items-center gap-2">
                <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-3.5 w-3.5" /></div>
                <div><p className="font-medium">{person.name}</p><p className="text-xs text-muted-foreground">{person.email || "—"}</p></div>
              </div>
            </TableCell>
            <TableCell>
              <div className="flex flex-wrap items-center gap-2">
                <span className={`text-sm font-medium ${getLookupTextClass(person.relationshipType)}`}>{relationshipLabel(person.relationshipType, t)}</span>
                {(person.additionalRelationshipTypes ?? []).map((relationship) => <Badge key={relationship} variant="secondary" className={`px-1.5 py-0 text-[10px] ${getLookupTextClass(relationship)}`}>{relationshipLabel(relationship, t)}</Badge>)}
              </div>
              <p className="mt-1 text-xs text-muted-foreground">{person.role || "—"}</p>
              {(person.functionalRoles ?? []).length > 0 ? <div className="mt-1 flex flex-wrap gap-1">{person.functionalRoles?.map((role) => <Badge key={role.id} variant="secondary" className="px-1.5 py-0 text-[10px]">{role.nameEn}</Badge>)}</div> : null}
            </TableCell>
            <TableCell className={getResourceTypeTextClass(person.resourceType)}>{person.resourceType || "—"}</TableCell>
            <TableCell className={getSystemUserTextClass(person.hasSystemUser)}>{t(person.hasSystemUser ? "projects.yes" : "projects.no")}</TableCell>
            <TableCell>{person.relationshipType === "TeamMember" && person.allocationPercentage != null ? `${person.allocationPercentage}%` : t("projects.notApplicable")}</TableCell>
            <TableCell><Badge status={person.status}>{person.status}</Badge></TableCell>
            {showActionColumn ? <TableCell><div className="flex justify-end gap-1">
              {canViewResourceContacts && hasContactCard(person) ? <Button type="button" size="sm" variant="ghost" className="h-8 w-8 p-0" onClick={() => setContactPerson(person)} aria-label={t("projects.viewContactCard")} title={t("projects.viewContactCard")}><ContactRound className="h-4 w-4" aria-hidden="true" /></Button> : null}
              {canManageStakeholders && canInvitePerson(person) ? <Button size="sm" variant="ghost" onClick={() => setInviteSelected(person)} aria-label={t("projects.inviteStakeholder")}><Mail className="h-4 w-4" /></Button> : null}
              {canManagePerson(person, canManageProjectTeam, canManageStakeholders, canViewTeams) && canRemovePerson(person) ? <Button size="sm" variant="ghost" onClick={() => void remove(person)} aria-label={t("projects.removeProjectPerson")}><UserMinus className="h-4 w-4 text-destructive" /></Button> : null}
              {canManagePerson(person, canManageProjectTeam, canManageStakeholders, canViewTeams) && canEditPerson(person) ? <Button size="sm" variant="ghost" onClick={() => openForm(person)} aria-label={t("projects.editProjectPerson")}><Pencil className="h-4 w-4" /></Button> : null}
            </div></TableCell> : null}
          </TableRow>)}
        </TableBody>
      </Table>}
    </CardContent>
    <SideSheet open={open} onOpenChange={(next) => { if (next) setOpen(true); else closeForm(); }} title={editing ? t("projects.editProjectPerson") : t("projects.addProjectPerson")} description={t("projects.projectPeopleDescription")}>
      <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-3">
          {form.relationshipType === "TeamMember" && !editing ? <div>
            <Label>{t("projects.selectProjectTeamTarget")}</Label>
            <select className="input-field" value={form.targetType} onChange={(event) => setForm({ ...form, targetType: event.target.value as "resource" | "team", resourceId: "", teamId: "" })}>
              <option value="resource">{t("projects.projectTeamTargetResource")}</option>
              <option value="team" disabled={!canViewTeams}>{t("projects.projectTeamTargetTeam")}</option>
            </select>
          </div> : null}
          <div>
            <Label>{t(form.targetType === "team" ? "projects.selectExistingTeam" : "projects.selectExistingResource")} *</Label>
            {editing
              ? <Input value={people.find((person) => person.id === editing.id)?.name ?? editing.name} disabled />
              : form.targetType === "team"
                ? <SearchableSelect options={teamOptions} value={form.teamId} onValueChange={(value) => setForm({ ...form, teamId: value })} onSearchChange={(query) => { if (query || !form.teamId) debouncedTeamSearch(query); }} placeholder={t("projects.selectExistingTeam")} searchPlaceholder={t("projects.searchTeams")} loading={teamsQuery.isLoading} emptyText={t("common.noResults")} />
                : <SearchableSelect options={resourceOptions} value={form.resourceId} onValueChange={(value) => setForm({ ...form, resourceId: value })} onSearchChange={(query) => { if (query || !form.resourceId) debouncedResourceSearch(query); }} placeholder={t("projects.searchResources")} searchPlaceholder={t("projects.searchResources")} loading={resourcesQuery.isLoading} emptyText={t("common.noResults")} />}
          </div>
          {!editing && form.targetType === "resource" && selectedResource ? <section className="space-y-1 rounded-md border bg-muted/20 p-3"><p className="text-xs font-semibold text-muted-foreground">{t("projects.selectedPerson")}</p><p className="font-medium">{selectedResource.name}</p><p className="text-xs text-muted-foreground">{selectedResource.email || "—"}</p><p className="text-xs text-muted-foreground">{[selectedResource.type, selectedResource.jobTitle, selectedResource.userId ? t("projects.systemUser") : null].filter(Boolean).join(" · ")}</p>{hasHistoricalForSelectedRelationship ? <p className="pt-1 text-xs text-muted-foreground">{t("projects.previouslyInProjectDescription")}</p> : null}</section> : null}
          {!editing && form.targetType === "team" && selectedTeam ? <section className="space-y-1 rounded-md border bg-muted/20 p-3"><p className="text-xs font-semibold text-muted-foreground">{t("projects.projectTeamTargetTeam")}</p><p className="font-medium">{selectedTeam.name}</p><p className="text-xs text-muted-foreground">{t("projects.teamMembershipEffectiveCapacityHint")}</p></section> : null}
          {activeDuplicate ? <p className="rounded-md border border-destructive/30 bg-destructive/5 p-2 text-sm text-destructive" role="alert">{t("projects.alreadyInProject")}</p> : null}
          {sponsorConflict ? <p className="rounded-md border border-destructive/30 bg-destructive/5 p-2 text-sm text-destructive" role="alert">{t("projects.sponsorAlreadyAssignedDescription")}</p> : null}
          <div>
            <Label>{t("projects.projectRelationship")}</Label>
            <select className="input-field" value={form.relationshipType} onChange={(event) => setForm({ ...form, relationshipType: event.target.value, targetType: "resource", teamId: "" })} disabled={Boolean(editing)}>
              {canManageProjectTeam ? <option value="TeamMember">{t("projects.teamMember")}</option> : null}
              {canManageStakeholders && canViewResourceContacts ? <>
                <option value="Stakeholder">{t("projects.stakeholder")}</option>
                <option value="Sponsor">{t("projects.sponsor")}</option>
                <option value="Other">{t("projects.otherRelationship")}</option>
              </> : null}
            </select>
          </div>
          <div><Label>{t("projects.role")}</Label><Input value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })} /></div>
          {form.relationshipType === "TeamMember" ? <>
            <div><Label>{t("projects.allocationPercentage")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label><Input type="number" min="0" max="100" step="0.1" value={form.allocationPercentage} onChange={(event) => setForm({ ...form, allocationPercentage: event.target.value })} required aria-required="true" /></div>
            {form.targetType === "resource" ? <section className="grid grid-cols-2 gap-3 rounded-md border bg-muted/20 p-3" aria-live="polite">
              <div><p className="text-xs text-muted-foreground">{t("projects.resourceCapacityPerWeek")}</p><p className="mt-1 text-sm font-semibold tabular-nums">{resourceCapacityQuery.isLoading ? "—" : resourceCapacityQuery.data ? `${formatNumber(resourceCapacityQuery.data.weeklyCapacityHours)} ${t("common.hours")}` : "—"}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("projects.projectCapacityPerWeek")}</p><p className="mt-1 text-sm font-semibold tabular-nums">{resourceCapacityQuery.data && Number.isFinite(proposedAllocation) ? `${formatNumber(resourceCapacityQuery.data.weeklyCapacityHours * proposedAllocation / 100)} ${t("common.hours")}` : "—"}</p></div>
              {resourceCapacityQuery.isError ? <p className="col-span-2 text-xs text-destructive">{t("projects.resourceCapacityLoadError")}</p> : null}
            </section> : <p className="text-xs text-muted-foreground">{t("projects.teamMembershipEffectiveCapacityHint")}</p>}
            <div>
              <Label>{t("projects.functionalRoles")}</Label>
              <div className="mt-1 flex flex-wrap gap-1.5">
                {(rolesQuery.data ?? []).map((role) => {
                  const active = form.projectRoleIds.includes(role.id);
                  return <button key={role.id} type="button" aria-pressed={active} onClick={() => setForm({ ...form, projectRoleIds: active ? form.projectRoleIds.filter((id) => id !== role.id) : [...form.projectRoleIds, role.id] })} className={`rounded-full border px-2.5 py-1 text-xs ${active ? "border-primary bg-primary/10 text-primary" : "border-input text-muted-foreground"}`}>{role.nameEn}</button>;
                })}
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div><Label>{t("projects.startDate")}</Label><Input type="date" value={form.startDate} onChange={(event) => setForm({ ...form, startDate: event.target.value })} /></div>
              <div><Label>{t("projects.endDate")}</Label><Input type="date" value={form.endDate} onChange={(event) => setForm({ ...form, endDate: event.target.value })} /></div>
            </div>
            <div className="flex items-center gap-2"><Checkbox checked={form.isBillable} onCheckedChange={() => setForm({ ...form, isBillable: !form.isBillable })} id="person-billable" /><Label htmlFor="person-billable">{t("projects.isBillable")}</Label></div>
          </> : <p className="text-xs text-muted-foreground">{t(`projects.${form.relationshipType === "Stakeholder" ? "relationshipStakeholderDescription" : form.relationshipType === "Sponsor" ? "relationshipSponsorDescription" : "relationshipOtherDescription"}`)}</p>}
          {(form.relationshipType === "Stakeholder" || form.relationshipType === "Other") ? <div className="grid grid-cols-2 gap-2"><div><Label>{t("projects.stakeholderInterest")}</Label><Input value={form.interest} onChange={(event) => setForm({ ...form, interest: event.target.value })} /></div><div><Label>{t("projects.stakeholderInfluence")}</Label><Input value={form.influence} onChange={(event) => setForm({ ...form, influence: event.target.value })} /></div></div> : null}
          {conflictCheckEnabled && conflictsQuery.isFetching ? <p className="text-xs text-muted-foreground">{t("projects.checkingAllocationConflicts")}</p> : null}
          {allocationConflicts.length > 0 ? <div className="space-y-2 rounded-md border border-warning/40 bg-warning/10 p-3" role="alert">
            <p className="text-sm font-medium text-warning-foreground">{t("projects.conflictsDetected")}</p>
            <div className="space-y-1">{allocationConflicts.map((conflict) => <div key={conflict.allocationId} className="flex flex-wrap items-center justify-between gap-2 text-xs">{conflict.isRestrictedConflict ? <span>{t("projects.conflictInAnotherProject")}</span> : <><span>{conflict.resourceName}{conflict.role ? ` · ${conflict.role}` : ""}</span><span className="text-muted-foreground">{formatNumber(conflict.allocationPercentage)}% · {conflict.startDate.slice(0, 10)} – {conflict.endDate.slice(0, 10)}</span></>}</div>)}</div>
          </div> : null}
          {error ? <p className="text-sm text-destructive" role="alert">{error}</p> : null}
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={closeForm} disabled={addPerson.isPending || updatePerson.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={addPerson.isPending || updatePerson.isPending || !canManage || effectiveReadOnly || (!editing && (form.targetType === "team" ? !form.teamId : !form.resourceId)) || activeDuplicate || sponsorConflict}>{t(`projects.${submitAction}`)}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  </Card>
  {contactPerson ? <ProjectTeamContactCard person={contactPerson} open onOpenChange={(next) => { if (!next) setContactPerson(null); }} /> : null}
  <Dialog open={Boolean(removeCandidate)} onOpenChange={(next) => { if (!next && !removePerson.isPending) { setRemoveCandidate(null); setRemoveError(null); } }}>
    <DialogContent role="dialog" aria-modal="true" aria-label={t("projects.removeProjectPerson")}>
      <DialogHeader>
        <DialogTitle>{t("projects.removeProjectPerson")}</DialogTitle>
        <DialogDescription>{t("projects.removeProjectPersonDescription", { name: removeCandidate?.name ?? "" })}</DialogDescription>
      </DialogHeader>
      <div className="py-4">
        <p className="rounded-lg bg-muted/30 p-3 text-sm text-muted-foreground">{t("projects.removeProjectPersonHistoryHint")}</p>
        {removeError ? <p className="mt-3 whitespace-pre-line text-sm text-destructive" role="alert">{removeError}</p> : null}
      </div>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={() => setRemoveCandidate(null)} disabled={removePerson.isPending}>{t("common.cancel")}</Button>
        <Button type="button" variant="destructive" onClick={() => void confirmRemove()} disabled={removePerson.isPending || !removeCandidate || !canManagePerson(removeCandidate, canManageProjectTeam, canManageStakeholders, canViewTeams) || effectiveReadOnly}>{removePerson.isPending ? t("common.saving") : t("projects.removeProjectPerson")}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
  <SideSheet open={Boolean(inviteSelected)} onOpenChange={(next) => { if (!next) closeInviteSheet(); }} title={t("projects.inviteStakeholder")} description={t("projects.inviteStakeholderDescription")} size="md">
    <SideSheetBody className="space-y-4">
      <div className="grid gap-2 rounded-lg border bg-muted/20 p-3 text-sm">
        <div><p className="text-xs text-muted-foreground">{t("projects.name")}</p><p className="font-medium">{inviteSelected?.name}</p></div>
        <div><p className="text-xs text-muted-foreground">{t("projects.email")}</p><p className="font-medium">{inviteSelected?.email}</p></div>
        <div className="grid gap-3 sm:grid-cols-2"><div><p className="text-xs text-muted-foreground">{t("projects.resourceType")}</p><p>{inviteSelected?.resourceType}</p></div><div><p className="text-xs text-muted-foreground">{t("projects.projectRelationship")}</p><p>{t("projects.stakeholder")}</p></div></div>
      </div>
      <p className="text-xs text-muted-foreground">{t("projects.inviteCanonicalResourceHint")}</p>
      {invitation.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : invitation.data?.status === "Pending" ? <section className="space-y-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm"><p className="font-medium">{t("projects.invitationPending")}</p><p className="text-xs">{formatInvitationExpiry(invitation.data.expiresAt, locale)}</p><p className="text-xs text-muted-foreground">{t("projects.invitationEmailDeliveryStatus", { status: invitationEmailStatusLabel(invitation.data.emailDeliveryStatus, t) })}</p>{invitation.data.invitationUrl ? <div className="break-all rounded bg-background p-2 text-xs">{invitation.data.invitationUrl}</div> : null}</section> : invitation.data ? <div className="space-y-1 text-sm text-muted-foreground"><p>{t("projects.invitationStatus", { status: invitation.data.status })}</p>{invitation.data.status === "Accepted" ? <p>{t("projects.invitationConsents", { reports: invitation.data.reportsConsent ? t("common.yes") : t("common.no"), notifications: invitation.data.notificationsConsent ? t("common.yes") : t("common.no") })}</p> : null}<p>{t("projects.invitationEmailDeliveryStatus", { status: invitationEmailStatusLabel(invitation.data.emailDeliveryStatus, t) })}</p></div> : null}
      {invitationMutationErrorMessage ? <p className="text-sm text-destructive" role="alert">{invitationMutationErrorMessage}</p> : null}
    </SideSheetBody>
    <SideSheetFooter>
      {invitation.data?.status === "Pending" ? <Button type="button" variant="outline" onClick={() => { void confirm({ description: t("projects.revokeInvitationConfirm"), destructive: true }).then((accepted) => { if (accepted) return revokeInvitation.mutateAsync(); }); }} disabled={revokeInvitation.isPending}>{revokeInvitation.isPending ? t("common.saving") : t("projects.revokeInvitation")}</Button> : null}
      <Button type="button" onClick={() => void sendInvitation.mutateAsync()} disabled={sendInvitation.isPending || !inviteSelected}>{sendInvitation.isPending ? t("common.saving") : invitation.data?.status === "Pending" ? t("projects.resendInvitation") : t("projects.inviteStakeholder")}</Button>
    </SideSheetFooter>
  </SideSheet>
  </>;
}
