import * as React from "react";
import { ArrowRight, LockKeyhole } from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { useI18n } from "../../i18n/messages";

export function LoginPage() {
  const { t } = useI18n();
  const { login, status } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  async function submit(event: React.FormEvent) { event.preventDefault(); setError(null); try { await login(email, password); const from = (location.state as { from?: string } | null)?.from || "/home"; navigate(from, { replace: true }); } catch { setError(t("signInError")); } }
  return <main className="login-page"><div className="login-card"><div className="login-brand"><div className="brand-mark">N</div><div><strong>{t("brand")}</strong><span>ENTERPRISE PMO</span></div></div><h1>{t("signIn")}</h1><p>{t("signInDescription")}</p><form onSubmit={submit}><label>{t("email")}<input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="username" /></label><label>{t("password")}<input required type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" /></label>{error ? <div className="form-error" role="alert">{error}</div> : null}<button className="primary-button submit-button" type="submit" disabled={status === "loading"}><LockKeyhole size={16} />{t("signIn")}<ArrowRight size={16} /></button></form></div></main>;
}
