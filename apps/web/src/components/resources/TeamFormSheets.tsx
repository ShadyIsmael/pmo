"use client";

/**
 * Team create/edit side-sheet.
 *
 * The Team code is system-assigned (TEAM-000001) and is never sent by the client: it is
 * shown read-only on the form and revealed on the confirmation panel after create.
 */

import * as React from "react";
import type { FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { useAuth } from "@/lib/auth";
import { hasAnyPermission, Permissions } from "@/lib/permissions";
import { useResources } from "@/hooks/resources/useResources";
import {
  useCreateTeam,
  useDepartments,
  useTeam,
  useUpdateTeam,
  type TeamDto,
} from "@/hooks/resources/useTeams";

type TeamForm = {
  name: string;
  description: string;
  departmentId: string;
  teamLeadId: string;
  emailAlias: string;
};

const emptyForm: TeamForm = {
  name: "",
  description: "",
  departmentId: "",
  teamLeadId: "",
  emailAlias: "",
};

function formFrom(team: TeamDto): TeamForm {
  return {
    name: team.name ?? "",
    description: team.description ?? "",
    departmentId: team.departmentId ?? "",
    teamLeadId: team.teamLeadId ?? "",
    emailAlias: team.emailAlias ?? "",
  };
}

export function AddTeamButton() {
  const t = useTranslations("resources.teams");
  const { user, status: authStatus } = useAuth();
  const canCreate = hasAnyPermission(user?.permissions ?? [], [Permissions.Team.Create, Permissions.Team.Manage]);

  const [open, setOpen] = React.useState(false);
  const [form, setForm] = React.useState<TeamForm>(emptyForm);
  const [nameError, setNameError] = React.useState(false);
  const [createdTeam, setCreatedTeam] = React.useState<TeamDto | null>(null);

  const create = useCreateTeam();
  const departmentsQuery = useDepartments();
  const leadResourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });

  const departments = React.useMemo(() => departmentsQuery.data ?? [], [departmentsQuery.data]);
  const leadOptions = React.useMemo(() => leadResourcesQuery.data?.items ?? [], [leadResourcesQuery.data]);

  if (authStatus === "loading") {
    return (
      <Button size="sm" disabled>
        <Plus className="me-2 h-4 w-4" aria-hidden="true" />
        {t("addTeam")}
      </Button>
    );
  }
  if (!canCreate) return null;

  const setField = <K extends keyof TeamForm>(field: K, value: TeamForm[K]) =>
    setForm((current) => ({ ...current, [field]: value }));

  const close = () => {
    setOpen(false);
    setForm(emptyForm);
    setNameError(false);
    setCreatedTeam(null);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const name = form.name.trim();
    if (!name) {
      setNameError(true);
      return;
    }
    setNameError(false);

    try {
      const created = await create.mutateAsync({
        name,
        description: form.description.trim() || undefined,
        departmentId: form.departmentId || undefined,
        teamLeadId: form.teamLeadId || undefined,
        emailAlias: form.emailAlias.trim() || undefined,
        memberResourceIds: [],
      });
      // Keep the sheet open so the system-assigned team code is shown to the user.
      setCreatedTeam(created ?? null);
    } catch {
      /* surfaced via create.error below */
    }
  };

  return (
    <>
      <Button size="sm" onClick={() => setOpen(true)} type="button">
        <Plus className="me-2 h-4 w-4" aria-hidden="true" />
        {t("addTeam")}
      </Button>
      <SideSheet
        open={open}
        onOpenChange={(next) => {
          if (!next) close();
          else setOpen(next);
        }}
        title={createdTeam ? t("createdTitle") : t("createTitle")}
        description={createdTeam ? createdTeam.name : t("formDescription")}
        size="lg"
        closeLabel={createdTeam ? t("close") : t("cancel")}
      >
        {createdTeam ? (
          <>
            <SideSheetBody className="space-y-4">
              <div className="rounded-md border border-border bg-muted/40 p-4">
                <p className="text-sm text-muted-foreground">{t("createdCodeLabel")}</p>
                <p className="mt-1 font-mono text-2xl font-semibold tracking-wide text-foreground">
                  {createdTeam.code ?? "—"}
                </p>
              </div>
              <p className="text-sm text-muted-foreground">{t("createdHint")}</p>
            </SideSheetBody>
            <SideSheetFooter>
              <Button type="button" onClick={close}>
                {t("done")}
              </Button>
            </SideSheetFooter>
          </>
        ) : (
          <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void submit(event)}>
            <SideSheetBody className="space-y-6">
              <section aria-labelledby="team-basic-heading" className="space-y-3">
                <h3 id="team-basic-heading" className="text-sm font-semibold text-foreground">
                  {t("basicInformation")}
                </h3>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-1.5 sm:col-span-2">
                    <Label htmlFor="team-name">{t("name")}</Label>
                    <Input
                      id="team-name"
                      required
                      value={form.name}
                      onChange={(event) => setField("name", event.target.value)}
                    />
                    {nameError ? <p className="text-xs text-destructive">{t("nameRequired")}</p> : null}
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="team-code">{t("code")}</Label>
                    <Input
                      id="team-code"
                      value=""
                      readOnly
                      disabled
                      placeholder={t("autoGenerated")}
                      aria-describedby="team-code-hint"
                    />
                    <p id="team-code-hint" className="text-xs text-muted-foreground">
                      {t("autoGeneratedHint")}
                    </p>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="team-email-alias">{t("emailAlias")}</Label>
                    <Input
                      id="team-email-alias"
                      value={form.emailAlias}
                      onChange={(event) => setField("emailAlias", event.target.value)}
                      placeholder={t("emailAliasPlaceholder")}
                    />
                  </div>
                  <div className="space-y-1.5 sm:col-span-2">
                    <Label htmlFor="team-description">{t("description")}</Label>
                    <Input
                      id="team-description"
                      value={form.description}
                      onChange={(event) => setField("description", event.target.value)}
                    />
                  </div>
                </div>
              </section>

              <section aria-labelledby="team-org-heading" className="space-y-3">
                <h3 id="team-org-heading" className="text-sm font-semibold text-foreground">
                  {t("organisationalLinks")}
                </h3>
                <p className="text-xs text-muted-foreground">{t("organisationalLinksHint")}</p>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="team-department">{t("department")}</Label>
                    <select
                      id="team-department"
                      className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                      value={form.departmentId}
                      onChange={(event) => setField("departmentId", event.target.value)}
                    >
                      <option value="">{t("noDepartment")}</option>
                      {departments.map((department) => (
                        <option key={department.id} value={department.id}>
                          {department.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="team-lead">{t("teamLead")}</Label>
                    <select
                      id="team-lead"
                      className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                      value={form.teamLeadId}
                      onChange={(event) => setField("teamLeadId", event.target.value)}
                    >
                      <option value="">{t("noTeamLead")}</option>
                      {leadOptions.map((resource) => (
                        <option key={resource.id} value={resource.id}>
                          {resource.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              </section>

              {create.error ? (
                <p role="alert" className="text-sm text-destructive">
                  {create.error instanceof Error ? create.error.message : t("mutationError")}
                </p>
              ) : null}
            </SideSheetBody>
            <SideSheetFooter>
              <Button type="button" variant="outline" onClick={close} disabled={create.isPending}>
                {t("cancel")}
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending ? t("saving") : t("create")}
              </Button>
            </SideSheetFooter>
          </form>
        )}
      </SideSheet>
    </>
  );
}

export function EditTeamButton({
  team,
  open: controlledOpen,
  onOpenChange,
  hideTrigger = false,
}: {
  team: TeamDto;
  open?: boolean;
  // eslint-disable-next-line no-unused-vars
  onOpenChange?: (next: boolean) => void;
  hideTrigger?: boolean;
}) {
  const t = useTranslations("resources.teams");
  const { user, status: authStatus } = useAuth();
  const canEdit = hasAnyPermission(user?.permissions ?? [], [Permissions.Team.Edit, Permissions.Team.Manage]);

  const [uncontrolledOpen, setUncontrolledOpen] = React.useState(false);
  const open = controlledOpen ?? uncontrolledOpen;
  const setOpen = onOpenChange ?? setUncontrolledOpen;

  const [form, setForm] = React.useState<TeamForm>(() => formFrom(team));
  const [nameError, setNameError] = React.useState(false);

  const teamQuery = useTeam(open ? team.id : null);
  const departmentsQuery = useDepartments();
  const leadResourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const update = useUpdateTeam();

  const departments = React.useMemo(() => departmentsQuery.data ?? [], [departmentsQuery.data]);
  const leadOptions = React.useMemo(() => leadResourcesQuery.data?.items ?? [], [leadResourcesQuery.data]);

  React.useEffect(() => {
    if (!open) return;
    setForm(formFrom(teamQuery.data ?? team));
    setNameError(false);
  }, [open, teamQuery.data, team]);

  if (authStatus === "loading") {
    return (
      <Button size="sm" variant="ghost" disabled>
        {t("edit")}
      </Button>
    );
  }
  if (!canEdit) return null;

  const setField = <K extends keyof TeamForm>(field: K, value: TeamForm[K]) =>
    setForm((current) => ({ ...current, [field]: value }));

  const close = () => {
    setOpen(false);
    setNameError(false);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const name = form.name.trim();
    if (!name) {
      setNameError(true);
      return;
    }
    setNameError(false);

    try {
      await update.mutateAsync({
        id: team.id,
        input: {
          name,
          description: form.description.trim(),
          departmentId: form.departmentId,
          teamLeadId: form.teamLeadId,
          emailAlias: form.emailAlias.trim(),
        },
      });
      close();
    } catch {
      /* surfaced via update.error below */
    }
  };

  return (
    <>
      {hideTrigger ? null : (
        <Button size="sm" variant="outline" onClick={() => setOpen(true)} type="button">
          {t("edit")}
        </Button>
      )}
      <SideSheet
        open={open}
        onOpenChange={(next) => {
          if (!next) close();
          else setOpen(next);
        }}
        title={t("editTitle")}
        description={t("editDescription")}
        size="lg"
        closeLabel={t("cancel")}
      >
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void submit(event)}>
          <SideSheetBody className="space-y-6">
            <section aria-labelledby="team-edit-basic-heading" className="space-y-3">
              <h3 id="team-edit-basic-heading" className="text-sm font-semibold text-foreground">
                {t("basicInformation")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5 sm:col-span-2">
                  <Label htmlFor="edit-team-name">{t("name")}</Label>
                  <Input
                    id="edit-team-name"
                    required
                    value={form.name}
                    onChange={(event) => setField("name", event.target.value)}
                  />
                  {nameError ? <p className="text-xs text-destructive">{t("nameRequired")}</p> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-team-code">{t("code")}</Label>
                  <Input
                    id="edit-team-code"
                    value={team.code ?? ""}
                    readOnly
                    disabled
                    aria-describedby="edit-team-code-hint"
                  />
                  <p id="edit-team-code-hint" className="text-xs text-muted-foreground">
                    {t("codeReadOnlyHint")}
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-team-email-alias">{t("emailAlias")}</Label>
                  <Input
                    id="edit-team-email-alias"
                    value={form.emailAlias}
                    onChange={(event) => setField("emailAlias", event.target.value)}
                  />
                </div>
                <div className="space-y-1.5 sm:col-span-2">
                  <Label htmlFor="edit-team-description">{t("description")}</Label>
                  <Input
                    id="edit-team-description"
                    value={form.description}
                    onChange={(event) => setField("description", event.target.value)}
                  />
                </div>
              </div>
            </section>

            <section aria-labelledby="team-edit-org-heading" className="space-y-3">
              <h3 id="team-edit-org-heading" className="text-sm font-semibold text-foreground">
                {t("organisationalLinks")}
              </h3>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="edit-team-department">{t("department")}</Label>
                  <select
                    id="edit-team-department"
                    className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={form.departmentId}
                    onChange={(event) => setField("departmentId", event.target.value)}
                  >
                    <option value="">{t("noDepartment")}</option>
                    {departments.map((department) => (
                      <option key={department.id} value={department.id}>
                        {department.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="edit-team-lead">{t("teamLead")}</Label>
                  <select
                    id="edit-team-lead"
                    className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                    value={form.teamLeadId}
                    onChange={(event) => setField("teamLeadId", event.target.value)}
                  >
                    <option value="">{t("noTeamLead")}</option>
                    {leadOptions.map((resource) => (
                      <option key={resource.id} value={resource.id}>
                        {resource.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </section>

            {update.error ? (
              <p role="alert" className="text-sm text-destructive">
                {update.error instanceof Error ? update.error.message : t("mutationError")}
              </p>
            ) : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={close} disabled={update.isPending}>
              {t("cancel")}
            </Button>
            <Button type="submit" disabled={update.isPending}>
              {update.isPending ? t("saving") : t("save")}
            </Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </>
  );
}
