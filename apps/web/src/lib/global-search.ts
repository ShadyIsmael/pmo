import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export type SearchEntityType = "project" | "program" | "portfolio" | "resource" | "meeting";

export interface SearchResult {
  id: string;
  type: SearchEntityType;
  name: string;
  href: string;
  code?: string;
  status?: string;
  context?: string;
}

interface SearchProject { id: string; name: string; code?: string; status?: string; }
interface SearchProgram { id: string; name: string; code?: string; status?: string; portfolioName?: string; }
interface SearchPortfolio { id: string; name: string; code?: string; status?: string; }
interface SearchResource { id: string; name: string; employeeId?: string; jobTitle?: string; status?: string; }
interface SearchMeeting { id: string; title: string; projectName?: string; status?: string; startTime?: string; }

function page<T>(result: PromiseSettledResult<PagedResult<T>>): readonly T[] {
  return result.status === "fulfilled" ? result.value.items ?? [] : [];
}

/** Bounded, composed read search. Nothing is requested until the user searches. */
export async function searchNexus(query: string): Promise<SearchResult[]> {
  const q = query.trim();
  if (q.length < 2) return [];
  const [projects, programs, portfolios, resources, meetings] = await Promise.allSettled([
    api.get<PagedResult<SearchProject>>(endpoints.projects.root, { search: q, page: 1, pageSize: 5 }),
    api.get<PagedResult<SearchProgram>>(endpoints.programs.root, { search: q, page: 1, pageSize: 5 }),
    api.get<PagedResult<SearchPortfolio>>(endpoints.portfolios.root, { search: q, page: 1, pageSize: 5 }),
    api.get<PagedResult<SearchResource>>(endpoints.resources.root, { search: q, page: 1, pageSize: 5 }),
    api.get<PagedResult<SearchMeeting>>(endpoints.meetings.root, { search: q, page: 1, pageSize: 5 }),
  ]);
  return [
    ...page(projects).map((x) => ({ id: x.id, type: "project" as const, name: x.name, href: `/projects/${x.id}`, code: x.code, status: x.status })),
    ...page(programs).map((x) => ({ id: x.id, type: "program" as const, name: x.name, href: `/portfolios/programs/${x.id}`, code: x.code, status: x.status, context: x.portfolioName })),
    ...page(portfolios).map((x) => ({ id: x.id, type: "portfolio" as const, name: x.name, href: `/portfolios/${x.id}`, code: x.code, status: x.status })),
    // Resources currently have a directory route rather than a dedicated detail URL.
    ...page(resources).map((x) => ({ id: x.id, type: "resource" as const, name: x.name, href: "/resources", code: x.employeeId, status: x.status, context: x.jobTitle })),
    ...page(meetings).map((x) => ({ id: x.id, type: "meeting" as const, name: x.title, href: `/meetings/${x.id}`, status: x.status, context: x.projectName })),
  ];
}
