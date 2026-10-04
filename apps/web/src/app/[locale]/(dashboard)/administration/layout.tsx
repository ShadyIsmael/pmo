import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function AdministrationLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="administration"><main className="min-w-0">{children}</main></ScopedIntlProvider>;
}
