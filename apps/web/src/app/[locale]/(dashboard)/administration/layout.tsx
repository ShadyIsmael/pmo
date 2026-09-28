import { AdministrationSidebar } from "@/components/administration/AdministrationSidebar";
import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function AdministrationLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="administration"><div className="grid min-w-0 gap-6 lg:grid-cols-[15rem_minmax(0,1fr)]"><AdministrationSidebar /><main className="min-w-0">{children}</main></div></ScopedIntlProvider>;
}
