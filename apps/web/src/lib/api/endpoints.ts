const id = (base: string, value: string) => `${base}/${encodeURIComponent(value)}`;
export const endpoints = {
  auth: { login: "/api/auth/login", refresh: "/api/auth/refresh", logout: "/api/auth/logout", me: "/api/auth/me" },
  projects: { root: "/api/projects", detail: (v: string) => id("/api/projects", v), team: (v: string) => `${id("/api/projects", v)}/team`, member: (p: string, m: string) => `${id("/api/projects", p)}/team/${encodeURIComponent(m)}`, conflicts: (v: string) => `${id("/api/projects", v)}/team/conflicts`, archive: (v: string) => `${id("/api/projects", v)}/archive`, reactivate: (v: string) => `${id("/api/projects", v)}/reactivate`, history: (v: string) => `${id("/api/projects", v)}/manager-history`, snapshots: (v: string) => `${id("/api/projects", v)}/snapshots`, snapshot: (p: string, s: string) => `${id("/api/projects", p)}/snapshots/${encodeURIComponent(s)}`, handovers: (v: string) => `${id("/api/projects", v)}/handover-requests`, changeRequests: (v: string) => `${id("/api/projects", v)}/change-requests` },
  projectChangeRequests: { root: "/api/project-change-requests", detail: (v: string) => id("/api/project-change-requests", v), pending: "/api/project-change-requests/pending-for-me", approve: (v: string) => `${id("/api/project-change-requests", v)}/approve`, reject: (v: string) => `${id("/api/project-change-requests", v)}/reject`, cancel: (v: string) => `${id("/api/project-change-requests", v)}/cancel` },
  portfolios: { root: "/api/portfolios", detail: (v: string) => id("/api/portfolios", v), archive: (v: string) => `${id("/api/portfolios", v)}/archive`, reactivate: (v: string) => `${id("/api/portfolios", v)}/reactivate` },
  resources: { root: "/api/resources", detail: (v: string) => id("/api/resources", v) },
  allocations: { root: "/api/allocations", detail: (v: string) => id("/api/allocations", v), byResource: (v: string) => id("/api/allocations/by-resource", v), byProject: (v: string) => id("/api/allocations/by-project", v), conflicts: "/api/allocations/conflicts" },
  capacity: { matrix: "/api/capacity/matrix", availability: "/api/capacity/availability", demand: "/api/capacity/demand-vs-capacity", gantt: "/api/capacity/gantt", resourceAvailability: (v: string) => `${id("/api/capacity/resource", v)}/availability` },
  users: { root: "/api/users", detail: (v: string) => id("/api/users", v), lookup: "/api/users/lookup", roles: "/api/roles" },
  currencies: { root: "/api/currencies", detail: (v: string) => id("/api/currencies", v) },
  programs: "/api/programs", programDetail: (v: string) => id("/api/programs", v), departments: "/api/departments", skills: "/api/skills", vendors: "/api/vendors", resourceFinder: "/api/resource-finder/find",
  handover: { pending: "/api/handover-requests/pending-for-me", accept: (v: string) => `${id("/api/handover-requests", v)}/accept`, reject: (v: string) => `${id("/api/handover-requests", v)}/reject`, cancel: (v: string) => `${id("/api/handover-requests", v)}/cancel` },
  managerHandovers: {
    pending: "/api/manager-handovers/pending-for-me",
    requests: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/${encodeURIComponent(v)}/requests`,
    create: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/${encodeURIComponent(v)}/requests`,
    accept: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/requests/${encodeURIComponent(v)}/accept`,
    reject: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/requests/${encodeURIComponent(v)}/reject`,
    cancel: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/requests/${encodeURIComponent(v)}/cancel`,
    history: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/${encodeURIComponent(v)}/history`,
    snapshots: (type: "programs" | "portfolios", v: string) => `/api/manager-handovers/${type}/${encodeURIComponent(v)}/snapshots`,
  },
} as const;
