import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function RisksIssuesLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="risks">{children}</ScopedIntlProvider>;
}
