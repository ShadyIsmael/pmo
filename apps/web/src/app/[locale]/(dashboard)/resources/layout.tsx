import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function ResourcesLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="resources">{children}</ScopedIntlProvider>;
}
