import * as React from "react";

export type Locale = "en" | "ar";

const messages = {
  en: {
    brand: "NEXUS PMO", home: "Home", dashboard: "Dashboard", projects: "Projects", myWork: "My Work", notifications: "Notifications", pilotScope: "Home pilot", search: "Search projects, tasks, documents...", language: "العربية", lightTheme: "Light theme", darkTheme: "Dark theme", signOut: "Sign out", signIn: "Sign in", email: "Email", password: "Password", signInDescription: "Sign in to continue to NEXUS PMO.", signInError: "Unable to sign in. Check your credentials and try again.", activeProjects: "Active projects", projectsAtRisk: "Projects at risk", openRisks: "Open risks", criticalRisks: "Critical risks", health: "Project health", attention: "Projects needing attention", topRisks: "Top risks", budget: "Budget overview", project: "Project", progress: "Progress", risk: "Risk", status: "Status", noAttention: "No projects currently need attention.", noRisks: "No open risks in the current scope.", noBudget: "No budget data in the current scope.", loading: "Loading Home...", loadError: "Home data could not be loaded.", retry: "Retry", refresh: "Refresh", trackTime: "Track time", noActiveTimer: "No active timer", activeTimer: "Active timer", elapsed: "Elapsed", timerUnavailable: "Timer unavailable", unread: "unread notifications", noPermission: "You do not have access to this area.",
  },
  ar: {
    brand: "نيكسس PMO", home: "الرئيسية", dashboard: "لوحة المعلومات", projects: "المشروعات", myWork: "عملي", notifications: "الإشعارات", pilotScope: "نسخة Home التجريبية", search: "البحث في المشروعات والمهام والمستندات...", language: "English", lightTheme: "المظهر الفاتح", darkTheme: "المظهر الداكن", signOut: "تسجيل الخروج", signIn: "تسجيل الدخول", email: "البريد الإلكتروني", password: "كلمة المرور", signInDescription: "سجّل الدخول للمتابعة إلى NEXUS PMO.", signInError: "تعذر تسجيل الدخول. تحقق من البيانات وحاول مرة أخرى.", activeProjects: "المشروعات النشطة", projectsAtRisk: "مشروعات معرضة للخطر", openRisks: "المخاطر المفتوحة", criticalRisks: "المخاطر الحرجة", health: "حالة المشروعات", attention: "مشروعات تحتاج إلى متابعة", topRisks: "أهم المخاطر", budget: "ملخص الميزانية", project: "المشروع", progress: "نسبة الإنجاز", risk: "الخطر", status: "الحالة", noAttention: "لا توجد مشروعات تحتاج إلى متابعة حاليًا.", noRisks: "لا توجد مخاطر مفتوحة ضمن النطاق الحالي.", noBudget: "لا توجد بيانات ميزانية ضمن النطاق الحالي.", loading: "جارٍ تحميل الرئيسية...", loadError: "تعذر تحميل بيانات الرئيسية.", retry: "إعادة المحاولة", refresh: "تحديث", trackTime: "تسجيل الوقت", noActiveTimer: "لا يوجد مؤقت نشط", activeTimer: "المؤقت النشط", elapsed: "الوقت المنقضي", timerUnavailable: "المؤقت غير متاح", unread: "إشعارات غير مقروءة", noPermission: "ليس لديك صلاحية الوصول إلى هذه المنطقة.",
  },
} as const;

type MessageKey = keyof typeof messages.en;
interface I18nContextValue { locale: Locale; setLocale: (locale: Locale) => void; t: (key: MessageKey) => string; }
const I18nContext = React.createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocale] = React.useState<Locale>(() => {
    const saved = typeof window === "undefined" ? null : window.localStorage.getItem("nexus-react-locale");
    return saved === "ar" ? "ar" : "en";
  });
  React.useEffect(() => { window.localStorage.setItem("nexus-react-locale", locale); document.documentElement.lang = locale; document.documentElement.dir = locale === "ar" ? "rtl" : "ltr"; }, [locale]);
  const value = React.useMemo<I18nContextValue>(() => ({ locale, setLocale, t: (key) => messages[locale][key] }), [locale]);
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue { const value = React.useContext(I18nContext); if (!value) throw new Error("useI18n must be used within I18nProvider"); return value; }
export function formatNumber(value: number): string { return new Intl.NumberFormat("en-US", { useGrouping: false }).format(value); }
export function formatPercent(value: number): string { return `${formatNumber(Math.round(value))}%`; }
