import { useTranslations } from "next-intl";
import {
  ProjectBaselineStatus,
  ProjectBaselineStatusHelper,
  ProjectChangeRequestStatus,
  ProjectChangeRequestStatusHelper,
  ProjectChangeRequestType,
  ProjectChangeRequestTypeHelper,
} from "@/lib/enums";

type EnumTypeMap = {
  projectBaselineStatus: ProjectBaselineStatus;
  projectChangeRequestStatus: ProjectChangeRequestStatus;
  projectChangeRequestType: ProjectChangeRequestType;
};

interface EnumLabelHelper<T extends string> {
  getLabelKey: (value: T) => string;
}

const HELPER_MAP: { [K in keyof EnumTypeMap]: EnumLabelHelper<EnumTypeMap[K]> } = {
  projectBaselineStatus: ProjectBaselineStatusHelper,
  projectChangeRequestStatus: ProjectChangeRequestStatusHelper,
  projectChangeRequestType: ProjectChangeRequestTypeHelper,
};

function getLabelKey<K extends keyof EnumTypeMap>(
  helper: EnumLabelHelper<EnumTypeMap[K]>,
  value: EnumTypeMap[K],
): string {
  return helper.getLabelKey(value);
}

/**
 * Custom hook to get the translated label for an enum value.
 * @param enumType The type of enum (e.g., "projectChangeRequestStatus").
 * @returns A function that takes an enum value and returns its translated label.
 */
export function useEnumLabel<K extends keyof EnumTypeMap>(
  enumType: K,
): (value: EnumTypeMap[K]) => string {
  const t = useTranslations();
  return (value) => t(getLabelKey(HELPER_MAP[enumType], value));
}