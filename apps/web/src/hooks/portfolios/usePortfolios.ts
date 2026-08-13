"use client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
export interface PortfolioProject { id:string; name:string; code:string; status:string; health:string; ragStatus:string; progress:number; startDate?:string; endDate?:string; budget?:number; forecast?:number; actualCost?:number; currency:string; }
export interface PortfolioMetrics { projectCount:number; activeProjects:number; completedProjects:number; delayedProjects:number; totalBudget:number; totalForecast:number; totalActualCost:number; averageProgress:number; earliestStartDate?:string; latestEndDate?:string; projectsByStatus:Record<string,number>; projectsByHealth:Record<string,number>; }
export interface PortfolioDto { id:string; name:string; code:string; description?:string; status:string; ownerId:string; ownerName?:string; ownerEmail?:string; metrics:PortfolioMetrics; projects:PortfolioProject[]; createdAt:string; updatedAt?:string; createdBy:string; updatedBy?:string; }
export type SavePortfolioDto={name:string;description?:string;ownerId:string;status:"Active"|"Archived"};
export function usePortfolios(params:{search?:string;status?:string}={}){return useQuery({queryKey:["portfolios",params],queryFn:()=>api.get<PagedResult<PortfolioDto>>("/api/portfolios",{pageSize:200,...params}),staleTime:15000});}
export function usePortfolio(id?:string){return useQuery({queryKey:["portfolios",id],queryFn:()=>api.get<PortfolioDto>(`/api/portfolios/${id}`),enabled:!!id});}
function usePortfolioMutation(method:"post"|"put",suffix=""){const q=useQueryClient();return useMutation({mutationFn:({id,dto}:{id?:string;dto?:SavePortfolioDto})=>method==="put"?api.put<PortfolioDto>(`/api/portfolios/${id}`,dto):api.post<PortfolioDto>(`/api/portfolios/${id||""}${suffix}`,dto),onSuccess:()=>q.invalidateQueries({queryKey:["portfolios"]})});}
export const useCreatePortfolio=()=>usePortfolioMutation("post");export const useUpdatePortfolio=()=>usePortfolioMutation("put");export const useArchivePortfolio=()=>usePortfolioMutation("post","/archive");export const useReactivatePortfolio=()=>usePortfolioMutation("post","/reactivate");
