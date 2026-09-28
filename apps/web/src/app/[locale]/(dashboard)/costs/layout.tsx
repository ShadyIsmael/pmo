import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function CostsLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="costs">{children}</ScopedIntlProvider>;
}
