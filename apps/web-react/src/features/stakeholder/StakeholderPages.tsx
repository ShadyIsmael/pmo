import * as React from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRight, CheckCircle2, ShieldCheck } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Badge } from "@components/ui/Badge";
import { useI18n } from "@i18n/messages";
import { api, ApiError } from "@api/client";
import { PageHeader } from "@components/layout/PageHeader";

interface InvitationValidation { resourceName: string; email: string; projectName: string; }

// Public route (no auth): /stakeholder/accept-invitation?token=...
export function AcceptInvitationPage() {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = React.useState("");
  const [confirmPassword, setConfirmPassword] = React.useState("");
  const validation = useQuery({
    queryKey: ["stakeholder-invitation", "validate", token],
    queryFn: () => api.get<InvitationValidation>(`/api/stakeholder-invitations/validate?token=${encodeURIComponent(token)}`),
    enabled: token.length > 0,
    retry: false,
  });
  const accept = useMutation({ mutationFn: () => api.post<void>("/api/stakeholder-invitations/accept", { token, password, confirmPassword }) });

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    await accept.mutateAsync();
  }

  if (accept.isSuccess) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <Card className="w-full max-w-lg"><CardContent className="space-y-5 p-6 text-center">
          <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" />
          <h1 className="text-xl font-semibold">{t("stakeholder.invitationAccepted")}</h1>
          <Button asChild><Link to="/login">{t("stakeholder.goToLogin")}<ArrowRight className="ms-2 h-4 w-4" /></Link></Button>
        </CardContent></Card>
      </main>
    );
  }
  if (!token || validation.isError) {
    const message = validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("expired")
      ? t("stakeholder.invitationExpired")
      : validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("revoked")
        ? t("stakeholder.invitationRevoked")
        : t("stakeholder.invitationInvalid");
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <Card className="w-full max-w-lg"><CardContent className="space-y-3 p-6 text-center">
          <ShieldCheck className="mx-auto h-10 w-10 text-muted-foreground" />
          <h1 className="text-xl font-semibold">{message}</h1>
        </CardContent></Card>
      </main>
    );
  }
  if (validation.isLoading || !validation.data) return <main className="flex min-h-screen items-center justify-center p-4"><p className="text-sm text-muted-foreground">{t("common.loading")}</p></main>;
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <div className="mb-2 flex items-center gap-2"><Badge status="stakeholder">{t("stakeholder.stakeholder")}</Badge></div>
          <CardTitle>{t("stakeholder.acceptInvitationTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("stakeholder.acceptInvitationDescription")}</p>
        </CardHeader>
        <CardContent>
          <form className="space-y-5" onSubmit={(e) => void submit(e)}>
            <section className="space-y-3 rounded-lg border bg-muted/20 p-4">
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationResource")}</p><p className="font-medium">{validation.data.resourceName}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationEmail")}</p><p className="font-medium">{validation.data.email}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationProject")}</p><p className="font-medium">{validation.data.projectName}</p></div>
            </section>
            <div className="space-y-2"><Label htmlFor="stakeholder-password">{t("stakeholder.setPassword")}</Label><Input id="stakeholder-password" type="password" minLength={6} required value={password} onChange={(e) => setPassword(e.target.value)} /></div>
            <div className="space-y-2"><Label htmlFor="stakeholder-confirm-password">{t("stakeholder.confirmPassword")}</Label><Input id="stakeholder-confirm-password" type="password" minLength={6} required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} /></div>
            {accept.error ? <p className="text-sm text-destructive" role="alert">{accept.error instanceof Error ? accept.error.message : t("stakeholder.invitationAcceptError")}</p> : null}
            <Button className="w-full" type="submit" disabled={accept.isPending}>{accept.isPending ? t("stakeholder.submitting") : t("stakeholder.activateAccount")}</Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}

export function StakeholderProjectsPage() {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: ["stakeholder", "projects"],
    queryFn: () => api.get<{ items: { id: string; name: string }[] }>("/api/stakeholder/projects", { pageSize: 50 }),
    staleTime: 30_000,
  });
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("stakeholder.title")} description={t("stakeholder.description")} />
      {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : items.length === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">{t("stakeholder.emptyTitle")}</p>
        : <ul className="space-y-2">{items.map((p) => <li key={p.id}><Link to={`/stakeholder/projects/${encodeURIComponent(p.id)}`} className="text-sm font-medium text-primary hover:underline">{p.name}</Link></li>)}</ul>}
    </div>
  );
}

export function useInvalidateStakeholder() {
  const client = useQueryClient();
  return () => { void client.invalidateQueries({ queryKey: ["stakeholder"] }); };
}
