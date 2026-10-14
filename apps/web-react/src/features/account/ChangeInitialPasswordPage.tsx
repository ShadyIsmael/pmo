import * as React from "react";
import { useNavigate } from "react-router-dom";
import { KeyRound, LogOut } from "lucide-react";
import { api, ApiError } from "@api/client";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";

export function ChangeInitialPasswordPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const [policy, setPolicy] = React.useState<{ minimumLength: number; maximumLength: number } | null>(null);
  const [policyError, setPolicyError] = React.useState(false);
  const [currentPassword, setCurrentPassword] = React.useState("");
  const [newPassword, setNewPassword] = React.useState("");
  const [confirmPassword, setConfirmPassword] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    api.getInitialPasswordPolicy()
      .then((value) => { if (!cancelled) { setPolicy(value); setPolicyError(false); } })
      .catch(() => { if (!cancelled) setPolicyError(true); });
    return () => { cancelled = true; };
  }, []);

  const expired = user?.temporaryPasswordExpiresAt
    ? new Date(user.temporaryPasswordExpiresAt).getTime() <= Date.now()
    : true;

  async function handleLogout() {
    await logout();
    navigate("/login", { replace: true });
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!policy) return;
    if (newPassword !== confirmPassword) {
      setError(t("account.initialPassword.confirmMismatch"));
      return;
    }
    if (newPassword.length < policy.minimumLength || newPassword.length > policy.maximumLength) {
      setError(t("account.initialPassword.lengthError", { minimum: policy.minimumLength, maximum: policy.maximumLength }));
      return;
    }

    setSaving(true);
    try {
      await api.changeInitialPassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      await logout();
      navigate("/login", { replace: true, state: { passwordChanged: true } });
    } catch (cause) {
      const code = cause instanceof ApiError ? cause.code : undefined;
      setError(code === "TEMPORARY_PASSWORD_EXPIRED"
        ? t("account.initialPassword.expired")
        : code === "UNAUTHORIZED"
          ? t("account.initialPassword.currentPasswordInvalid")
          : t("account.initialPassword.changeError"));
    } finally {
      setSaving(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-4 py-10">
      <Card className="w-full max-w-xl shadow-elevated">
        <CardHeader>
          <div className="mb-2 flex h-11 w-11 items-center justify-center rounded-xl bg-warning/10 text-warning"><KeyRound className="h-5 w-5" /></div>
          <CardTitle>{t("account.initialPassword.title")}</CardTitle>
          <CardDescription>{t("account.initialPassword.description")}</CardDescription>
        </CardHeader>
        <CardContent>
          {expired ? <div className="space-y-4">
            <p role="alert" className="text-sm text-warning">{t("account.initialPassword.expired")}</p>
            <Button type="button" onClick={() => void handleLogout()}><LogOut className="me-2 h-4 w-4" />{t("auth.signOut")}</Button>
          </div> : policyError ? <div className="space-y-4">
            <p role="alert" className="text-sm text-destructive">{t("account.initialPassword.policyLoadError")}</p>
            <Button type="button" variant="outline" onClick={() => { setPolicyError(false); void api.getInitialPasswordPolicy().then(setPolicy).catch(() => setPolicyError(true)); }}>{t("common.retry")}</Button>
          </div> : !policy ? <p className="text-sm text-muted-foreground" role="status">{t("common.loading")}</p> : <form className="space-y-5" onSubmit={(event) => void handleSubmit(event)}>
            <p className="text-sm text-warning">{t("account.initialPassword.expiryNotice", { expiresAt: user?.temporaryPasswordExpiresAt ? new Date(user.temporaryPasswordExpiresAt).toLocaleString() : "" })}</p>
            <div className="space-y-2"><Label htmlFor="initial-current-password">{t("account.initialPassword.currentPassword")}</Label><Input id="initial-current-password" type="password" autoComplete="current-password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} disabled={saving} required /></div>
            <div className="space-y-2"><Label htmlFor="initial-new-password">{t("account.initialPassword.newPassword")}</Label><Input id="initial-new-password" type="password" autoComplete="new-password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} disabled={saving} required minLength={policy.minimumLength} maxLength={policy.maximumLength} aria-describedby="initial-password-policy" /><p id="initial-password-policy" className="text-xs text-muted-foreground">{t("account.initialPassword.lengthError", { minimum: policy.minimumLength, maximum: policy.maximumLength })}</p></div>
            <div className="space-y-2"><Label htmlFor="initial-confirm-password">{t("account.initialPassword.confirmPassword")}</Label><Input id="initial-confirm-password" type="password" autoComplete="new-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} disabled={saving} required minLength={policy.minimumLength} maxLength={policy.maximumLength} /></div>
            {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
            <div className="flex flex-wrap gap-2">
              <Button type="submit" disabled={saving || !currentPassword || !newPassword || !confirmPassword}>{saving ? t("account.initialPassword.saving") : t("account.initialPassword.changeAction")}</Button>
              <Button type="button" variant="outline" disabled={saving} onClick={() => void handleLogout()}><LogOut className="me-2 h-4 w-4" />{t("auth.signOut")}</Button>
            </div>
          </form>}
        </CardContent>
      </Card>
    </main>
  );
}
