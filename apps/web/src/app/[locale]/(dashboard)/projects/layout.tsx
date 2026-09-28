import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function ProjectsLayout({
  children,
  params: { locale },
}: {
  children: React.ReactNode;
  params: { locale: string };
}) {
  return (
    <ScopedIntlProvider feature="projects" additionalFeatures={["risks", "meetings", "documents"]}>
      <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-project-surface">
        {children}
      </div>
    </ScopedIntlProvider>
  );
}
