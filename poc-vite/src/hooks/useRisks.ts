import { useQuery } from "@tanstack/react-query";
import { riskApi } from "@/api/client";
import type { RiskDto } from "@/types/risk";

type RiskCollection = { items: readonly RiskDto[]; totalCount: number };

async function fetchAllRisks(): Promise<RiskCollection> {
  const pageSize = 100;
  const firstPage = await riskApi.list({ page: 1, pageSize });

  if (firstPage.totalPages <= 1) {
    return { items: [...firstPage.items], totalCount: firstPage.totalCount };
  }

  const remainingPages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, i) =>
      riskApi.list({ page: i + 2, pageSize })
    )
  );

  return {
    items: [firstPage.items, ...remainingPages.map((p) => p.items)].flat(),
    totalCount: firstPage.totalCount,
  };
}

export function useRisks() {
  return useQuery({
    queryKey: ["risks", "list"],
    queryFn: fetchAllRisks,
    staleTime: 30_000,
  });
}
