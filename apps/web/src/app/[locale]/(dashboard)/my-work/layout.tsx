import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function MyWorkLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="approvals">{children}</ScopedIntlProvider>;
}
