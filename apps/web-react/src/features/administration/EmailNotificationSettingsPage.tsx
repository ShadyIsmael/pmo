import * as React from "react";
import { Navigate } from "react-router-dom";
import { Mail, RotateCcw, Save, Send, ShieldCheck, TestTube2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { ErrorState } from "@components/ui/StateDisplay";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useOrganizationProfiles } from "./api/settings";
import {
  useCheckTrustedRelay,
  useEmailNotificationDeliveryLogs,
  useEmailNotificationSettings,
  useEmailNotificationSettingsAccess,
  useResetEmailNotificationSettings,
  useSendEmailNotificationTest,
  useTestEmailNotificationConnection,
  useUpdateEmailNotificationSettings,
  type EmailNotificationDeliveryLog,
  type EmailNotificationProfileView,
  type EmailNotificationScope,
  type SmtpAuthenticationMode,
  type SmtpSecurityMode,
  type UpdateEmailNotificationProfile,
} from "./api/emailNotifications";

type ProfileDraft = {
  enabled: boolean;
  host: string;
  port: number;
  securityMode: SmtpSecurityMode;
  authenticationMode: Exclude<SmtpAuthenticationMode, "OAuth2">;
  userName: string;
  fromAddress: string;
  fromName: string;
  replyToAddress: string;
};

type Feedback = { kind: "success" | "error"; message: string };

export function EmailNotificationSettingsPage() {
  const { t } = useI18n();
  const access = useEmailNotificationSettingsAccess();
  const [scope, setScope] = React.useState<EmailNotificationScope>("Tenant");
  const [organizationId, setOrganizationId] = React.useState("");
  const canReadTenant = access.data?.canReadTenant === true;
  const organizations = useOrganizationProfiles(canReadTenant, true);

  const availableScopes = React.useMemo(() => {
    const scopes: EmailNotificationScope[] = [];
    if (access.data?.canReadPlatform) scopes.push("Platform");
    if (canReadTenant) scopes.push("Tenant");
    if (canReadTenant && (organizations.data?.length ?? 0) > 0) scopes.push("Organization");
    return scopes;
  }, [access.data?.canReadPlatform, canReadTenant, organizations.data]);

  React.useEffect(() => {
    if (availableScopes.length && !availableScopes.includes(scope)) setScope(availableScopes[0]);
  }, [availableScopes, scope]);

  React.useEffect(() => {
    const items = organizations.data ?? [];
    if (!items.length) setOrganizationId("");
    else if (!organizationId || !items.some(item => item.id === organizationId)) setOrganizationId(items[0].id);
  }, [organizations.data, organizationId]);

  if (access.isLoading) return <p className="py-10 text-center text-sm text-muted-foreground" role="status">{t("administration.emailNotifications.loading")}</p>;
  if (access.isError || !access.data) return <ErrorState title={t("administration.emailNotifications.loadError")} onRetry={() => void access.refetch()} />;
  if (!access.data.canReadTenant && !access.data.canReadPlatform) return <Navigate to="/administration" replace />;
  if (scope === "Organization" && organizations.isLoading) return <p className="py-10 text-center text-sm text-muted-foreground" role="status">{t("administration.emailNotifications.loading")}</p>;
  if (scope === "Organization" && organizations.isError) return <ErrorState title={t("administration.emailNotifications.loadError")} onRetry={() => void organizations.refetch()} />;

  const hasOrganization = Boolean(organizationId);
  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      <PageHeader title={t("administration.emailNotifications.title")} description={t("administration.emailNotifications.description")} />
      <Card>
        <CardContent className="space-y-4 pt-6">
          {availableScopes.length > 1 ? (
            <div role="tablist" aria-label={t("administration.emailNotifications.title")} className="flex flex-wrap gap-2">
              {availableScopes.map(item => (
                <button key={item} type="button" role="tab" aria-selected={scope === item} onClick={() => setScope(item)}
                  className={`rounded-lg border px-4 py-2 text-sm font-medium transition-colors ${scope === item ? "border-primary bg-primary/10 text-primary" : "border-border bg-background hover:bg-muted"}`}>
                  {scopeLabel(item, t)}
                </button>
              ))}
            </div>
          ) : <span className="inline-flex rounded-lg border bg-muted/40 px-3 py-2 text-sm font-medium">{scopeLabel(scope, t)}</span>}
          {scope === "Organization" ? (
            organizations.data?.length ? (
              <div className="max-w-xl space-y-2">
                <Label htmlFor="email-settings-organization">{t("administration.emailNotifications.organization")}</Label>
                <select id="email-settings-organization" value={organizationId} onChange={event => setOrganizationId(event.target.value)}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
                  {organizations.data.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </div>
            ) : <p className="text-sm text-muted-foreground">{t("administration.emailNotifications.noOrganizations")}</p>
          ) : null}
          <p className="max-w-4xl text-sm leading-6 text-muted-foreground">{t("administration.emailNotifications.inheritanceDescription")}</p>
        </CardContent>
      </Card>
      {scope === "Organization" && !hasOrganization ? null : (
        <EmailNotificationProfileEditor key={`${scope}:${organizationId}`} scope={scope} organizationId={scope === "Organization" ? organizationId : undefined} />
      )}
    </div>
  );
}

function EmailNotificationProfileEditor({ scope, organizationId }: { scope: EmailNotificationScope; organizationId?: string }) {
  const { t, locale, formatDateTime } = useI18n();
  const settings = useEmailNotificationSettings(scope, organizationId);
  const update = useUpdateEmailNotificationSettings(scope, organizationId);
  const reset = useResetEmailNotificationSettings(scope, organizationId);
  const testConnection = useTestEmailNotificationConnection(scope, organizationId);
  const sendTest = useSendEmailNotificationTest(scope, organizationId);
  const trustedRelayCheck = useCheckTrustedRelay(scope, organizationId);
  const [draft, setDraft] = React.useState<ProfileDraft>(() => emptyDraft());
  const [password, setPassword] = React.useState("");
  const [clearPassword, setClearPassword] = React.useState(false);
  const [feedback, setFeedback] = React.useState<Feedback | null>(null);
  const [testFeedback, setTestFeedback] = React.useState<Feedback | null>(null);
  const [relayAuthorization, setRelayAuthorization] = React.useState<{ host: string; allowed: boolean } | null>(null);

  const visibleProfile = settings.data?.overrideProfile ?? settings.data?.effectiveProfile ?? null;
  const hasExactOverride = settings.data?.hasOverride === true;
  const canManage = settings.data?.canManage === true && settings.data.persistenceAvailable;
  const canTest = settings.data?.canTest === true;
  const trustedRelayForCurrentHost = relayAuthorization?.host === normalizedHost(draft.host)
    ? relayAuthorization.allowed
    : (settings.data?.trustedRelayAvailableForEffectiveHost === true
      && normalizedHost(visibleProfile?.host ?? "") === normalizedHost(draft.host));
  const savedDraft = React.useMemo(() => draftFromProfile(visibleProfile), [visibleProfile]);
  const dirty = !sameDraft(draft, savedDraft) || password.length > 0 || clearPassword;
  const busy = update.isPending || reset.isPending || testConnection.isPending || sendTest.isPending || trustedRelayCheck.isPending;
  const canUseTest = canTest && !dirty && Boolean(settings.data?.effectiveProfile?.enabled);

  React.useEffect(() => {
    setDraft(draftFromProfile(visibleProfile));
    setPassword("");
    setClearPassword(false);
    setFeedback(null);
    setTestFeedback(null);
    setRelayAuthorization(null);
  }, [scope, organizationId, visibleProfile, settings.data?.hasOverride]);

  const setField = <K extends keyof ProfileDraft>(key: K, value: ProfileDraft[K]) => {
    setDraft(current => ({ ...current, [key]: value }));
    setFeedback(null);
    if (key === "host") setRelayAuthorization(null);
  };

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    setFeedback(null);
    const request: UpdateEmailNotificationProfile = {
      ...draft,
      userName: draft.authenticationMode === "TrustedRelay" ? null : nullable(draft.userName),
      password: draft.authenticationMode === "TrustedRelay" || !password ? null : password,
      clearPassword: draft.authenticationMode === "TrustedRelay" || (clearPassword && !password),
      replyToAddress: nullable(draft.replyToAddress),
      expectedVersion: settings.data?.overrideProfile?.version ?? null,
    };
    try {
      await update.mutateAsync(request);
      setPassword("");
      setClearPassword(false);
      setFeedback({ kind: "success", message: t("administration.emailNotifications.saved") });
    } catch {
      setFeedback({ kind: "error", message: t("administration.emailNotifications.saveFailed") });
    }
  };

  const discard = () => {
    setDraft(draftFromProfile(visibleProfile));
    setPassword("");
    setClearPassword(false);
    setFeedback(null);
  };

  const removeOverride = async () => {
    const version = settings.data?.overrideProfile?.version;
    if (!version || !window.confirm(t("administration.emailNotifications.resetConfirm"))) return;
    setFeedback(null);
    try {
      await reset.mutateAsync(version);
      setFeedback({ kind: "success", message: t("administration.emailNotifications.resetOverride") });
    } catch {
      setFeedback({ kind: "error", message: t("administration.emailNotifications.resetFailed") });
    }
  };

  const runConnectionTest = async () => {
    setTestFeedback(null);
    try {
      const result = await testConnection.mutateAsync();
      setTestFeedback({ kind: result.succeeded ? "success" : "error", message: t(result.succeeded
        ? "administration.emailNotifications.testConnectionSuccess"
        : "administration.emailNotifications.testConnectionFailed") });
    } catch {
      setTestFeedback({ kind: "error", message: t("administration.emailNotifications.testConnectionFailed") });
    }
  };

  const runSendTest = async () => {
    setTestFeedback(null);
    try {
      const result = await sendTest.mutateAsync();
      setTestFeedback({ kind: result.accepted ? "success" : "error", message: t(result.accepted
        ? "administration.emailNotifications.testEmailAccepted"
        : "administration.emailNotifications.testEmailFailed") });
    } catch {
      setTestFeedback({ kind: "error", message: t("administration.emailNotifications.testEmailFailed") });
    }
  };

  const authorizeRelay = async () => {
    setRelayAuthorization(null);
    try {
      const result = await trustedRelayCheck.mutateAsync(draft.host);
      setRelayAuthorization({ host: normalizedHost(draft.host), allowed: result.isAuthorized });
      setFeedback({ kind: result.isAuthorized ? "success" : "error", message: t(result.isAuthorized
        ? "administration.emailNotifications.trustedRelayAllowed"
        : "administration.emailNotifications.trustedRelayNotAllowed") });
    } catch {
      setRelayAuthorization({ host: normalizedHost(draft.host), allowed: false });
      setFeedback({ kind: "error", message: t("administration.emailNotifications.trustedRelayNotAllowed") });
    }
  };

  const logScope = scope === "Platform" ? "Tenant" : scope;
  const scopedLogs = useEmailNotificationDeliveryLogs(logScope, organizationId, settings.data?.canViewLogs === true);

  if (settings.isLoading) return <p className="py-10 text-center text-sm text-muted-foreground" role="status">{t("administration.emailNotifications.loading")}</p>;
  if (settings.isError || !settings.data) return <ErrorState title={t("administration.emailNotifications.loadError")} onRetry={() => void settings.refetch()} />;

  const hasPassword = visibleProfile?.hasPassword === true;
  const testButtonDisabled = !canUseTest || busy;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Mail className="h-5 w-5 text-primary" />{t("administration.emailNotifications.title")}</CardTitle>
          <CardDescription>{t("administration.emailNotifications.smtpCapabilityNote")}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {!settings.data.hasOverride && visibleProfile ? (
            <div className="rounded-lg border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
              <div className="font-medium text-foreground">{t("administration.emailNotifications.inheritedProfile")}</div>
              <div className="mt-1">{t("administration.emailNotifications.effectiveSource")}: {sourceLabel(settings.data.effectiveSourceScope, t)}</div>
            </div>
          ) : null}
          {!visibleProfile ? <p className="rounded-lg border border-dashed px-4 py-3 text-sm text-muted-foreground">{t("administration.emailNotifications.sourceNone")}</p> : null}
          {!canManage ? <p className="rounded-lg border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">{t("administration.emailNotifications.readOnly")}</p> : null}
          <form className="space-y-6" onSubmit={event => void handleSave(event)}>
            <label className="flex items-start gap-3 rounded-lg border p-4">
              <input type="checkbox" className="mt-1 h-4 w-4" checked={draft.enabled} disabled={!canManage || busy}
                onChange={event => setField("enabled", event.target.checked)} />
              <span><span className="block text-sm font-medium">{t("administration.emailNotifications.enabled")}</span>
                <span className="mt-1 block text-xs text-muted-foreground">{t("administration.emailNotifications.secretAtRest")}</span></span>
            </label>

            <div className="grid gap-4 md:grid-cols-2">
              <Field id="email-smtp-host" label={t("administration.emailNotifications.host")}>
                <Input id="email-smtp-host" value={draft.host} autoComplete="url" required maxLength={253} disabled={!canManage || busy}
                  onChange={event => setField("host", event.target.value)} />
              </Field>
              <Field id="email-smtp-port" label={t("administration.emailNotifications.port")}>
                <Input id="email-smtp-port" type="number" min={1} max={65535} required value={draft.port} disabled={!canManage || busy}
                  onChange={event => setField("port", Number(event.target.value))} />
              </Field>
              <Field id="email-smtp-security" label={t("administration.emailNotifications.securityMode")}>
                <select id="email-smtp-security" value={draft.securityMode} disabled={!canManage || busy}
                  onChange={event => setField("securityMode", event.target.value as SmtpSecurityMode)} className={selectClass}>
                  <option value="StartTlsRequired">{t("administration.emailNotifications.startTls")}</option>
                  <option value="ImplicitTls">{t("administration.emailNotifications.implicitTls")}</option>
                </select>
              </Field>
              <Field id="email-smtp-auth" label={t("administration.emailNotifications.authenticationMode")}>
                <select id="email-smtp-auth" value={draft.authenticationMode} disabled={!canManage || busy}
                  onChange={event => {
                    const mode = event.target.value as Exclude<SmtpAuthenticationMode, "OAuth2">;
                    setField("authenticationMode", mode);
                    if (mode === "TrustedRelay") { setPassword(""); setClearPassword(true); }
                    else setClearPassword(false);
                  }} className={selectClass}>
                  <option value="UsernamePassword">{t("administration.emailNotifications.usernamePassword")}</option>
                  <option value="TrustedRelay" disabled={!trustedRelayForCurrentHost && draft.authenticationMode !== "TrustedRelay"}>{t("administration.emailNotifications.trustedRelay")}</option>
                </select>
              </Field>
            </div>

            {draft.authenticationMode === "TrustedRelay" ? (
              <div className="space-y-3 rounded-lg border bg-muted/30 p-4">
                <p className="text-sm text-muted-foreground">{t("administration.emailNotifications.trustedRelayDescription")}</p>
                {canManage ? <Button type="button" variant="outline" size="sm" disabled={!draft.host.trim() || trustedRelayCheck.isPending}
                  onClick={() => void authorizeRelay()}><ShieldCheck className="me-2 h-4 w-4" />{trustedRelayCheck.isPending ? t("administration.emailNotifications.checkingRelay") : t("administration.emailNotifications.checkTrustedRelay")}</Button> : null}
              </div>
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                <Field id="email-smtp-username" label={t("administration.emailNotifications.username")}>
                  <Input id="email-smtp-username" autoComplete="username" value={draft.userName} maxLength={512} disabled={!canManage || busy}
                    onChange={event => setField("userName", event.target.value)} />
                </Field>
                <Field id="email-smtp-password" label={t("administration.emailNotifications.password")}>
                  <Input id="email-smtp-password" type="password" autoComplete="new-password" value={password} maxLength={4096}
                    placeholder={hasPassword ? t("administration.emailNotifications.passwordPlaceholder") : undefined}
                    disabled={!canManage || busy} onChange={event => { setPassword(event.target.value); setClearPassword(false); }} />
                  {hasPassword ? <p className="text-xs text-muted-foreground">{t("administration.emailNotifications.passwordSaved")}</p> : null}
                  {canManage && hasPassword && !draft.enabled ? <label className="flex items-center gap-2 text-xs text-muted-foreground">
                    <input type="checkbox" checked={clearPassword} disabled={busy} onChange={event => setClearPassword(event.target.checked)} />
                    {t("administration.emailNotifications.clearPassword")}
                  </label> : null}
                </Field>
              </div>
            )}

            <div className="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">{t("administration.emailNotifications.oauthUnavailable")}</div>

            <div className="grid gap-4 md:grid-cols-3">
              <Field id="email-sender-address" label={t("administration.emailNotifications.senderAddress")}>
                <Input id="email-sender-address" type="email" autoComplete="email" required maxLength={320} value={draft.fromAddress} disabled={!canManage || busy}
                  onChange={event => setField("fromAddress", event.target.value)} />
              </Field>
              <Field id="email-sender-name" label={t("administration.emailNotifications.senderName")}>
                <Input id="email-sender-name" maxLength={200} value={draft.fromName} disabled={!canManage || busy}
                  onChange={event => setField("fromName", event.target.value)} />
              </Field>
              <Field id="email-reply-to" label={t("administration.emailNotifications.replyToAddress")}>
                <Input id="email-reply-to" type="email" maxLength={320} value={draft.replyToAddress} disabled={!canManage || busy}
                  onChange={event => setField("replyToAddress", event.target.value)} />
              </Field>
            </div>

            {feedback ? <p role={feedback.kind === "error" ? "alert" : "status"} className={feedbackClass(feedback.kind)}>{feedback.message}</p> : null}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={!canManage || !dirty || busy}>
                  <Save className="me-2 h-4 w-4" />{update.isPending ? t("administration.emailNotifications.saving") : t("administration.emailNotifications.save")}
                </Button>
                <Button type="button" variant="outline" disabled={!dirty || busy} onClick={discard}>{t("administration.emailNotifications.cancel")}</Button>
                {canManage && hasExactOverride ? <Button type="button" variant="outline" disabled={busy} onClick={() => void removeOverride()}>
                  <RotateCcw className="me-2 h-4 w-4" />{t("administration.emailNotifications.resetOverride")}
                </Button> : null}
              </div>
              {hasExactOverride ? <span className="text-xs text-muted-foreground">{t("administration.emailNotifications.effectiveSource")}: {sourceLabel(settings.data.effectiveSourceScope, t)}</span> : null}
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("administration.emailNotifications.testConnection")}</CardTitle>
          <CardDescription>{t("administration.emailNotifications.testRecipientRule")}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">{t("administration.emailNotifications.testRequiresSave")}</p>
          {testFeedback ? <p role={testFeedback.kind === "error" ? "alert" : "status"} className={feedbackClass(testFeedback.kind)}>{testFeedback.message}</p> : null}
          {!canTest ? <p className="text-sm text-muted-foreground">{t("administration.emailNotifications.readOnly")}</p> : null}
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" disabled={testButtonDisabled} onClick={() => void runConnectionTest()}>
              <TestTube2 className="me-2 h-4 w-4" />{testConnection.isPending ? t("administration.emailNotifications.testing") : t("administration.emailNotifications.testConnection")}
            </Button>
            <Button type="button" disabled={testButtonDisabled} onClick={() => void runSendTest()}>
              <Send className="me-2 h-4 w-4" />{sendTest.isPending ? t("administration.emailNotifications.testing") : t("administration.emailNotifications.sendTestEmail")}
            </Button>
          </div>
        </CardContent>
      </Card>

      {settings.data.canViewLogs && scope !== "Platform" ? (
        <EmailDeliveryLogs logs={scopedLogs.data ?? []} isLoading={scopedLogs.isLoading} isError={scopedLogs.isError} retry={() => void scopedLogs.refetch()} locale={locale} formatDateTime={formatDateTime} t={t} />
      ) : null}
    </div>
  );
}

function EmailDeliveryLogs({
  logs,
  isLoading,
  isError,
  retry,
  locale,
  formatDateTime,
  t,
}: {
  logs: EmailNotificationDeliveryLog[];
  isLoading: boolean;
  isError: boolean;
  retry: () => void;
  locale: "en" | "ar";
  formatDateTime: (value: Date | string) => string;
  t: (key: string) => string;
}) {
  return <Card>
    <CardHeader>
      <CardTitle>{t("administration.emailNotifications.logsTitle")}</CardTitle>
      <CardDescription>{t("administration.emailNotifications.logsDescription")}</CardDescription>
    </CardHeader>
    <CardContent>
      {isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("administration.emailNotifications.loading")}</p>
        : isError ? <ErrorState title={t("administration.emailNotifications.logsLoadFailed")} onRetry={retry} />
        : logs.length === 0 ? <p className="text-sm text-muted-foreground">{t("administration.emailNotifications.logsEmpty")}</p>
        : <div className="overflow-x-auto"><table className="w-full min-w-[680px] text-sm">
          <thead><tr className="border-b text-start text-muted-foreground">
            <th className="py-2 font-medium">{t("administration.emailNotifications.time")}</th>
            <th className="px-3 py-2 text-start font-medium">{t("administration.emailNotifications.event")}</th>
            <th className="px-3 py-2 text-start font-medium">{t("administration.emailNotifications.status")}</th>
            <th className="px-3 py-2 text-end font-medium">{t("administration.emailNotifications.attempts")}</th>
            <th className="px-3 py-2 text-start font-medium">{t("administration.emailNotifications.failure")}</th>
          </tr></thead>
          <tbody>{logs.map(log => <tr key={log.id} className="border-b last:border-0">
            <td className="py-3 text-muted-foreground">{formatDateTime(log.createdAt)}</td>
            <td className="px-3 py-3">{eventLabel(log, t)}</td>
            <td className="px-3 py-3">{statusLabel(log.status, t)}</td>
            <td className="px-3 py-3 text-end tabular-nums">{new Intl.NumberFormat(locale).format(log.attemptCount)}</td>
            <td className="px-3 py-3 text-muted-foreground">{failureLabel(log.failureCategory, t)}</td>
          </tr>)}</tbody>
        </table></div>}
    </CardContent>
  </Card>;
}

function Field({ id, label, children }: { id: string; label: string; children: React.ReactNode }) {
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{children}</div>;
}

function emptyDraft(): ProfileDraft {
  return { enabled: false, host: "", port: 587, securityMode: "StartTlsRequired", authenticationMode: "UsernamePassword", userName: "", fromAddress: "", fromName: "", replyToAddress: "" };
}

function draftFromProfile(profile: EmailNotificationProfileView | null | undefined): ProfileDraft {
  if (!profile) return emptyDraft();
  return {
    enabled: profile.enabled,
    host: profile.host,
    port: profile.port,
    securityMode: profile.securityMode,
    authenticationMode: profile.authenticationMode === "TrustedRelay" ? "TrustedRelay" : "UsernamePassword",
    userName: profile.userName ?? "",
    fromAddress: profile.fromAddress,
    fromName: profile.fromName,
    replyToAddress: profile.replyToAddress ?? "",
  };
}

function sameDraft(left: ProfileDraft, right: ProfileDraft): boolean {
  return left.enabled === right.enabled && left.host === right.host && left.port === right.port
    && left.securityMode === right.securityMode && left.authenticationMode === right.authenticationMode
    && left.userName === right.userName && left.fromAddress === right.fromAddress
    && left.fromName === right.fromName && left.replyToAddress === right.replyToAddress;
}

function nullable(value: string): string | null { return value.trim() || null; }
function normalizedHost(value: string): string { return value.trim().replace(/\.$/, "").toLowerCase(); }
const selectClass = "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50";

function feedbackClass(kind: Feedback["kind"]): string {
  return kind === "error" ? "text-sm text-destructive" : "text-sm text-emerald-700 dark:text-emerald-400";
}

function scopeLabel(scope: EmailNotificationScope, t: (key: string) => string): string {
  return scope === "Platform" ? t("administration.emailNotifications.scopePlatform")
    : scope === "Tenant" ? t("administration.emailNotifications.scopeTenant")
    : t("administration.emailNotifications.scopeOrganization");
}

function sourceLabel(scope: EmailNotificationScope | null, t: (key: string) => string): string {
  return scope === "Platform" ? t("administration.emailNotifications.sourcePlatform")
    : scope === "Tenant" ? t("administration.emailNotifications.sourceTenant")
    : scope === "Organization" ? t("administration.emailNotifications.sourceOrganization")
    : t("administration.emailNotifications.sourceNone");
}

function statusLabel(status: string, t: (key: string) => string): string {
  const key = status.toLowerCase();
  if (key === "pending") return t("administration.emailNotifications.statusPending");
  if (key === "processing") return t("administration.emailNotifications.statusProcessing");
  if (key === "accepted") return t("administration.emailNotifications.statusAccepted");
  if (key === "succeeded") return t("administration.emailNotifications.statusSucceeded");
  if (key === "retrying" || key === "retryscheduled") return t("administration.emailNotifications.statusRetrying");
  if (key === "failed" || key === "failedpermanent") return t("administration.emailNotifications.statusFailed");
  if (key === "cancelled") return t("administration.emailNotifications.statusCancelled");
  return t("administration.emailNotifications.unknownStatus");
}

function eventLabel(log: EmailNotificationDeliveryLog, t: (key: string) => string): string {
  if (log.isTest) return t(log.eventType === "ConnectionTest"
    ? "administration.emailNotifications.eventTestConnection"
    : log.eventType === "TestEmail" ? "administration.emailNotifications.eventTestEmail" : "administration.emailNotifications.testEvent");
  const keys: Record<string, string> = {
    ProjectHandover: "eventProjectHandover", ProgramHandover: "eventProgramHandover", PortfolioHandover: "eventPortfolioHandover",
    ProjectChangeRequest: "eventProjectChangeRequest", PaymentDue: "eventPaymentDue", PaymentOverdue: "eventPaymentOverdue",
    MeetingReminder: "eventMeetingReminder", MeetingInvitation: "eventMeetingInvitation", MeetingStarted: "eventMeetingStarted",
    MeetingMinutesPublished: "eventMeetingMinutesPublished", MeetingMinutesComment: "eventMeetingMinutesComment",
    MeetingMinutesCommentReply: "eventMeetingMinutesCommentReply", MeetingMinutesShared: "eventMeetingMinutesShared",
    MeetingCompleted: "eventMeetingCompleted", RiskActionFollowUpDue: "eventRiskActionFollowUpDue",
  };
  const key = keys[log.eventType];
  return key ? t(`administration.emailNotifications.${key}`) : t("administration.emailNotifications.unknownEvent");
}

function failureLabel(code: string | null, t: (key: string) => string): string {
  if (!code) return "—";
  const labels: Record<string, string> = {
    Timeout: "timeout", ProviderUnavailable: "providerUnavailable", ProviderRejected: "providerRejected",
    AuthenticationFailed: "authenticationFailed", TlsValidationFailed: "tlsValidationFailed",
    ProtocolFailure: "protocolFailure", TlsRequired: "tlsRequired", ProviderError: "providerError",
    EmailProfileUnavailable: "emailProfileUnavailable", EmailUnavailable: "emailUnavailable",
    InvalidRecipient: "invalidRecipient", InvalidMessage: "invalidMessage", InvalidScope: "invalidScope",
    TestFailed: "testFailed",
  };
  const key = labels[code];
  return key ? t(`administration.emailNotifications.failure${key[0].toUpperCase()}${key.slice(1)}`)
    : t("administration.emailNotifications.unknownFailure");
}

