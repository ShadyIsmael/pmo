import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import type { ResourceDto } from "@features/resources/api/resources";

/** Reads the current canonical Resource profile for a Project Team contact card. */
export function useProjectTeamContactResource(resourceId: string, enabled = true) {
  return useQuery({
    // Match the Resource detail cache so Resource Profile edits invalidate this data too.
    queryKey: ["resources", "detail", resourceId],
    queryFn: () => api.get<ResourceDto>(endpoints.resources.detail(resourceId)),
    enabled: Boolean(resourceId && enabled),
    staleTime: 0,
    refetchOnMount: "always",
  });
}
