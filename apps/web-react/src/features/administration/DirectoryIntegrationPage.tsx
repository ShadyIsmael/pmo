import * as React from "react";
import { useDebounce } from "use-debounce";
import { ArrowLeft, CheckCircle2, PlugZap, Plus, RefreshCw, ShieldAlert, TestTube2 } from "lucide-react";
import { Link } from "react-router-dom";
import { useAuth } from "@auth/AuthProvider";
import { PageHeader } from "@components/layout/PageHeader";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { DirectoryAccessPermissions, hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import {
  useDirectoryConnections, useSaveDirectoryConnection, useTestDirectoryConnection,
  useDirectoryScope, useSaveDirectoryScope, useDirectoryFieldMappings, useSaveDirectoryFieldMappings,
  useDirectoryGroups, useDirectoryGroupMappings, useSaveDirectoryGroupMappings,
  useDirectoryOrganizationMappings, useSaveDirectoryOrganizationMappings, useDirectoryPolicy, useSaveDirectoryPolicy,
  useDirectoryPreview, useExecuteDirectorySync, useDirectoryHistory, useDirectoryHistoryDetail,
  useDirectoryReferenceData,
  type DirectoryConnectionDto, type DirectoryFieldMappingDto, type DirectoryGroupMappingDto,
  type DirectoryOrganizationMappingDto, type DirectoryPreviewItemDto, type DirectoryProviderType,
  type DirectoryProvisioningPolicyDto, type DirectorySyncScopeDto, type SaveDirectoryConnectionInput,
  type SelectedDirectorySyncItem,
} from "./api/directoryIntegration";

type IntegrationTab = "configuration" | "scope" | "mappings" | "provisioning" | "preview" | "history";
const textControl = "h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50";
const defaultConnectionDraft: SaveDirectoryConnectionInput = { name: "", providerType: "Entra", isActive: true, isDefault: false, useSsl: true, port: 636 };
const actionFor = (item: DirectoryPreviewItemDto) => item.proposedAction === "None" ? "Review" : item.proposedAction;
const itemKey = (item: Pick<SelectedDirectorySyncItem, "itemType" | "externalDirectoryId">) => `${item.itemType}:${item.externalDirectoryId}`;
const lines = (value: string) => value.split(/\r?\n/).map((part) => part.trim()).filter(Boolean);
const errorText = (error: unknown) => error instanceof Error ? error.message : "";

export function DirectoryIntegrationPage() {
  const { t, locale, formatDate } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const userPermissions = user?.permissions ?? [];
  const canView = hasAnyPermission(userPermissions, DirectoryAccessPermissions);
  const canManage = hasPermission(userPermissions, Permissions.Directory.Manage);
  const canTest = hasAnyPermission(userPermissions, [Permissions.Directory.TestConnection, Permissions.Directory.Manage]);
  const canPreview = hasAnyPermission(userPermissions, [Permissions.Directory.PreviewSync, Permissions.Directory.Manage]);
  const canExecute = hasAnyPermission(userPermissions, [Permissions.Directory.ExecuteSync, Permissions.Directory.Manage]);
  const canMap = hasAnyPermission(userPermissions, [Permissions.Directory.ManageMappings, Permissions.Directory.Manage]);
  const canManagePolicy = hasAnyPermission(userPermissions, [Permissions.Directory.ManageProvisioningPolicy, Permissions.Directory.Manage]);

  const connections = useDirectoryConnections(canView);
  const saveConnection = useSaveDirectoryConnection();
  const testConnection = useTestDirectoryConnection();
  const saveScope = useSaveDirectoryScope();
  const saveFieldMappings = useSaveDirectoryFieldMappings();
  const saveGroupMappings = useSaveDirectoryGroupMappings();
  const saveOrganizationMappings = useSaveDirectoryOrganizationMappings();
  const savePolicy = useSaveDirectoryPolicy();
  const executeSync = useExecuteDirectorySync();

  const [activeConnectionId, setActiveConnectionId] = React.useState<string | null>(null);
  const activeConnection = connections.data?.find((connection) => connection.id === activeConnectionId) ?? null;
  const [activeTab, setActiveTab] = React.useState<IntegrationTab>("configuration");
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editingConnection, setEditingConnection] = React.useState<DirectoryConnectionDto | null>(null);
  const [connectionDraft, setConnectionDraft] = React.useState<SaveDirectoryConnectionInput>(defaultConnectionDraft);
  const [connectionError, setConnectionError] = React.useState("");
  const [connectionNotice, setConnectionNotice] = React.useState("");
  const [testMessage, setTestMessage] = React.useState("");
  const [testIsError, setTestIsError] = React.useState(false);

  React.useEffect(() => {
    const list = connections.data ?? [];
    if (list.length > 0 && (!activeConnectionId || !list.some((connection) => connection.id === activeConnectionId))) {
      setActiveConnectionId(list.find((connection) => connection.isDefault)?.id ?? list[0].id);
    }
    if (list.length === 0) setActiveConnectionId(null);
  }, [connections.data, activeConnectionId]);

  const scope = useDirectoryScope(activeConnectionId, activeTab === "scope");
  const saveScopeLocal = saveScope;
  const [scopeGroupSearch, setScopeGroupSearch] = React.useState("");
  const [scopeGroupPage, setScopeGroupPage] = React.useState(1);
  const [mappingGroupSearch, setMappingGroupSearch] = React.useState("");
  const [mappingGroupPage, setMappingGroupPage] = React.useState(1);
  const groupSearch = activeTab === "scope" ? scopeGroupSearch : mappingGroupSearch;
  const groupPage = activeTab === "scope" ? scopeGroupPage : mappingGroupPage;
  const [debouncedGroupSearch] = useDebounce(groupSearch, 250);
  const groupsCatalog = useDirectoryGroups(activeConnectionId, (activeTab === "scope" && canManage) || (activeTab === "mappings" && canMap), debouncedGroupSearch, groupPage);
  const fieldMappingsQuery = useDirectoryFieldMappings(activeConnectionId, activeTab === "mappings");
  const groupMappingsQuery = useDirectoryGroupMappings(activeConnectionId, activeTab === "mappings");
  const organizationMappingsQuery = useDirectoryOrganizationMappings(activeConnectionId, activeTab === "mappings");
  const policyQuery = useDirectoryPolicy(activeConnectionId, activeTab === "provisioning");
  const referenceData = useDirectoryReferenceData(activeTab === "mappings" || activeTab === "provisioning");
  const history = useDirectoryHistory(activeTab === "history");

  const [scopeDraft, setScopeDraft] = React.useState<DirectorySyncScopeDto>({ scopeType: "SelectedGroups", selectedGroupIds: [], selectedUserIds: [], organizationalUnits: [], maxItems: 500 });
  const [fieldDraft, setFieldDraft] = React.useState<DirectoryFieldMappingDto[]>([]);
  const [groupDraft, setGroupDraft] = React.useState<DirectoryGroupMappingDto[]>([]);
  const [organizationDraft, setOrganizationDraft] = React.useState<DirectoryOrganizationMappingDto[]>([]);
  const [policyDraft, setPolicyDraft] = React.useState<DirectoryProvisioningPolicyDto>({
    missingUserAction: "Review", missingGroupAction: "Review", disabledUserAction: "Review", outOfScopeUserAction: "Review",
    changedGroupAction: "Review", missingGroupReferenceAction: "Review", createResource: false,
  });
  const [page, setPage] = React.useState(1);
  const [previewType, setPreviewType] = React.useState<"Users" | "Groups">("Users");
  const [previewSearch, setPreviewSearch] = React.useState("");
  const [selectedRows, setSelectedRows] = React.useState<Record<string, SelectedDirectorySyncItem>>({});
  const [syncMessage, setSyncMessage] = React.useState("");
  const [historyRunId, setHistoryRunId] = React.useState<string | null>(null);
  const historyDetail = useDirectoryHistoryDetail(historyRunId, Boolean(historyRunId));
  const preview = useDirectoryPreview(activeConnectionId, previewType, page, 50, previewSearch.trim(), activeTab === "preview" && canPreview);

  React.useEffect(() => { if (scope.data) setScopeDraft(scope.data); }, [scope.data]);
  React.useEffect(() => { if (fieldMappingsQuery.data) setFieldDraft(fieldMappingsQuery.data); }, [fieldMappingsQuery.data]);
  React.useEffect(() => {
    if (groupMappingsQuery.data) setGroupDraft(groupMappingsQuery.data);
  }, [groupMappingsQuery.data]);
  React.useEffect(() => { if (organizationMappingsQuery.data) setOrganizationDraft(organizationMappingsQuery.data); }, [organizationMappingsQuery.data]);
  React.useEffect(() => { if (policyQuery.data) setPolicyDraft(policyQuery.data); }, [policyQuery.data]);
  React.useEffect(() => { setPage(1); setSelectedRows({}); setSyncMessage(""); }, [activeConnectionId, previewType]);

  function startCreateConnection() {
    setEditingConnection(null);
    setConnectionDraft({ ...defaultConnectionDraft });
    setConnectionError("");
    setConnectionNotice("");
    setSheetOpen(true);
  }
  function startEditConnection(connection: DirectoryConnectionDto) {
    setEditingConnection(connection);
    setConnectionDraft({
      name: connection.name, providerType: connection.providerType, isActive: connection.isActive, isDefault: connection.isDefault,
      useSsl: connection.useSsl, directoryTenantId: connection.directoryTenantId ?? "", clientId: connection.clientId ?? "",
      credentialReference: connection.credentialReference ?? "", host: connection.host ?? "", port: connection.port ?? 636,
      baseDn: connection.baseDn ?? "", bindUser: connection.bindUser ?? "", bindCredentialReference: connection.bindCredentialReference ?? "", domain: connection.domain ?? "",
    });
    setConnectionError("");
    setConnectionNotice("");
    setSheetOpen(true);
  }
  function updateConnection<K extends keyof SaveDirectoryConnectionInput>(key: K, value: SaveDirectoryConnectionInput[K]) {
    setConnectionDraft((current) => ({ ...current, [key]: value }));
  }
  async function submitConnection(event: React.FormEvent) {
    event.preventDefault();
    setConnectionError("");
    try {
      const saved = await saveConnection.mutateAsync({ id: editingConnection?.id, input: connectionDraft });
      setActiveConnectionId(saved.id);
      setSheetOpen(false);
      setConnectionNotice(t("administration.integrations.directory.connectionSaved"));
    } catch (error) { setConnectionError(errorText(error)); }
  }
  async function runConnectionTest() {
    if (!activeConnection) return;
    setTestMessage("");
    setTestIsError(false);
    try {
      const result = await testConnection.mutateAsync(activeConnection.id);
      setTestMessage(result.connected ? `${t("administration.integrations.directory.testPassed")} (${result.latencyMilliseconds} ms)` : result.message ?? t("administration.integrations.directory.testFailed"));
      setTestIsError(!result.connected);
    } catch (error) { setTestMessage(errorText(error)); setTestIsError(true); }
  }
  async function persistScope() {
    if (!activeConnectionId) return;
    try { await saveScopeLocal.mutateAsync({ id: activeConnectionId, input: scopeDraft }); setConnectionNotice(t("administration.integrations.directory.saved")); }
    catch (error) { setConnectionNotice(errorText(error)); }
  }
  async function persistFieldMappings() {
    if (!activeConnectionId) return;
    try { await saveFieldMappings.mutateAsync({ id: activeConnectionId, mappings: fieldDraft }); setConnectionNotice(t("administration.integrations.directory.saved")); }
    catch (error) { setConnectionNotice(errorText(error)); }
  }
  async function persistGroupMappings() {
    if (!activeConnectionId) return;
    try { await saveGroupMappings.mutateAsync({ id: activeConnectionId, mappings: groupDraft }); setConnectionNotice(t("administration.integrations.directory.saved")); }
    catch (error) { setConnectionNotice(errorText(error)); }
  }
  async function persistOrganizationMappings() {
    if (!activeConnectionId) return;
    try { await saveOrganizationMappings.mutateAsync({ id: activeConnectionId, mappings: organizationDraft }); setConnectionNotice(t("administration.integrations.directory.saved")); }
    catch (error) { setConnectionNotice(errorText(error)); }
  }
  async function persistPolicy() {
    if (!activeConnectionId) return;
    try { await savePolicy.mutateAsync({ id: activeConnectionId, input: policyDraft }); setConnectionNotice(t("administration.integrations.directory.saved")); }
    catch (error) { setConnectionNotice(errorText(error)); }
  }
  function toggleSelected(item: DirectoryPreviewItemDto, checked: boolean) {
    const key = itemKey(item);
    setSelectedRows((current) => {
      const next = { ...current };
      if (!checked) delete next[key];
      else next[key] = { itemType: item.itemType, externalDirectoryId: item.externalDirectoryId, action: actionFor(item) };
      return next;
    });
  }
  function setRowAction(item: DirectoryPreviewItemDto, action: string) {
    const key = itemKey(item);
    setSelectedRows((current) => ({ ...current, [key]: { itemType: item.itemType, externalDirectoryId: item.externalDirectoryId, action } }));
  }
  async function synchronizeSelected() {
    if (!activeConnectionId) return;
    const items = Object.values(selectedRows);
    if (!items.length || !await confirm({ description: t("administration.integrations.directory.confirmSync") })) return;
    setSyncMessage("");
    try {
      const result = await executeSync.mutateAsync({ id: activeConnectionId, items });
      setSyncMessage(`${t("administration.integrations.directory.syncSucceeded")} ${result.run.usersCreated + result.run.usersUpdated + result.run.usersDeactivated} ${t("administration.integrations.directory.users").toLocaleLowerCase(locale)} · ${result.run.groupsCreated + result.run.groupsUpdated + result.run.groupsDeactivated} ${t("administration.integrations.directory.groups").toLocaleLowerCase(locale)}`);
      setSelectedRows({});
    } catch (error) { setSyncMessage(errorText(error)); }
  }

  const tabs: { id: IntegrationTab; label: string; allowed: boolean }[] = [
    { id: "configuration", label: t("administration.integrations.directory.configuration"), allowed: true },
    { id: "scope", label: t("administration.integrations.directory.scope"), allowed: canManage },
    { id: "mappings", label: t("administration.integrations.directory.mappings"), allowed: true },
    { id: "provisioning", label: t("administration.integrations.directory.provisioning"), allowed: true },
    { id: "preview", label: t("administration.integrations.directory.preview"), allowed: canPreview },
    { id: "history", label: t("administration.integrations.directory.history"), allowed: true },
  ];
  const visibleTabs = tabs.filter((tab) => tab.allowed);
  const selectedCount = Object.keys(selectedRows).length;
  const canWriteMapping = canMap;
  const messages = "administration.integrations.directory";

  return (
    <div className="space-y-5">
      <PageHeader title={t(`${messages}.title`)} description={t(`${messages}.description`)} actions={<Button asChild variant="outline" size="sm"><Link to="/administration/integrations"><ArrowLeft className="me-2 h-4 w-4" />{t(`${messages}.back`)}</Link></Button>} />
      {connections.isError ? <ErrorCard message={errorText(connections.error) || t(`${messages}.errorLoading`)} retry={() => void connections.refetch()} label={t(`${messages}.retry`)} /> : null}
      {connectionNotice ? <p role="status" className="text-sm text-muted-foreground">{connectionNotice}</p> : null}

      <Card>
        <CardContent className="flex flex-col gap-3 p-4 md:flex-row md:items-center">
          <div className="flex-1">
            <Label htmlFor="directory-connection">{t(`${messages}.chooseConnection`)}</Label>
            <select id="directory-connection" className={`${textControl} mt-1`} value={activeConnectionId ?? ""} onChange={(event) => setActiveConnectionId(event.target.value || null)} disabled={!connections.data?.length}>
              {!connections.data?.length ? <option value="">{t(`${messages}.noConnections`)}</option> : connections.data.map((connection) => <option key={connection.id} value={connection.id}>{connection.name} · {connection.providerType === "Entra" ? t(`${messages}.entra`) : t(`${messages}.ldap`)}</option>)}
            </select>
          </div>
          {activeConnection ? <div className="flex flex-wrap items-center gap-2">
            <Badge status={activeConnection.isActive ? "Active" : "Inactive"}>{t(`${messages}.${activeConnection.isActive ? "active" : "inactive"}`)}</Badge>
            {activeConnection.isDefault ? <Badge variant="outline">{t(`${messages}.default`)}</Badge> : null}
            {canTest ? <Button type="button" variant="outline" size="sm" disabled={testConnection.isPending} onClick={() => void runConnectionTest()}><TestTube2 className="me-2 h-4 w-4" />{testConnection.isPending ? t(`${messages}.testing`) : t(`${messages}.testConnection`)}</Button> : null}
            {canManage ? <Button type="button" size="sm" onClick={() => startEditConnection(activeConnection)}>{t("common.edit")}</Button> : null}
          </div> : canManage ? <Button type="button" size="sm" onClick={startCreateConnection}><Plus className="me-2 h-4 w-4" />{t(`${messages}.addConnection`)}</Button> : null}
        </CardContent>
      </Card>
      {testMessage ? <div role={testIsError ? "alert" : "status"} className={`flex items-start gap-2 text-sm ${testIsError ? "text-destructive" : "text-success"}`}>
        {testIsError ? <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" /> : <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />}{testMessage}
      </div> : null}

      {!activeConnection ? (
        <Card><CardContent className="flex flex-col items-center gap-3 p-10 text-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 text-primary"><PlugZap className="h-6 w-6" /></div>
          <p className="max-w-xl text-sm text-muted-foreground">{t(`${messages}.noConnections`)}</p>
          {canManage ? <Button type="button" size="sm" onClick={startCreateConnection}><Plus className="me-2 h-4 w-4" />{t(`${messages}.addConnection`)}</Button> : null}
        </CardContent></Card>
      ) : <>
        <nav className="flex max-w-full gap-1 overflow-x-auto border-b" aria-label={t(`${messages}.title`)}>
          {visibleTabs.map((tab) => <button key={tab.id} type="button" onClick={() => setActiveTab(tab.id)} aria-current={activeTab === tab.id ? "page" : undefined}
            className={`shrink-0 border-b-2 px-3 py-2 text-sm font-medium transition-colors ${activeTab === tab.id ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`}>{tab.label}</button>)}
        </nav>

        {activeTab === "configuration" ? <Card>
          <CardHeader><CardTitle>{activeConnection.name}</CardTitle><CardDescription>{activeConnection.providerType === "Entra" ? t(`${messages}.entra`) : t(`${messages}.ldap`)}</CardDescription></CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div className="grid gap-3 sm:grid-cols-2">
              <InfoLine label={t(`${messages}.provider`)} value={activeConnection.providerType === "Entra" ? t(`${messages}.entra`) : t(`${messages}.ldap`)} />
              <InfoLine label={t(`${messages}.name`)} value={activeConnection.name} />
              {activeConnection.providerType === "Entra" ? <>
                <InfoLine label={t(`${messages}.directoryTenantId`)} value={activeConnection.directoryTenantId ?? "—"} />
                <InfoLine label={t(`${messages}.clientId`)} value={activeConnection.clientId ?? "—"} />
                <InfoLine label={t(`${messages}.credentialReference`)} value={activeConnection.credentialReference ?? "—"} />
                <InfoLine label={t(`${messages}.secretConfigured`)} value={t(`${messages}.${activeConnection.credentialConfigured ? "secretConfigured" : "secretMissing"}`)} />
                {user?.tenantId && activeConnection.credentialReference ? <>
                  <InfoLine label={t(`${messages}.secretConfigurationKey`)} value={`DirectorySecrets:${user.tenantId}:${activeConnection.id}:${activeConnection.credentialReference}`} />
                  <InfoLine label={t(`${messages}.environmentVariableKey`)} value={`DirectorySecrets__${user.tenantId}__${activeConnection.id}__${activeConnection.credentialReference}`} />
                </> : null}
              </> : <>
                <InfoLine label={t(`${messages}.host`)} value={activeConnection.host ?? "—"} />
                <InfoLine label={t(`${messages}.port`)} value={String(activeConnection.port ?? 636)} />
                <InfoLine label={t(`${messages}.baseDn`)} value={activeConnection.baseDn ?? "—"} />
                <InfoLine label={t(`${messages}.bindUser`)} value={activeConnection.bindUser ?? "—"} />
                <InfoLine label={t(`${messages}.bindCredentialReference`)} value={activeConnection.bindCredentialReference ?? "—"} />
                <InfoLine label={t(`${messages}.secretConfigured`)} value={t(`${messages}.${activeConnection.bindCredentialConfigured ? "secretConfigured" : "secretMissing"}`)} />
                {user?.tenantId && activeConnection.bindCredentialReference ? <>
                  <InfoLine label={t(`${messages}.secretConfigurationKey`)} value={`DirectorySecrets:${user.tenantId}:${activeConnection.id}:${activeConnection.bindCredentialReference}`} />
                  <InfoLine label={t(`${messages}.environmentVariableKey`)} value={`DirectorySecrets__${user.tenantId}__${activeConnection.id}__${activeConnection.bindCredentialReference}`} />
                </> : null}
              </>}
            </div>
            <p className="rounded-md border border-muted bg-muted/30 p-3 text-muted-foreground">{t(`${messages}.secretReferenceHint`)}</p>
            <p className="text-muted-foreground">{t(`${messages}.externalReadNote`)}</p>
            {activeConnection.providerType === "Ldap" ? <p className="text-warning">{t(`${messages}.ldapTlsOnly`)}</p> : null}
            <p className="text-muted-foreground">{t(`${messages}.identityWarning`)}</p>
          </CardContent>
        </Card> : null}

        {activeTab === "scope" ? <ScopePanel
          t={t} messages={messages} draft={scopeDraft} setDraft={setScopeDraft} canManage={canManage}
          groups={groupsCatalog.data?.items ?? []} groupsLoading={groupsCatalog.isLoading} groupSearch={scopeGroupSearch}
          setGroupSearch={(value) => { setScopeGroupSearch(value); setScopeGroupPage(1); }} groupPage={scopeGroupPage}
          groupsHasMore={groupsCatalog.data?.hasMore ?? false} onPreviousGroups={() => setScopeGroupPage((current) => Math.max(1, current - 1))}
          onNextGroups={() => setScopeGroupPage((current) => current + 1)} onRefreshGroups={() => void groupsCatalog.refetch()}
          saving={saveScopeLocal.isPending} onSave={() => void persistScope()} error={scope.isError ? errorText(scope.error) : ""}
        /> : null}

        {activeTab === "mappings" ? <MappingsPanel
          t={t} messages={messages} locale={locale} provider={activeConnection.providerType} canManage={canWriteMapping}
          fieldDraft={fieldDraft} setFieldDraft={setFieldDraft} onSaveFields={() => void persistFieldMappings()} savingFields={saveFieldMappings.isPending}
          groups={groupsCatalog.data?.items ?? []} groupSearch={mappingGroupSearch}
          setGroupSearch={(value) => { setMappingGroupSearch(value); setMappingGroupPage(1); }} groupPage={mappingGroupPage}
          groupsHasMore={groupsCatalog.data?.hasMore ?? false} onPreviousGroups={() => setMappingGroupPage((current) => Math.max(1, current - 1))}
          onNextGroups={() => setMappingGroupPage((current) => current + 1)} groupDraft={groupDraft} setGroupDraft={setGroupDraft}
          roles={referenceData.roles.data ?? []} onSaveGroups={() => void persistGroupMappings()} savingGroups={saveGroupMappings.isPending}
          organizationDraft={organizationDraft} setOrganizationDraft={setOrganizationDraft} units={referenceData.units.data ?? []}
          onSaveOrganizations={() => void persistOrganizationMappings()} savingOrganizations={saveOrganizationMappings.isPending}
          groupsError={groupsCatalog.isError ? errorText(groupsCatalog.error) : ""}
        /> : null}

        {activeTab === "provisioning" ? <ProvisioningPanel
          t={t} messages={messages} draft={policyDraft} setDraft={setPolicyDraft} canManage={canManagePolicy}
          units={referenceData.units.data ?? []} calendars={referenceData.calendars.data ?? []}
          saving={savePolicy.isPending} onSave={() => void persistPolicy()}
        /> : null}

        {activeTab === "preview" ? <PreviewPanel
          t={t} messages={messages} itemType={previewType} setItemType={setPreviewType} search={previewSearch} setSearch={setPreviewSearch}
          page={page} setPage={setPage} data={preview.data} loading={preview.isLoading || preview.isFetching} error={preview.isError ? errorText(preview.error) : ""}
          canExecute={canExecute} selectedRows={selectedRows} onToggle={toggleSelected} onSetAction={setRowAction}
          onRefresh={() => void preview.refetch()} onSync={() => void synchronizeSelected()} syncing={executeSync.isPending}
          selectedCount={selectedCount} syncMessage={syncMessage}
        /> : null}

        {activeTab === "history" ? <HistoryPanel t={t} messages={messages} runs={history.data ?? []} loading={history.isLoading} error={history.isError ? errorText(history.error) : ""} onOpen={(id) => setHistoryRunId(id)} formatDate={formatDate} /> : null}
      </>}

      <ConnectionSheet
        t={t} messages={messages} open={sheetOpen} onOpenChange={setSheetOpen} draft={connectionDraft} update={updateConnection}
        editing={Boolean(editingConnection)} saving={saveConnection.isPending} error={connectionError} onSubmit={(event) => void submitConnection(event)}
      />
      <SideSheet open={Boolean(historyRunId)} onOpenChange={(open) => { if (!open) setHistoryRunId(null); }} title={t(`${messages}.summary`)} description={historyDetail.data?.run.connectionName} size="xl" closeLabel={t(`${messages}.cancel`)}>
        <SideSheetBody className="space-y-4">
          {historyDetail.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : null}
          {historyDetail.data ? <>
            <div className="grid gap-3 sm:grid-cols-3"><InfoLine label={t(`${messages}.startedAt`)} value={formatDate(historyDetail.data.run.startedAt)} /><InfoLine label={t(`${messages}.runStatus`)} value={historyDetail.data.run.status} /><InfoLine label={t(`${messages}.items`)} value={String(historyDetail.data.items.length)} /></div>
            <Table><TableHeader><TableRow><TableHead>{t(`${messages}.classification`)}</TableHead><TableHead>{t(`${messages}.matchedIdentity`)}</TableHead><TableHead>{t(`${messages}.proposedAction`)}</TableHead><TableHead>{t(`${messages}.runStatus`)}</TableHead></TableRow></TableHeader>
              <TableBody>{historyDetail.data.items.map((item) => <TableRow key={item.id}><TableCell><Badge status={item.classification}>{item.classification}</Badge></TableCell><TableCell>{item.displayName ?? item.externalDirectoryId}</TableCell><TableCell>{item.proposedAction}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell></TableRow>)}</TableBody>
            </Table>
          </> : null}
        </SideSheetBody>
      </SideSheet>
    </div>
  );
}

function InfoLine({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0 rounded-md border p-3"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 break-all font-medium">{value}</p></div>;
}

function ErrorCard({ message, retry, label }: { message: string; retry: () => void; label: string }) {
  return <Card><CardContent className="flex flex-wrap items-center justify-between gap-3 p-4"><p role="alert" className="text-sm text-destructive">{message}</p><Button type="button" variant="outline" size="sm" onClick={retry}>{label}</Button></CardContent></Card>;
}

function ConnectionSheet({
  t, messages, open, onOpenChange, draft, update, editing, saving, error, onSubmit,
}: {
  t: (key: string) => string; messages: string; open: boolean; onOpenChange: (open: boolean) => void;
  draft: SaveDirectoryConnectionInput; update: <K extends keyof SaveDirectoryConnectionInput>(key: K, value: SaveDirectoryConnectionInput[K]) => void;
  editing: boolean; saving: boolean; error: string; onSubmit: (event: React.FormEvent) => void;
}) {
  const m = (key: string) => t(`${messages}.${key}`);
  const entra = draft.providerType === "Entra";
  return <SideSheet open={open} onOpenChange={onOpenChange} title={editing ? m("editConnection") : m("newConnection")} description={m("secretReferenceHint")} size="lg" closeLabel={m("cancel")}>
    <form onSubmit={onSubmit} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        <Field label={m("name")}><Input required maxLength={150} value={draft.name} onChange={(event) => update("name", event.target.value)} /></Field>
        <Field label={m("provider")}><select className={textControl} value={draft.providerType} onChange={(event) => update("providerType", event.target.value as DirectoryProviderType)}><option value="Entra">{m("entra")}</option><option value="Ldap">{m("ldap")}</option></select></Field>
        {entra ? <>
          <Field label={m("directoryTenantId")}><Input required value={draft.directoryTenantId ?? ""} onChange={(event) => update("directoryTenantId", event.target.value)} /></Field>
          <Field label={m("clientId")}><Input required value={draft.clientId ?? ""} onChange={(event) => update("clientId", event.target.value)} /></Field>
          <Field label={m("credentialReference")}><Input required value={draft.credentialReference ?? ""} onChange={(event) => update("credentialReference", event.target.value)} /></Field>
        </> : <>
          <Field label={m("host")}><Input required value={draft.host ?? ""} onChange={(event) => update("host", event.target.value)} /></Field>
          <Field label={m("port")}><select className={textControl} value={draft.port ?? 636} onChange={(event) => update("port", Number(event.target.value))}><option value={636}>636 · LDAPS</option><option value={3269}>3269 · Global Catalog LDAPS</option></select></Field>
          <Field label={m("baseDn")}><Input required value={draft.baseDn ?? ""} onChange={(event) => update("baseDn", event.target.value)} placeholder="DC=example,DC=com" /></Field>
          <Field label={m("bindUser")}><Input required value={draft.bindUser ?? ""} onChange={(event) => update("bindUser", event.target.value)} /></Field>
          <Field label={m("bindCredentialReference")}><Input required value={draft.bindCredentialReference ?? ""} onChange={(event) => update("bindCredentialReference", event.target.value)} /></Field>
          <Field label={m("domain")}><Input value={draft.domain ?? ""} onChange={(event) => update("domain", event.target.value)} /></Field>
          <p className="text-xs text-warning">{m("ldapTlsOnly")}</p>
        </>}
        <p className="rounded-md border bg-muted/30 p-3 text-xs text-muted-foreground">{m("secretReferenceHint")}</p>
        <CheckField checked={draft.isActive} onChange={(value) => update("isActive", value)} label={m("active")} />
        <CheckField checked={draft.isDefault} onChange={(value) => update("isDefault", value)} label={m("default")} />
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{m("cancel")}</Button><Button type="submit" disabled={saving}>{saving ? t("common.saving") : m(editing ? "saveChanges" : "save")}</Button></SideSheetFooter>
    </form>
  </SideSheet>;
}

function ScopePanel({ t, messages, draft, setDraft, canManage, groups, groupsLoading, groupSearch, setGroupSearch, groupPage, groupsHasMore, onPreviousGroups, onNextGroups, onRefreshGroups, saving, onSave, error }: {
  t: (key: string) => string; messages: string; draft: DirectorySyncScopeDto; setDraft: React.Dispatch<React.SetStateAction<DirectorySyncScopeDto>>;
  canManage: boolean; groups: { externalDirectoryId: string; name: string }[]; groupsLoading: boolean; groupSearch: string; setGroupSearch: (value: string) => void;
  groupPage: number; groupsHasMore: boolean; onPreviousGroups: () => void; onNextGroups: () => void; onRefreshGroups: () => void;
  saving: boolean; onSave: () => void; error: string;
}) {
  const m = (key: string) => t(`${messages}.${key}`);
  const update = (values: Partial<DirectorySyncScopeDto>) => setDraft((current) => ({ ...current, ...values }));
  return <Card><CardHeader><CardTitle>{m("scope")}</CardTitle><CardDescription>{m("scopeWarning")}</CardDescription></CardHeader><CardContent className="space-y-4">
    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    <Field label={m("scopeType")}><select disabled={!canManage} className={textControl} value={draft.scopeType} onChange={(event) => update({ scopeType: event.target.value as DirectorySyncScopeDto["scopeType"] })}>
      {([["SelectedGroups", "selectedGroups"], ["SelectedUsers", "selectedUsers"], ["OrganizationalUnits", "organizationalUnits"], ["AllUsers", "allUsers"]] as const).map(([value, label]) => <option key={value} value={value}>{m(label)}</option>)}
    </select></Field>
    {draft.scopeType === "SelectedGroups" ? <div className="space-y-2">
      <div className="flex gap-2"><Input value={groupSearch} onChange={(event) => setGroupSearch(event.target.value)} placeholder={m("groupSearch")} aria-label={m("groupSearch")} /><Button type="button" variant="outline" size="sm" onClick={onRefreshGroups} aria-label={m("refreshGroups")}><RefreshCw className="h-4 w-4" /></Button></div>
      {groupsLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : <div className="max-h-64 space-y-2 overflow-auto rounded-md border p-3">
        {groups.map((group) => <CheckField key={group.externalDirectoryId} disabled={!canManage} checked={draft.selectedGroupIds.includes(group.externalDirectoryId)} onChange={(checked) => update({ selectedGroupIds: checked ? [...draft.selectedGroupIds, group.externalDirectoryId] : draft.selectedGroupIds.filter((id) => id !== group.externalDirectoryId) })} label={group.name} />)}
        {groups.length === 0 ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : null}
      </div>}
      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="outline" size="sm" disabled={groupPage <= 1 || groupsLoading} onClick={onPreviousGroups}>{m("previousGroups")}</Button>
        <span className="text-xs text-muted-foreground">{groupPage}</span>
        <Button type="button" variant="outline" size="sm" disabled={!groupsHasMore || groupsLoading} onClick={onNextGroups}>{m("nextGroups")}</Button>
      </div>
    </div> : null}
    {draft.scopeType === "SelectedUsers" ? <Field label={m("selectedUserIds")}><textarea disabled={!canManage} className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.selectedUserIds.join("\n")} onChange={(event) => update({ selectedUserIds: lines(event.target.value) })} /></Field> : null}
    {draft.scopeType === "OrganizationalUnits" ? <Field label={m("ouValues")}><textarea disabled={!canManage} className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.organizationalUnits.join("\n")} onChange={(event) => update({ organizationalUnits: lines(event.target.value) })} /></Field> : null}
    <div className="grid gap-4 sm:grid-cols-3">
      <Field label={m("maxItems")}><Input disabled={!canManage} type="number" min={1} max={5000} value={draft.maxItems} onChange={(event) => update({ maxItems: Number(event.target.value) })} /></Field>
      <Field label={m("userFilter")}><Input disabled={!canManage} maxLength={1024} value={draft.userFilter ?? ""} onChange={(event) => update({ userFilter: event.target.value })} /></Field>
      <Field label={m("groupFilter")}><Input disabled={!canManage} maxLength={1024} value={draft.groupFilter ?? ""} onChange={(event) => update({ groupFilter: event.target.value })} /></Field>
    </div>
    {canManage ? <Button type="button" size="sm" onClick={onSave} disabled={saving}>{saving ? t("common.saving") : m("saveScope")}</Button> : null}
  </CardContent></Card>;
}

function MappingsPanel({ t, messages, locale, provider, canManage, fieldDraft, setFieldDraft, onSaveFields, savingFields, groups, groupSearch, setGroupSearch, groupPage, groupsHasMore, onPreviousGroups, onNextGroups, groupDraft, setGroupDraft, roles, onSaveGroups, savingGroups, organizationDraft, setOrganizationDraft, units, onSaveOrganizations, savingOrganizations, groupsError }: {
  t: (key: string) => string; messages: string; locale: "en" | "ar"; provider: DirectoryProviderType; canManage: boolean;
  fieldDraft: DirectoryFieldMappingDto[]; setFieldDraft: React.Dispatch<React.SetStateAction<DirectoryFieldMappingDto[]>>; onSaveFields: () => void; savingFields: boolean;
  groups: { externalDirectoryId: string; name: string }[]; groupSearch: string; setGroupSearch: (value: string) => void; groupPage: number; groupsHasMore: boolean; onPreviousGroups: () => void; onNextGroups: () => void;
  groupDraft: DirectoryGroupMappingDto[]; setGroupDraft: React.Dispatch<React.SetStateAction<DirectoryGroupMappingDto[]>>;
  roles: { id: string; name: string }[]; onSaveGroups: () => void; savingGroups: boolean;
  organizationDraft: DirectoryOrganizationMappingDto[]; setOrganizationDraft: React.Dispatch<React.SetStateAction<DirectoryOrganizationMappingDto[]>>;
  units: { id: string; name: string; code?: string | null }[]; onSaveOrganizations: () => void; savingOrganizations: boolean; groupsError: string;
}) {
  const m = (key: string) => t(`${messages}.${key}`);
  const fieldLabel = (field: string) => m(field);
  const attrs = provider === "Entra" ? ["givenName", "surname", "mail", "userPrincipalName", "jobTitle", "employeeId", "department", "manager", "accountEnabled"] : ["givenName", "sn", "mail", "userPrincipalName", "title", "employeeID", "department", "manager", "accountEnabled"];
  function updateField(index: number, changes: Partial<DirectoryFieldMappingDto>) { setFieldDraft((current) => current.map((item, i) => i === index ? { ...item, ...changes } : item)); }
  function updateGroup(id: string, changes: Partial<DirectoryGroupMappingDto>) { setGroupDraft((current) => current.map((item) => item.externalGroupId === id ? { ...item, ...changes } : item)); }
  function addGroup(group: { externalDirectoryId: string; name: string }) {
    setGroupDraft((current) => current.some((item) => item.externalGroupId === group.externalDirectoryId) ? current : [...current, { externalGroupId: group.externalDirectoryId, externalGroupName: group.name, roleId: null, isActive: true }]);
  }
  function addOrgMapping() { setOrganizationDraft((current) => [...current, { directoryValue: "", organizationalUnitId: "" }]); }
  return <div className="space-y-5">
    <Card><CardHeader><CardTitle>{m("mappings")}</CardTitle><CardDescription>{locale === "ar" ? "تُطبّق التحديثات الحقلية فقط بعد اختيار الإجراء في المعاينة." : "Field changes are applied only after an action is selected in the preview."}</CardDescription></CardHeader><CardContent className="space-y-3">
      <div className="space-y-3">{fieldDraft.map((item, index) => <div key={item.targetField} className="grid gap-2 rounded-md border p-3 md:grid-cols-[1fr_1fr_1.2fr_1fr]">
        <Field label={m("targetField")}><Input disabled value={fieldLabel(item.targetField)} /></Field>
        <Field label={m("sourceAttribute")}><select disabled={!canManage} className={textControl} value={item.sourceAttribute} onChange={(event) => updateField(index, { sourceAttribute: event.target.value })}>{attrs.map((attr) => <option key={attr} value={attr}>{attr}</option>)}</select></Field>
        <Field label={m("updatePolicy")}><select disabled={!canManage} className={textControl} value={item.updatePolicy} onChange={(event) => updateField(index, { updatePolicy: event.target.value as DirectoryFieldMappingDto["updatePolicy"] })}><option value="Ignore">{m("ignore")}</option><option value="Review">{m("review")}</option><option value="AutoUpdate">{m("autoUpdate")}</option></select></Field>
        <Field label={m("transform")}><select disabled={!canManage} className={textControl} value={item.transform ?? ""} onChange={(event) => updateField(index, { transform: event.target.value || null })}><option value="">{m("noTransform")}</option><option value="Trim">Trim</option><option value="Lowercase">Lowercase</option><option value="Uppercase">Uppercase</option></select></Field>
      </div>)}</div>
      {canManage ? <Button type="button" size="sm" onClick={onSaveFields} disabled={savingFields}>{savingFields ? t("common.saving") : m("saveFieldMappings")}</Button> : null}
    </CardContent></Card>
    <Card><CardHeader><CardTitle>{m("groupRoleMappings")}</CardTitle></CardHeader><CardContent className="space-y-3">
      {groupsError ? <p role="alert" className="text-sm text-destructive">{groupsError}</p> : null}
      {canManage ? <div className="space-y-2 rounded-md border p-3">
        <Field label={m("groupSearch")}><Input value={groupSearch} onChange={(event) => setGroupSearch(event.target.value)} placeholder={m("groupSearch")} /></Field>
        <div className="flex flex-wrap gap-2">{groups.filter((group) => !groupDraft.some((item) => item.externalGroupId === group.externalDirectoryId)).map((group) =>
          <Button key={group.externalDirectoryId} type="button" variant="outline" size="sm" onClick={() => addGroup(group)}><Plus className="me-1.5 h-3.5 w-3.5" />{group.name}</Button>)}</div>
        <div className="flex items-center justify-end gap-2">
          <Button type="button" variant="outline" size="sm" disabled={groupPage <= 1} onClick={onPreviousGroups}>{m("previousGroups")}</Button>
          <span className="text-xs text-muted-foreground">{groupPage}</span>
          <Button type="button" variant="outline" size="sm" disabled={!groupsHasMore} onClick={onNextGroups}>{m("nextGroups")}</Button>
        </div>
      </div> : null}
      {groupDraft.length === 0 ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : <div className="space-y-2">
        {groupDraft.map((mapping) => <div key={mapping.externalGroupId} className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <Field label={m("directoryGroup")}><Input disabled value={mapping.externalGroupName} /></Field>
          <Field label={m("nexusRole")}><select disabled={!canManage} className={textControl} value={mapping.roleId ?? ""} onChange={(event) => updateGroup(mapping.externalGroupId, { roleId: event.target.value || null })}><option value="">{m("noRole")}</option>{roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}</select></Field>
          <CheckField disabled={!canManage} checked={mapping.isActive} onChange={(checked) => updateGroup(mapping.externalGroupId, { isActive: checked })} label={m("enabled")} />
        </div>)}
      </div>}
      {canManage ? <Button type="button" size="sm" onClick={onSaveGroups} disabled={savingGroups}>{savingGroups ? t("common.saving") : m("saveGroupMappings")}</Button> : null}
    </CardContent></Card>
    <Card><CardHeader><CardTitle>{m("organizationMappings")}</CardTitle><CardDescription>{locale === "ar" ? "لن تُستبدل عضوية تنظيمية يدوية عند وجود تعارض." : "Existing manually managed organization assignments are preserved when they conflict."}</CardDescription></CardHeader><CardContent className="space-y-3">
      {organizationDraft.map((mapping, index) => <div key={`${mapping.directoryValue}-${index}`} className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <Field label={m("directoryDepartment")}><Input disabled={!canManage} value={mapping.directoryValue} onChange={(event) => setOrganizationDraft((current) => current.map((item, i) => i === index ? { ...item, directoryValue: event.target.value } : item))} /></Field>
        <Field label={m("nexusUnit")}><select disabled={!canManage} className={textControl} value={mapping.organizationalUnitId} onChange={(event) => setOrganizationDraft((current) => current.map((item, i) => i === index ? { ...item, organizationalUnitId: event.target.value } : item))}><option value="">—</option>{units.map((unit) => <option key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ` : ""}{unit.name}</option>)}</select></Field>
        {canManage ? <Button type="button" variant="outline" size="sm" onClick={() => setOrganizationDraft((current) => current.filter((_, i) => i !== index))}>{m("remove")}</Button> : null}
      </div>)}
      {canManage ? <div className="flex flex-wrap gap-2"><Button type="button" variant="outline" size="sm" onClick={addOrgMapping}><Plus className="me-2 h-4 w-4" />{m("addMapping")}</Button><Button type="button" size="sm" disabled={savingOrganizations} onClick={onSaveOrganizations}>{savingOrganizations ? t("common.saving") : m("saveOrganizationMappings")}</Button></div> : null}
    </CardContent></Card>
  </div>;
}

function ProvisioningPanel({ t, messages, draft, setDraft, canManage, units, calendars, saving, onSave }: {
  t: (key: string) => string; messages: string; draft: DirectoryProvisioningPolicyDto; setDraft: React.Dispatch<React.SetStateAction<DirectoryProvisioningPolicyDto>>;
  canManage: boolean; units: { id: string; name: string }[]; calendars: { id: string; name: string }[]; saving: boolean; onSave: () => void;
}) {
  const m = (key: string) => t(`${messages}.${key}`);
  const update = (values: Partial<DirectoryProvisioningPolicyDto>) => setDraft((current) => ({ ...current, ...values }));
  const missingOptions = [["Ignore", m("ignore")], ["Review", m("review")], ["AutoCreate", m("autoCreate")]];
  const disableOptions = [["DoNothing", m("doNothing")], ["Review", m("review")], ["AutoDeactivate", m("autoDeactivate")]];
  const groupChangedOptions = [["Ignore", m("ignore")], ["Review", m("review")], ["AutoUpdate", m("autoUpdateGroup")]];
  const PolicySelect = ({ label, value, options, set }: { label: string; value: string; options: string[][]; set: (value: string) => void }) => <Field label={label}><select disabled={!canManage} className={textControl} value={value} onChange={(event) => set(event.target.value)}>{options.map(([key, text]) => <option key={key} value={key}>{text}</option>)}</select></Field>;
  return <Card><CardHeader><CardTitle>{m("provisioning")}</CardTitle><CardDescription>{m("conservativeDefaults")}</CardDescription></CardHeader><CardContent className="space-y-4">
    <div className="grid gap-4 md:grid-cols-2">
      <PolicySelect label={m("missingUserAction")} value={draft.missingUserAction} options={missingOptions} set={(value) => update({ missingUserAction: value })} />
      <PolicySelect label={m("missingGroupAction")} value={draft.missingGroupAction} options={missingOptions} set={(value) => update({ missingGroupAction: value })} />
      <PolicySelect label={m("disabledUserAction")} value={draft.disabledUserAction} options={disableOptions} set={(value) => update({ disabledUserAction: value })} />
      <PolicySelect label={m("outOfScopeUserAction")} value={draft.outOfScopeUserAction} options={disableOptions} set={(value) => update({ outOfScopeUserAction: value })} />
      <PolicySelect label={m("changedGroupAction")} value={draft.changedGroupAction} options={groupChangedOptions} set={(value) => update({ changedGroupAction: value })} />
      <PolicySelect label={m("missingGroupReferenceAction")} value={draft.missingGroupReferenceAction} options={disableOptions} set={(value) => update({ missingGroupReferenceAction: value })} />
      <Field label={m("defaultUnit")}><select disabled={!canManage} className={textControl} value={draft.defaultOrganizationalUnitId ?? ""} onChange={(event) => update({ defaultOrganizationalUnitId: event.target.value || null })}><option value="">—</option>{units.map((unit) => <option key={unit.id} value={unit.id}>{unit.name}</option>)}</select></Field>
      <Field label={m("defaultCalendar")}><select disabled={!canManage} className={textControl} value={draft.defaultBaseCalendarId ?? ""} onChange={(event) => update({ defaultBaseCalendarId: event.target.value || null })}><option value="">—</option>{calendars.map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.name}</option>)}</select></Field>
      <Field label={m("defaultEmploymentType")}><Input disabled={!canManage} maxLength={100} value={draft.defaultEmploymentType ?? ""} onChange={(event) => update({ defaultEmploymentType: event.target.value })} /></Field>
      <Field label={m("defaultSeniority")}><Input disabled={!canManage} maxLength={100} value={draft.defaultSeniority ?? ""} onChange={(event) => update({ defaultSeniority: event.target.value })} /></Field>
    </div>
    <CheckField disabled={!canManage} checked={draft.createResource} onChange={(checked) => update({ createResource: checked })} label={m("createResource")} />
    {canManage ? <Button type="button" size="sm" onClick={onSave} disabled={saving}>{saving ? t("common.saving") : m("saveProvisioning")}</Button> : null}
  </CardContent></Card>;
}

function PreviewPanel({ t, messages, itemType, setItemType, search, setSearch, page, setPage, data, loading, error, canExecute, selectedRows, onToggle, onSetAction, onRefresh, onSync, syncing, selectedCount, syncMessage }: {
  t: (key: string, params?: Record<string, string | number>) => string; messages: string; itemType: "Users" | "Groups"; setItemType: (value: "Users" | "Groups") => void;
  search: string; setSearch: (value: string) => void; page: number; setPage: (value: number) => void;
  data?: { items: DirectoryPreviewItemDto[]; hasMore: boolean; providerStatus: { message?: string | null } }; loading: boolean; error: string; canExecute: boolean;
  selectedRows: Record<string, SelectedDirectorySyncItem>; onToggle: (item: DirectoryPreviewItemDto, checked: boolean) => void; onSetAction: (item: DirectoryPreviewItemDto, action: string) => void;
  onRefresh: () => void; onSync: () => void; syncing: boolean; selectedCount: number; syncMessage: string;
}) {
  const m = (key: string, params?: Record<string, string | number>) => t(`${messages}.${key}`, params);
  const visibleSelectable = data?.items.filter((item) => item.selectable) ?? [];
  const allSelected = visibleSelectable.length > 0 && visibleSelectable.every((item) => Boolean(selectedRows[itemKey(item)]));
  const actionChoices = (item: DirectoryPreviewItemDto) => {
    const base = item.itemType === "User"
      ? item.classification === "New" ? ["Create"] : item.classification === "Changed" ? ["Update"] : ["Deactivate"]
      : item.classification === "New" ? ["Create"] : item.classification === "Changed" ? ["Update"] : ["Deactivate"];
    return [...base, "Review", "Ignore"];
  };
  return <Card><CardHeader><CardTitle>{m("preview")}</CardTitle><CardDescription>{m("previewNote")}</CardDescription></CardHeader><CardContent className="space-y-4">
    <div className="flex flex-wrap items-center gap-2">
      <Button type="button" size="sm" variant={itemType === "Users" ? "default" : "outline"} onClick={() => setItemType("Users")}>{m("users")}</Button>
      <Button type="button" size="sm" variant={itemType === "Groups" ? "default" : "outline"} onClick={() => setItemType("Groups")}>{m("groups")}</Button>
      <div className="min-w-48 flex-1"><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={m("searchPreview")} aria-label={m("searchPreview")} /></div>
      <Button type="button" variant="outline" size="sm" onClick={onRefresh} disabled={loading}><RefreshCw className={`me-2 h-4 w-4 ${loading ? "animate-spin" : ""}`} />{m("refreshPreview")}</Button>
    </div>
    {data?.providerStatus.message ? <p className="text-xs text-muted-foreground">{data.providerStatus.message}</p> : null}
    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    {syncMessage ? <p role="status" className="text-sm text-muted-foreground">{syncMessage}</p> : null}
    <div className="flex flex-wrap items-center justify-between gap-2">
      {canExecute ? <div className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={!visibleSelectable.length} checked={allSelected} onChange={(event) => visibleSelectable.forEach((item) => onToggle(item, event.target.checked))} aria-label={m("selectVisible")} /><span>{m("selectVisible")}</span></div> : <span />}
      {canExecute ? <Button type="button" size="sm" disabled={!selectedCount || syncing} onClick={onSync}>{syncing ? t("common.saving") : m("executeSelected", { count: selectedCount })}</Button> : null}
    </div>
    <Table><TableHeader><TableRow><TableHead>{m("select")}</TableHead><TableHead>{m("matchedIdentity")}</TableHead><TableHead>{m("classification")}</TableHead><TableHead>{m("proposedAction")}</TableHead><TableHead>{m("reason")}</TableHead></TableRow></TableHeader>
      <TableBody>
        {data?.items.map((item) => {
          const key = itemKey(item);
          const selected = selectedRows[key];
          return <TableRow key={key}>
            <TableCell><input type="checkbox" aria-label={`${m("select")} ${item.displayName}`} disabled={!canExecute || (!item.selectable && !selected)} checked={Boolean(selected)} onChange={(event) => onToggle(item, event.target.checked)} /></TableCell>
            <TableCell><div className="font-medium">{item.displayName}</div>{item.matchedUserName ? <div className="text-xs text-muted-foreground">{item.matchedUserName}</div> : null}</TableCell>
            <TableCell><Badge status={item.classification}>{item.classification}</Badge></TableCell>
            <TableCell>{selected ? <select className={`${textControl} min-w-36`} value={selected.action} onChange={(event) => onSetAction(item, event.target.value)}>{actionChoices(item).map((action) => <option key={action} value={action}>{action}</option>)}</select> : item.proposedAction}</TableCell>
            <TableCell className="max-w-sm text-xs text-muted-foreground">{item.policyReason}</TableCell>
          </TableRow>;
        })}
        {(!data?.items.length && !loading) ? <TableRow><TableCell colSpan={5} className="py-10 text-center text-muted-foreground">{m("noMatches")}</TableCell></TableRow> : null}
        {loading && !data?.items.length ? <TableRow><TableCell colSpan={5} className="py-10 text-center text-muted-foreground">{t("common.loading")}</TableCell></TableRow> : null}
      </TableBody>
    </Table>
    <div className="flex items-center justify-end gap-2">
      <Button type="button" variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>{m("previous")}</Button>
      <span className="text-sm text-muted-foreground">{m("page", { page })}</span>
      <Button type="button" variant="outline" size="sm" disabled={!data?.hasMore || loading} onClick={() => setPage(page + 1)}>{m("next")}</Button>
    </div>
  </CardContent></Card>;
}

function HistoryPanel({ t, messages, runs, loading, error, onOpen, formatDate }: {
  t: (key: string) => string; messages: string; runs: { id: string; connectionName: string; startedAt: string; status: string; usersCreated: number; usersUpdated: number; usersDeactivated: number; groupsCreated: number; groupsUpdated: number; groupsDeactivated: number; failures: number }[];
  loading: boolean; error: string; onOpen: (id: string) => void; formatDate: (value: string) => string;
}) {
  const m = (key: string) => t(`${messages}.${key}`);
  return <Card><CardHeader><CardTitle>{m("history")}</CardTitle></CardHeader><CardContent>
    {error ? <p role="alert" className="mb-3 text-sm text-destructive">{error}</p> : null}
    <Table><TableHeader><TableRow><TableHead>{m("startedAt")}</TableHead><TableHead>{m("chooseConnection")}</TableHead><TableHead>{m("runStatus")}</TableHead><TableHead>{m("summary")}</TableHead><TableHead /></TableRow></TableHeader>
      <TableBody>{runs.map((run) => <TableRow key={run.id}><TableCell>{formatDate(run.startedAt)}</TableCell><TableCell>{run.connectionName}</TableCell><TableCell><Badge status={run.status}>{run.status}</Badge></TableCell><TableCell>{run.usersCreated + run.usersUpdated + run.usersDeactivated + run.groupsCreated + run.groupsUpdated + run.groupsDeactivated} · {run.failures}</TableCell><TableCell><Button type="button" variant="outline" size="sm" onClick={() => onOpen(run.id)}>{m("viewDetails")}</Button></TableCell></TableRow>)}
        {!runs.length && !loading ? <TableRow><TableCell colSpan={5} className="py-10 text-center text-muted-foreground">{m("noHistory")}</TableCell></TableRow> : null}
        {loading ? <TableRow><TableCell colSpan={5} className="py-10 text-center text-muted-foreground">{t("common.loading")}</TableCell></TableRow> : null}
      </TableBody>
    </Table>
  </CardContent></Card>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  const id = React.useId();
  return <div className="space-y-1.5"><Label htmlFor={id}>{label}</Label>{React.isValidElement<{ id?: string }>(children) ? React.cloneElement(children, { id }) : children}</div>;
}

function CheckField({ checked, onChange, label, disabled = false }: { checked: boolean; onChange: (checked: boolean) => void; label: string; disabled?: boolean }) {
  return <label className={`flex items-center gap-2 text-sm ${disabled ? "opacity-60" : ""}`}><input type="checkbox" disabled={disabled} checked={checked} onChange={(event) => onChange(event.target.checked)} />{label}</label>;
}
