import type { ApiClient } from "./client";
import type { Portfolio, PortfolioHealth, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createPortfoliosApi(client: ApiClient) {
  return {
    getPortfolios: (params?: PaginationParams) =>
      client.getPaged<Portfolio>("/portfolios", params),

    getPortfolio: (id: string) => client.get<Portfolio>(`/portfolios/${id}`),

    getPortfolioHealth: (id: string) =>
      client.get<PortfolioHealth>(`/portfolios/${id}/health`),
  };
}
