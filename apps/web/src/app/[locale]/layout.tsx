import { NextIntlClientProvider } from "next-intl";
import { getMessages } from "next-intl/server";
import { notFound } from "next/navigation";
import { SUPPORTED_LOCALES } from "@/lib/constants";
import { DocumentDirection } from "@/components/layout/DocumentDirection";
import { BASE_NAMESPACES, selectMessageNamespaces } from "@/i18n/ScopedIntlProvider";

export default async function LocaleLayout({
  children,
  params: { locale },
}: {
  children: React.ReactNode;
  params: { locale: string };
}) {
  if (!(SUPPORTED_LOCALES as readonly string[]).includes(locale)) notFound();

  const messages = await getMessages();

  return (
    <NextIntlClientProvider messages={selectMessageNamespaces(messages, BASE_NAMESPACES)}>
      <DocumentDirection locale={locale} />
      {children}
    </NextIntlClientProvider>
  );
}
