import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useAdminUsers, useRoles, useCurrencies, useVendorCategories, useWorkItemStatuses, useProjectRoles, usePlatformSettings, useSavePlatformSettings } from "./api/admin";
import * as React from "react";

export function AccessPage() {
  const { t } = useI18n();
  const users = useAdminUsers(true);
  const roles = useRoles(true);
  const items = users.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.access.title")} description={t("administration.access.description")} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card><CardHeader><CardTitle>{t("nav.users")}</CardTitle></CardHeader><CardContent className="p-0">
          {users.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : <Table><TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.email")}</TableHead></TableRow></TableHeader><TableBody>{items.slice(0, 50).map((u) => <TableRow key={u.id}><TableCell className="font-medium">{u.fullName}</TableCell><TableCell className="text-muted-foreground">{u.email}</TableCell></TableRow>)}</TableBody></Table>}
        </CardContent></Card>
        <Card><CardHeader><CardTitle>{t("nav.roles")}</CardTitle></CardHeader><CardContent>
          {(roles.data ?? []).map((r) => <p key={r.id} className="border-b py-2 text-sm last:border-0"><strong>{r.name}</strong> <span className="text-muted-foreground">· {(r.permissions ?? []).length}</span></p>)}
        </CardContent></Card>
      </div>
    </div>
  );
}

export function ReferenceDataPage() {
  const { t, locale } = useI18n();
  const vendors = useVendorCategories(true);
  const statuses = useWorkItemStatuses(true);
  const roles = useProjectRoles(true);
  const Section = ({ title, items }: { title: string; items: { id: string; code: string; nameEn: string; nameAr: string }[] }) => (
    <Card><CardHeader><CardTitle>{title}</CardTitle></CardHeader><CardContent>
      {items.map((i) => <p key={i.id} className="border-b py-2 text-sm last:border-0"><span className="font-mono text-xs text-muted-foreground">{i.code}</span> <span className="ms-2">{locale === "ar" ? i.nameAr : i.nameEn}</span></p>)}
    </CardContent></Card>
  );
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.referenceData.title")} description={t("administration.referenceData.description")} />
      <div className="grid gap-4 lg:grid-cols-3">
        <Section title={t("nav.workItemStatuses")} items={statuses.data ?? []} />
        <Section title={t("nav.workItemStatuses") && t("resources.vendors.tabVendors")} items={vendors.data ?? []} />
        <Section title={t("nav.projectRoles")} items={roles.data ?? []} />
      </div>
    </div>
  );
}

export function PlatformSettingsPage() {
  const { t } = useI18n();
  const settings = usePlatformSettings(true);
  const save = useSavePlatformSettings();
  const [appName, setAppName] = React.useState("");
  React.useEffect(() => {
    if (settings.data?.["app.name"]) setAppName(settings.data["app.name"]);
  }, [settings.data]);
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.settings.title")} description={t("administration.settings.description")} />
      <Card><CardHeader><CardTitle>{t("administration.settings.general")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5"><Label>{t("administration.settings.appName")}</Label><Input value={appName} onChange={(e) => setAppName(e.target.value)} /></div>
          <Button disabled={save.isPending} onClick={() => save.mutate({ "app.name": appName })}>{t("common.save")}</Button>
        </CardContent>
      </Card>
    </div>
  );
}

export function IntegrationsPage() {
  const { t } = useI18n();
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.integrations.title")} description={t("administration.integrations.description")} />
      <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.integrations.empty")}</CardContent></Card>
    </div>
  );
}

export function GovernancePage() {
  const { t } = useI18n();
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.governance.title")} description={t("administration.governance.description")} />
      <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.governance.empty")}</CardContent></Card>
    </div>
  );
}

export function CurrenciesPage() {
  const { t, locale } = useI18n();
  const query = useCurrencies(true);
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.currencies.title")} description={t("administration.currencies.description")} />
      <Card><CardContent className="p-0">
        <Table><TableHeader><TableRow><TableHead>{t("common.code")}</TableHead><TableHead>{t("common.name")}</TableHead></TableRow></TableHeader>
          <TableBody>{(query.data ?? []).map((c) => <TableRow key={c.id}><TableCell className="font-mono">{c.code}</TableCell><TableCell>{locale === "ar" ? c.nameAr : c.nameEn}</TableCell></TableRow>)}</TableBody></Table>
      </CardContent></Card>
    </div>
  );
}
