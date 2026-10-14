import * as React from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import "@xyflow/react/dist/style.css";
import { Check, ChevronRight, Eye, GitBranch, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useFullWorkflowDefinition, useFullWorkflowDefinitions, useFullWorkflowMetadata, useFullWorkflowMutations, useFullWorkflowPreview, useFullWorkflowVersion, type SaveWorkflowState, type SaveWorkflowTransition, type WorkflowDomain, type WorkflowState, type WorkflowTransition } from "./api/workflowsFull";
import { WorkflowFlowDesignerFull } from "./WorkflowFlowDesignerFull";
import { WorkflowRuleSheet } from "./WorkflowRuleSheet";

function Loading({ label }: { label: string }) { return <p className="py-12 text-center text-sm text-muted-foreground">{label}</p>; }
function ErrorText({ label }: { label: string }) { return <p className="py-12 text-center text-sm text-destructive">{label}</p>; }
const workflowCompactTableClass = "text-xs [&_th]:h-10 [&_th]:px-3 [&_th]:py-2.5 [&_th]:text-center [&_td]:px-3 [&_td]:py-2.5";
function canManage(user: ReturnType<typeof useAuth>["user"], readOnly: boolean) { return Boolean(user && hasPermission(user.permissions, Permissions.Workflow.Manage) && !readOnly); }
function canPublish(user: ReturnType<typeof useAuth>["user"], readOnly: boolean) { return Boolean(user && hasPermission(user.permissions, Permissions.Workflow.Publish) && !readOnly); }
function workflowStatusLabel(status: string, t: (key: string) => string) {
  return status === "Published" ? t("workflows.published") : status === "Retired" ? t("workflows.retired") : t("workflows.draft");
}

export function WorkflowsPageFull() {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagement();
  const navigate = useNavigate();
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState<"all" | "active" | "inactive">("all");
  const [domain, setDomain] = React.useState<WorkflowDomain | "all">("all");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useFullWorkflowDefinitions({ search: search || undefined, isActive: status === "all" ? undefined : status === "active", domain: domain === "all" ? undefined : domain });
  const mutations = useFullWorkflowMutations();
  const editable = canManage(user, effectiveReadOnly || !canMutate);
  const [form, setForm] = React.useState({ name: "", code: "", description: "", domain: "Task" as WorkflowDomain, entityType: "" });
  async function create(event: React.FormEvent) { event.preventDefault(); await mutations.create.mutateAsync({ ...form, entityType: form.entityType.trim() || undefined }); setCreateOpen(false); setForm({ name: "", code: "", description: "", domain: "Task", entityType: "" }); }
  return (
    <div className="space-y-6">
      <PageHeader title={t("workflows.title")} description={t("workflows.description")} actions={editable ? <Button onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("workflows.create")}</Button> : effectiveReadOnly ? <Badge status="ReadOnly">{t("workflows.readOnly")}</Badge> : null} />
      <div className="flex flex-wrap gap-2">
        <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("workflows.searchPlaceholder")} className="max-w-sm" />
        {(["all", "active", "inactive"] as const).map((item) => <Button key={item} size="sm" variant={status === item ? "default" : "outline"} onClick={() => setStatus(item)}>{item === "all" ? t("common.all") : t(`workflows.${item}`)}</Button>)}
        {(["all", "Task", "Risk", "Finance"] as const).map((item) => <Button key={item} size="sm" variant={domain === item ? "default" : "outline"} onClick={() => setDomain(item)}>{item === "all" ? t("common.all") : item}</Button>)}
      </div>
      <Card>
        <CardContent className="p-0">
          {query.isLoading ? <Loading label={t("common.loading")} /> : query.isError ? <ErrorText label={t("common.errorLoading")} /> : query.data?.length ? (
            <Table className={workflowCompactTableClass}>
              <TableHeader><TableRow><TableHead>{t("workflows.name")}</TableHead><TableHead>{t("workflows.code")}</TableHead><TableHead>{t("workflows.domain")}</TableHead><TableHead>{t("workflows.entityType")}</TableHead><TableHead>{t("workflows.version")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("workflows.updated")}</TableHead><TableHead>{t("common.actions")}</TableHead></TableRow></TableHeader>
              <TableBody>{query.data.map((item) => <TableRow key={item.id}>
                <TableCell><Link to={`/workflows/${encodeURIComponent(item.id)}`} className="font-semibold hover:underline">{item.name}</Link></TableCell>
                <TableCell className="font-mono text-xs">{item.code}</TableCell>
                <TableCell>{item.domain}</TableCell>
                <TableCell>{item.entityType}</TableCell>
                <TableCell>{item.activePublishedVersionNumber ? `v${item.activePublishedVersionNumber}` : "—"}</TableCell>
                <TableCell><Badge status={item.activePublishedLifecycleStatus ?? (item.isActive ? "Active" : "Inactive")}>{item.activePublishedLifecycleStatus ?? (item.isActive ? t("workflows.active") : t("workflows.inactive"))}</Badge></TableCell>
                <TableCell>{item.updatedAt && Number.isFinite(Date.parse(item.updatedAt)) ? formatDate(item.updatedAt, { numberingSystem: "latn" }) : "—"}</TableCell>
                <TableCell><Button size="sm" variant="ghost" onClick={() => navigate(`/workflows/${encodeURIComponent(item.id)}`)}><Eye className="me-1 h-4 w-4" />{t("workflows.open")}</Button></TableCell>
              </TableRow>)}</TableBody>
            </Table>
          ) : <p className="p-10 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}
        </CardContent>
      </Card>
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("workflows.createTitle")}>
        <form onSubmit={(event) => void create(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-4">
            <div className="space-y-1.5"><Label>{t("workflows.name")}</Label><Input required value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></div>
            <div className="space-y-1.5"><Label>{t("workflows.code")}</Label><Input required value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value.toUpperCase() })} /></div>
            <div className="space-y-1.5"><Label>{t("workflows.domain")}</Label><select className="input-field" value={form.domain} onChange={(event) => setForm({ ...form, domain: event.target.value as WorkflowDomain })}><option value="Task">Task</option><option value="Risk">Risk</option><option value="Finance">Finance</option></select></div>
            <div className="space-y-1.5"><Label>{t("workflows.entityType")}</Label><Input value={form.entityType} onChange={(event) => setForm({ ...form, entityType: event.target.value })} /></div>
            <div className="space-y-1.5"><Label>{t("workflows.description")}</Label><Textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={mutations.create.isPending}>{t("common.create")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}

export function WorkflowDefinitionPageFull() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagement();
  const query = useFullWorkflowDefinition(id);
  const mutations = useFullWorkflowMutations();
  const editable = canManage(user, effectiveReadOnly || !canMutate) && Boolean(query.data?.activePublishedVersionId);
  if (query.isLoading) return <Loading label={t("common.loading")} />;
  if (query.isError || !query.data) return <ErrorText label={t("common.errorLoading")} />;
  const definition = query.data;
  async function createDraft() { const created = await mutations.createDraft.mutateAsync({ id: definition.id, body: { changeLog: "Draft created from management UI" } }); navigate(`/workflows/${encodeURIComponent(definition.id)}/versions/${encodeURIComponent(created.id)}`); }
  return (
    <div className="space-y-6">
      <PageHeader title={definition.name} description={`${definition.code} · ${definition.entityType}`} actions={editable ? <Button onClick={() => void createDraft()} disabled={mutations.createDraft.isPending}><GitBranch className="me-1.5 h-4 w-4" />{t("workflows.newDraft")}</Button> : effectiveReadOnly ? <Badge status="ReadOnly">{t("workflows.readOnly")}</Badge> : null} />
      <Card>
        <CardHeader><CardTitle>{t("workflows.workflowDetails")}</CardTitle><CardDescription>{definition.description || t("workflows.description")}</CardDescription></CardHeader>
        <CardContent className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Info label={t("workflows.name")} value={definition.name} />
          <Info label={t("workflows.code")} value={definition.code} />
          <Info label={t("workflows.domain")} value={definition.domain} />
          <Info label={t("workflows.entityType")} value={definition.entityType} />
          <Info label={t("common.status")} value={definition.isActive ? t("workflows.active") : t("workflows.inactive")} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>{t("workflows.versions")}</CardTitle>
          <CardDescription>{definition.activePublishedVersionId ? t("workflows.publishHint") : t("workflows.noVersions")}</CardDescription>
        </CardHeader>
        <CardContent>{definition.versions.length ? (
          <Table className={workflowCompactTableClass}>
            <TableHeader><TableRow><TableHead>{t("workflows.version")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("workflows.states")}</TableHead><TableHead>{t("workflows.transitions")}</TableHead><TableHead>{t("workflows.updated")}</TableHead><TableHead>{t("common.actions")}</TableHead></TableRow></TableHeader>
            <TableBody>{definition.versions.map((version) => {
              const updatedAt = version.publishedAt ?? version.createdAt;
              return <TableRow key={version.id}>
                <TableCell>v{version.versionNumber}</TableCell>
                <TableCell><Badge status={version.lifecycleStatus}>{workflowStatusLabel(version.lifecycleStatus, t)}</Badge></TableCell>
                <TableCell>{version.stateCount}</TableCell>
                <TableCell>{version.transitionCount}</TableCell>
                <TableCell>{updatedAt && Number.isFinite(Date.parse(updatedAt)) ? formatDate(updatedAt, { numberingSystem: "latn" }) : "—"}</TableCell>
                <TableCell><Link to={`/workflows/${encodeURIComponent(definition.id)}/versions/${encodeURIComponent(version.id)}`}><Button size="sm" variant="ghost">{t("workflows.open")}</Button></Link></TableCell>
              </TableRow>;
            })}</TableBody>
          </Table>
        ) : <p className="text-sm text-muted-foreground">{t("workflows.noVersions")}</p>}</CardContent>
      </Card>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) { return <div><p className="text-xs uppercase text-muted-foreground">{label}</p><p className="mt-1 font-semibold">{value}</p></div>; }

export function WorkflowVersionPageFull() {
  const { id, versionId } = useParams<{ id: string; versionId: string }>();
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagement();
  const version = useFullWorkflowVersion(versionId);
  const definition = useFullWorkflowDefinition(id);
  const metadata = useFullWorkflowMetadata(Boolean(version.data));
  const preview = useFullWorkflowPreview(versionId);
  const mutations = useFullWorkflowMutations();
  const editable = canManage(user, effectiveReadOnly || !canMutate) && version.data?.lifecycleStatus === "Draft";
  const publishable = canPublish(user, effectiveReadOnly || !canMutate) && version.data?.lifecycleStatus === "Draft";
  const [tab, setTab] = React.useState<"states" | "transitions" | "preview">("states");
  const [stateOpen, setStateOpen] = React.useState(false);
  const [transitionOpen, setTransitionOpen] = React.useState(false);
  const [ruleOpen, setRuleOpen] = React.useState(false);
  const [selectedState, setSelectedState] = React.useState<WorkflowState | undefined>();
  const [selectedTransition, setSelectedTransition] = React.useState<WorkflowTransition | undefined>();
  const [validation, setValidation] = React.useState<string[]>([]);
  const [publishConfirmOpen, setPublishConfirmOpen] = React.useState(false);
  const [publishError, setPublishError] = React.useState<string | null>(null);
  if (version.isLoading || definition.isLoading) return <Loading label={t("common.loading")} />;
  if (version.isError || !version.data) return <ErrorText label={t("common.errorLoading")} />;
  const current = version.data;
  const states = current.states;
  async function validate() { const result = await mutations.validate.mutateAsync(current.id); setValidation(result.errors); }
  async function publish() {
    setPublishError(null);
    try {
      await mutations.publish.mutateAsync({ id: current.id, rowVersion: current.rowVersion });
      setPublishConfirmOpen(false);
    } catch {
      setPublishError(t("workflows.publishFailed"));
    }
  }
  async function connectStates(fromStateId: string, toStateId: string) { const from = states.find((state) => state.id === fromStateId); const to = states.find((state) => state.id === toStateId); if (!from || !to) return; const normalize = (value: string) => value.replace(/[^a-zA-Z0-9]+/g, "_").replace(/^_|_$/g, "").toUpperCase(); await mutations.createTransition.mutateAsync({ id: current.id, body: { code: `MOVE_${normalize(from.code)}_TO_${normalize(to.code)}`, nameEn: `Move to ${to.nameEn}`, nameAr: `الانتقال إلى ${to.nameAr}`, fromStateId, toStateId, displayOrder: current.transitions.length + 1, isActive: true } }); }
  const designerLabels = { title: t("workflows.designer"), hint: t("workflows.designerHint"), connect: t("workflows.connectStates"), autoLayout: t("workflows.autoLayout"), fitView: t("workflows.fitView"), resetLayout: t("workflows.resetLayout"), connectHint: t("workflows.connectHint"), connectFailed: t("workflows.connectFailed"), initial: t("workflows.initial"), terminal: t("workflows.final"), normalState: t("workflows.normalState") };
  return (
    <div className="space-y-6">
      <PageHeader
        title={`${definition.data?.name ?? t("workflows.title")} · v${current.versionNumber}`}
        description={current.lifecycleStatus === "Draft" ? t("workflows.cloneHint") : workflowStatusLabel(current.lifecycleStatus, t)}
        actions={(
          <div className="flex flex-wrap gap-2">
            {current.lifecycleStatus === "Draft" && editable ? (
              <Button variant="outline" onClick={() => void validate()} disabled={mutations.validate.isPending}>
                <Check className="me-1.5 h-4 w-4" />{t("workflows.validate")}
              </Button>
            ) : null}
            {publishable ? <Button onClick={() => { setPublishError(null); setPublishConfirmOpen(true); }} disabled={mutations.publish.isPending}>{t("workflows.publish")}</Button> : null}
            <Link to={`/workflows/${encodeURIComponent(id ?? "")}`} className="button-outline">{t("workflows.backToDefinition")}</Link>
          </div>
        )}
      />
      <WorkflowFlowDesignerFull
        version={current}
        locale={locale}
        editable={Boolean(editable)}
        labels={designerLabels}
        onSelectState={(stateId) => {
          const selected = states.find((state) => state.id === stateId);
          if (selected) { setSelectedState(selected); setStateOpen(true); }
        }}
        onSelectTransition={(transitionId) => {
          const selected = current.transitions.find((transition) => transition.id === transitionId);
          if (selected) { setSelectedTransition(selected); setTransitionOpen(true); }
        }}
        onConnectStates={connectStates}
      />
      <div className="flex flex-wrap gap-2 border-b pb-2">
        {(["states", "transitions", "preview"] as const).map((item) => (
          <Button key={item} size="sm" variant={tab === item ? "default" : "ghost"} onClick={() => setTab(item)}>{t(`workflows.${item}`)}</Button>
        ))}
      </div>
      {validation.length ? (
        <Card className="border-destructive/30">
          <CardHeader><CardTitle className="text-sm text-destructive">{t("workflows.validationErrors")}</CardTitle></CardHeader>
          <CardContent className="space-y-1 text-sm text-destructive">{validation.map((error) => <p key={error}>{error}</p>)}</CardContent>
        </Card>
      ) : null}
      {tab === "states" ? (
        <StatesPanel
          states={states}
          editable={Boolean(editable)}
          t={t}
          onAdd={() => { setSelectedState(undefined); setStateOpen(true); }}
          onEdit={(state) => { setSelectedState(state); setStateOpen(true); }}
          onDelete={(state) => void mutations.deleteState.mutateAsync({ id: state.id, rowVersion: state.rowVersion })}
        />
      ) : null}
      {tab === "transitions" ? (
        <TransitionsPanel
          transitions={current.transitions}
          editable={Boolean(editable)}
          t={t}
          onAdd={() => { setSelectedTransition(undefined); setTransitionOpen(true); }}
          onEdit={(transition) => { setSelectedTransition(transition); setTransitionOpen(true); }}
          onDelete={(transition) => void mutations.deleteTransition.mutateAsync({ id: transition.id, rowVersion: transition.rowVersion })}
          onRule={(transition) => { setSelectedTransition(transition); setRuleOpen(true); }}
        />
      ) : null}
      {tab === "preview" ? <PreviewPanel preview={preview.data ?? []} locale={locale} t={t} /> : null}
      <StateSheet open={stateOpen} onOpenChange={setStateOpen} state={selectedState} versionId={current.id} editable={Boolean(editable)} t={t} mutations={mutations} />
      <TransitionSheet open={transitionOpen} onOpenChange={setTransitionOpen} transition={selectedTransition} states={states} versionId={current.id} editable={Boolean(editable)} t={t} mutations={mutations} />
      <WorkflowRuleSheet open={ruleOpen} onOpenChange={setRuleOpen} transition={selectedTransition} entityType={definition.data?.entityType} metadata={metadata.data} editable={Boolean(editable)} locale={locale} t={t} mutations={mutations} />
      <Dialog open={publishConfirmOpen} onOpenChange={(open) => { if (!mutations.publish.isPending) setPublishConfirmOpen(open); }} ariaLabel={t("workflows.publishConfirmTitle")}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("workflows.publishConfirmTitle")}</DialogTitle>
            <DialogDescription>{t("workflows.publishConfirmDescription")}</DialogDescription>
          </DialogHeader>
          {publishError ? <p role="alert" className="text-sm text-destructive">{publishError}</p> : null}
          <DialogFooter>
            <Button type="button" variant="outline" disabled={mutations.publish.isPending} onClick={() => setPublishConfirmOpen(false)}>{t("common.cancel")}</Button>
            <Button type="button" disabled={mutations.publish.isPending} onClick={() => void publish()}>{t("workflows.publish")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function StatesPanel({ states, editable, t, onAdd, onEdit, onDelete }: { states: WorkflowState[]; editable: boolean; t: (key: string) => string; onAdd: () => void; onEdit: (state: WorkflowState) => void; onDelete: (state: WorkflowState) => void }) {
  return <Card>
    <CardHeader className="flex flex-row items-center justify-between"><CardTitle>{t("workflows.states")}</CardTitle>{editable ? <Button size="sm" onClick={onAdd}><Plus className="me-1 h-4 w-4" />{t("workflows.addState")}</Button> : null}</CardHeader>
    <CardContent>{states.length ? <Table className={workflowCompactTableClass}>
      <TableHeader><TableRow>
        <TableHead className="text-center">{t("workflows.displayOrder")}</TableHead>
        <TableHead className="text-center">{t("workflows.code")}</TableHead>
        <TableHead className="text-center">{t("workflows.nameEn")}</TableHead>
        <TableHead className="text-center">{t("workflows.nameAr")}</TableHead>
        <TableHead className="text-center">{t("workflows.initial")}</TableHead>
        <TableHead className="text-center">{t("workflows.final")}</TableHead>
        <TableHead className="text-center">{t("common.actions")}</TableHead>
      </TableRow></TableHeader>
      <TableBody>{states.map((state) => <TableRow key={state.id}>
        <TableCell className="tabular-nums">{state.displayOrder}</TableCell>
        <TableCell className="font-mono text-xs">{state.code}</TableCell>
        <TableCell>{state.nameEn}</TableCell>
        <TableCell dir="rtl">{state.nameAr}</TableCell>
        <TableCell>{state.isInitial ? <Check className="h-4 w-4 text-success" /> : "—"}</TableCell>
        <TableCell>{state.isFinal ? <Check className="h-4 w-4 text-success" /> : "—"}</TableCell>
        <TableCell className="text-end">{editable ? <><Button size="sm" variant="ghost" onClick={() => onEdit(state)}>{t("workflows.open")}</Button><Button size="sm" variant="ghost" onClick={() => onDelete(state)}><Trash2 className="h-4 w-4" /></Button></> : <Badge status="ReadOnly">{t("workflows.readOnly")}</Badge>}</TableCell>
      </TableRow>)}</TableBody>
    </Table> : <p className="text-sm text-muted-foreground">{t("workflows.noStates")}</p>}</CardContent>
  </Card>;
}
function TransitionsPanel({ transitions, editable, t, onAdd, onEdit, onDelete, onRule }: { transitions: WorkflowTransition[]; editable: boolean; t: (key: string) => string; onAdd: () => void; onEdit: (transition: WorkflowTransition) => void; onDelete: (transition: WorkflowTransition) => void; onRule: (transition: WorkflowTransition) => void }) {
  return <Card>
    <CardHeader className="flex flex-row items-center justify-between">
      <CardTitle>{t("workflows.transitions")}</CardTitle>
      {editable ? <Button size="sm" onClick={onAdd}><Plus className="me-1 h-4 w-4" />{t("workflows.addTransition")}</Button> : null}
    </CardHeader>
    <CardContent>
      {transitions.length ? <Table className={workflowCompactTableClass}>
        <TableHeader><TableRow>
          <TableHead>{t("workflows.code")}</TableHead>
          <TableHead>{t("workflows.from")}</TableHead>
          <TableHead>{t("workflows.to")}</TableHead>
          <TableHead>{t("workflows.guards")}</TableHead>
          <TableHead>{t("workflows.actionsTab")}</TableHead>
          <TableHead>{t("common.actions")}</TableHead>
        </TableRow></TableHeader>
        <TableBody>{transitions.map((transition) => <TableRow key={transition.id}>
          <TableCell className="font-mono text-xs">{transition.code}</TableCell>
          <TableCell>{transition.fromStateCode}</TableCell>
          <TableCell>{transition.toStateCode}</TableCell>
          <TableCell>{transition.guards.length}</TableCell>
          <TableCell>{transition.actions.length}</TableCell>
          <TableCell>{editable ? <>
            <Button size="sm" variant="ghost" onClick={() => onEdit(transition)}>{t("workflows.open")}</Button>
            <Button size="sm" variant="ghost" aria-label={t("workflows.addGuard")} onClick={() => onRule(transition)}><ShieldCheck className="h-4 w-4" /></Button>
            <Button size="sm" variant="ghost" onClick={() => onDelete(transition)}><Trash2 className="h-4 w-4" /></Button>
          </> : null}</TableCell>
        </TableRow>)}</TableBody>
      </Table> : <p className="text-sm text-muted-foreground">{t("workflows.noTransitions")}</p>}
    </CardContent>
  </Card>;
}
function PreviewPanel({ preview, locale, t }: { preview: { code: string; nameEn: string; nameAr: string; isInitial: boolean; isFinal: boolean; transitions: { code: string; nameEn: string; nameAr: string; toStateCode: string; toStateNameEn: string; toStateNameAr: string }[] }[]; locale: string; t: (key: string) => string }) {
  return <Card>
    <CardHeader><CardTitle>{t("workflows.preview")}</CardTitle><CardDescription>{t("workflows.previewHint")}</CardDescription></CardHeader>
    <CardContent className="grid gap-3 md:grid-cols-2">
      {preview.map((state) => <div key={state.code} className="rounded-xl border border-border/70 p-4">
        <div className="flex items-center justify-between">
          <div><p className="font-semibold">{locale === "ar" ? state.nameAr : state.nameEn}</p><p className="font-mono text-[11px] text-muted-foreground">{state.code}</p></div>
          <div className="flex gap-1">{state.isInitial ? <Badge status="Initial">{t("workflows.initial")}</Badge> : null}{state.isFinal ? <Badge status="Done">{t("workflows.final")}</Badge> : null}</div>
        </div>
        <div className="mt-3 space-y-2">
          {state.transitions.length ? state.transitions.map((transition) => <div key={transition.code} className="flex items-center gap-2 text-xs">
            <ChevronRight className="h-3.5 w-3.5 text-primary rtl:rotate-180" />
            <span>{locale === "ar" ? transition.nameAr : transition.nameEn}</span>
            <span className="text-muted-foreground">→ {locale === "ar" ? transition.toStateNameAr : transition.toStateNameEn}</span>
          </div>) : <p className="text-xs text-muted-foreground">{t("workflows.noTransitions")}</p>}
        </div>
      </div>)}
    </CardContent>
  </Card>;
}

function StateSheet({ open, onOpenChange, state, versionId, editable, t, mutations }: { open: boolean; onOpenChange: (open: boolean) => void; state?: WorkflowState; versionId: string; editable: boolean; t: (key: string) => string; mutations: ReturnType<typeof useFullWorkflowMutations> }) {
  const [form, setForm] = React.useState<SaveWorkflowState>({ code: "", nameEn: "", nameAr: "", description: "", displayOrder: 0, isInitial: false, isFinal: false, isActive: true });
  React.useEffect(() => {
    setForm(state
      ? { code: state.code, nameEn: state.nameEn, nameAr: state.nameAr, description: state.description ?? "", displayOrder: state.displayOrder, isInitial: state.isInitial, isFinal: state.isFinal, isActive: state.isActive, rowVersion: state.rowVersion }
      : { code: "", nameEn: "", nameAr: "", description: "", displayOrder: 0, isInitial: false, isFinal: false, isActive: true });
  }, [state, open]);
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (!editable) return;
    if (state) await mutations.updateState.mutateAsync({ id: state.id, body: form });
    else await mutations.createState.mutateAsync({ id: versionId, body: form });
    onOpenChange(false);
  }
  return <SideSheet open={open} onOpenChange={onOpenChange} title={state ? t("workflows.open") : t("workflows.addState")}>
    <form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-3">
        <div className="space-y-1"><Label htmlFor="workflow-state-code">{t("workflows.code")}</Label><Input id="workflow-state-code" required value={form.code} disabled={!editable} onChange={(event) => setForm({ ...form, code: event.target.value })} /></div>
        <div className="space-y-1"><Label htmlFor="workflow-state-name-en">{t("workflows.nameEn")}</Label><Input id="workflow-state-name-en" required value={form.nameEn} disabled={!editable} onChange={(event) => setForm({ ...form, nameEn: event.target.value })} /></div>
        <div className="space-y-1"><Label htmlFor="workflow-state-name-ar">{t("workflows.nameAr")}</Label><Input id="workflow-state-name-ar" required value={form.nameAr} disabled={!editable} onChange={(event) => setForm({ ...form, nameAr: event.target.value })} /></div>
        <div className="space-y-1"><Label htmlFor="workflow-state-description">{t("workflows.stateDescription")}</Label><Textarea id="workflow-state-description" value={form.description ?? ""} disabled={!editable} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div>
        <div className="space-y-1"><Label htmlFor="workflow-state-order">{t("workflows.displayOrder")}</Label><Input id="workflow-state-order" type="number" value={form.displayOrder} disabled={!editable} onChange={(event) => setForm({ ...form, displayOrder: Number(event.target.value) })} /></div>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isInitial} disabled={!editable} onChange={(event) => setForm({ ...form, isInitial: event.target.checked })} />{t("workflows.initial")}</label>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isFinal} disabled={!editable} onChange={(event) => setForm({ ...form, isFinal: event.target.checked })} />{t("workflows.final")}</label>
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>{editable ? <Button type="submit">{t("common.save")}</Button> : null}</SideSheetFooter>
    </form>
  </SideSheet>;
}
function TransitionSheet({ open, onOpenChange, transition, states, versionId, editable, t, mutations }: { open: boolean; onOpenChange: (open: boolean) => void; transition?: WorkflowTransition; states: WorkflowState[]; versionId: string; editable: boolean; t: (key: string) => string; mutations: ReturnType<typeof useFullWorkflowMutations> }) {
  const [form, setForm] = React.useState<SaveWorkflowTransition>({ code: "", nameEn: "", nameAr: "", fromStateId: "", toStateId: "", displayOrder: 0, isActive: true });
  React.useEffect(() => {
    setForm(transition
      ? { code: transition.code, nameEn: transition.nameEn, nameAr: transition.nameAr, fromStateId: transition.fromStateId, toStateId: transition.toStateId, displayOrder: transition.displayOrder, isActive: transition.isActive, rowVersion: transition.rowVersion }
      : { code: "", nameEn: "", nameAr: "", fromStateId: states[0]?.id ?? "", toStateId: states[1]?.id ?? states[0]?.id ?? "", displayOrder: 0, isActive: true });
  }, [transition, states, open]);

  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (transition) await mutations.updateTransition.mutateAsync({ id: transition.id, body: form });
    else await mutations.createTransition.mutateAsync({ id: versionId, body: form });
    onOpenChange(false);
  }

  return <SideSheet open={open} onOpenChange={onOpenChange} title={transition ? t("workflows.open") : t("workflows.addTransition")}>
    <form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-3">
        <Label>{t("workflows.code")}</Label><Input required value={form.code} disabled={!editable} onChange={(event) => setForm({ ...form, code: event.target.value })} />
        <Label>{t("workflows.nameEn")}</Label><Input required value={form.nameEn} disabled={!editable} onChange={(event) => setForm({ ...form, nameEn: event.target.value })} />
        <Label>{t("workflows.nameAr")}</Label><Input required value={form.nameAr} disabled={!editable} onChange={(event) => setForm({ ...form, nameAr: event.target.value })} />
        <Label>{t("workflows.from")}</Label><select className="input-field" disabled={!editable} value={form.fromStateId} onChange={(event) => setForm({ ...form, fromStateId: event.target.value })}>{states.map((state) => <option key={state.id} value={state.id}>{state.code}</option>)}</select>
        <Label>{t("workflows.to")}</Label><select className="input-field" disabled={!editable} value={form.toStateId} onChange={(event) => setForm({ ...form, toStateId: event.target.value })}>{states.map((state) => <option key={state.id} value={state.id}>{state.code}</option>)}</select>
        <Label>{t("workflows.displayOrder")}</Label><Input type="number" value={form.displayOrder} disabled={!editable} onChange={(event) => setForm({ ...form, displayOrder: Number(event.target.value) })} />
        {transition ? <div className="space-y-3 rounded-lg border border-border/70 p-3 text-sm">
          <div>
            <p className="font-medium">{t("workflows.guards")}</p>
            {transition.guards.length ? <ul className="mt-1 list-disc space-y-1 ps-5 text-xs text-muted-foreground">{transition.guards.map((rule) => <li key={rule.id}>{rule.guardType}</li>)}</ul> : <p className="mt-1 text-xs text-muted-foreground">—</p>}
          </div>
          <div>
            <p className="font-medium">{t("workflows.actionsTab")}</p>
            {transition.actions.length ? <ul className="mt-1 list-disc space-y-1 ps-5 text-xs text-muted-foreground">{transition.actions.map((rule) => <li key={rule.id}>{rule.actionType}</li>)}</ul> : <p className="mt-1 text-xs text-muted-foreground">—</p>}
          </div>
        </div> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>{editable ? <Button type="submit">{t("common.save")}</Button> : null}</SideSheetFooter>
    </form>
  </SideSheet>;
}
