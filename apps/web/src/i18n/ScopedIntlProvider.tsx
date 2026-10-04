import { NextIntlClientProvider } from "next-intl";
import { getMessages } from "next-intl/server";
import type { AbstractIntlMessages } from "use-intl/core";

export const BASE_NAMESPACES = ["nav", "common", "enums", "organization", "perspective", "programManager", "portfolioManager", "dashboard", "timeTracking", "costUtilization", "account"] as const;

export type FeatureMessageNamespace =
  | "administration"
  | "approvals"
  | "managerHandover"
  | "meetings"
  | "projects"
  | "resources"
  | "risks"
  | "costs"
  | "documents"
  | "stakeholder";

export function selectMessageNamespaces(
  messages: AbstractIntlMessages,
  namespaces: readonly string[],
): AbstractIntlMessages {
  return namespaces.reduce<AbstractIntlMessages>((selected, namespace) => {
    const messageNamespace = messages[namespace];
    if (messageNamespace !== undefined) selected[namespace] = messageNamespace;
    return selected;
  }, {});
}

export async function ScopedIntlProvider({
  feature,
  additionalFeatures = [],
  children,
}: {
  feature: FeatureMessageNamespace;
  additionalFeatures?: readonly FeatureMessageNamespace[];
  children: React.ReactNode;
}) {
  const messages = await getMessages();

  return (
    <NextIntlClientProvider
      messages={selectMessageNamespaces(messages, [...BASE_NAMESPACES, feature, ...additionalFeatures])}
    >
      {children}
    </NextIntlClientProvider>
  );
}
