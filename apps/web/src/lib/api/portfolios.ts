import { api } from "@/lib/api/client"; import { endpoints } from "@/lib/api/endpoints";
export const portfolioKeys={all:["portfolios"] as const,list:(filters:unknown={})=>["portfolios","list",filters] as const,detail:(id:string)=>["portfolios","detail",id] as const};
export const portfolioApi={list:<T>(p?:Record<string,unknown>)=>api.get<T>(endpoints.portfolios.root,p as never),detail:<T>(id:string)=>api.get<T>(endpoints.portfolios.detail(id)),create:<T>(b:unknown)=>api.post<T>(endpoints.portfolios.root,b),update:<T>(id:string,b:unknown)=>api.put<T>(endpoints.portfolios.detail(id),b)};
