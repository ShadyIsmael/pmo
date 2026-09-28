import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function PortfoliosLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="managerHandover">{children}</ScopedIntlProvider>;
}
