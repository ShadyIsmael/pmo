import { Bell, Languages, LogOut, Moon, Search, Sun } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { useI18n } from "../i18n/messages";
import { useTheme } from "../theme/ThemeProvider";
import { useUnreadNotificationCount } from "../api/notifications";
import { HeaderTimer } from "./HeaderTimer";

export function Header() {
  const { user, logout } = useAuth();
  const { locale, setLocale, t } = useI18n();
  const { theme, toggleTheme } = useTheme();
  const unread = useUnreadNotificationCount(Boolean(user));
  const navigate = useNavigate();
  const displayName = user?.fullName || user?.email || "User";
  const initials = displayName.split(" ").map((part) => part[0]).join("").slice(0, 2).toUpperCase();
  async function signOut() { await logout(); navigate("/login", { replace: true }); }
  return <header className="app-header">
    <div className="header-search"><Search size={16} /><span>{t("search")}</span><kbd>Ctrl K</kbd></div>
    <HeaderTimer />
    <div className="header-actions">
      <button className="icon-button" type="button" onClick={() => setLocale(locale === "en" ? "ar" : "en")} aria-label={t("language")} title={t("language")}><Languages size={17} /></button>
      <button className="icon-button" type="button" onClick={toggleTheme} aria-label={theme === "dark" ? t("lightTheme") : t("darkTheme")} title={theme === "dark" ? t("lightTheme") : t("darkTheme")}>{theme === "dark" ? <Sun size={17} /> : <Moon size={17} />}</button>
      <div className="notification-indicator" aria-label={`${unread.data ?? 0} ${t("unread")}`} title={`${unread.data ?? 0} ${t("unread")}`}><Bell size={17} />{(unread.data ?? 0) > 0 ? <span>{unread.data}</span> : null}</div>
      <div className="user-menu"><span className="avatar">{initials}</span><span className="user-name">{displayName}</span><button className="icon-button" type="button" onClick={signOut} aria-label={t("signOut")} title={t("signOut")}><LogOut size={16} /></button></div>
    </div>
  </header>;
}
