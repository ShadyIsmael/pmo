import { BarChart3, Bell, BriefcaseBusiness, Home, LockKeyhole, type LucideIcon } from "lucide-react";
import { NavLink } from "react-router-dom";
import { useI18n } from "../i18n/messages";

interface NavItem { key: "home" | "myWork" | "projects" | "notifications"; icon: LucideIcon; active?: boolean; }
const navItems: NavItem[] = [
  { key: "home", icon: Home, active: true },
  { key: "myWork", icon: BriefcaseBusiness },
  { key: "projects", icon: BarChart3 },
  { key: "notifications", icon: Bell },
];

export function Sidebar() {
  const { t } = useI18n();
  return <aside className="sidebar" aria-label="Primary navigation">
    <div className="brand"><div className="brand-mark">N</div><div><strong>{t("brand")}</strong><span>ENTERPRISE PMO</span></div></div>
    <div className="sidebar-search">⌕ <span>{t("search")}</span></div>
    <p className="nav-caption">{t("pilotScope")}</p>
    <nav className="nav-list">
      {navItems.map(({ key, icon: Icon, active }) => active
        ? <NavLink key={key} to="/home" className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}><Icon size={17} /><span>{t(key)}</span></NavLink>
        : <span key={key} className="nav-item disabled" aria-disabled="true"><Icon size={17} /><span>{t(key)}</span></span>)}
    </nav>
    <div className="sidebar-footer"><LockKeyhole size={15} /><span>Tenant and permission scope preserved</span></div>
  </aside>;
}
