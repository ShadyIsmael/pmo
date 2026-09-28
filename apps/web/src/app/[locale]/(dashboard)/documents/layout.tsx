import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function DocumentsLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="documents"><div className="nexus-document-surface">{children}</div></ScopedIntlProvider>;
}
