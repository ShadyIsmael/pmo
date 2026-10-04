import { DashboardAuthGate } from "@/components/layout/DashboardAuthGate";
import { Sidebar } from "@/components/layout/Sidebar";
import { Header } from "@/components/layout/Header";
import { Breadcrumbs } from "@/components/layout/Breadcrumbs";

export default function DashboardLayout({
  children,
  params: { locale },
}: {
  children: React.ReactNode;
  params: { locale: string };
}) {
  return (
    <DashboardAuthGate>
      <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-shell flex h-screen min-h-0 overflow-hidden bg-muted/30">
        <Sidebar />
        <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
          <Header />
          <main className="nexus-main min-h-0 min-w-0 flex-1 overflow-y-auto overflow-x-hidden">
            <div className="nexus-content flex flex-col gap-4 p-4 lg:p-5 xl:p-6">
              <Breadcrumbs />
              {children}
            </div>
          </main>
        </div>
      </div>
    </DashboardAuthGate>
  );
}
