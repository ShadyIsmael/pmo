import { ScopedIntlProvider } from "@/i18n/ScopedIntlProvider";

export default function MeetingsLayout({ children }: { children: React.ReactNode }) {
  return <ScopedIntlProvider feature="meetings">{children}</ScopedIntlProvider>;
}
