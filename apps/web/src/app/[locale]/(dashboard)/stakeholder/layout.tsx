import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function StakeholderLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="stakeholder">{children}</ScopedIntlProvider>;
}
