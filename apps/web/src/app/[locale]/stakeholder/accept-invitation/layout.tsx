import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function StakeholderInvitationLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="stakeholder">{children}</ScopedIntlProvider>;
}
