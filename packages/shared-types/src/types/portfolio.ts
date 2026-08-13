import { BaseEntity } from "./common";

export interface Portfolio extends BaseEntity {
  organizationId: string;
  name: string;
  code: string;
  description?: string;
  objectives: string;
  managerId: string;
  sponsorId?: string;
  startDate: string;
  endDate?: string;
  health: PortfolioHealth;
  budget: number;
  currencyCode: string;
  isActive: boolean;
}

export interface Program extends BaseEntity {
  portfolioId: string;
  name: string;
  code: string;
  description?: string;
  objectives: string;
  managerId: string;
  startDate: string;
  endDate?: string;
  health: PortfolioHealth;
  budget: number;
  currencyCode: string;
  isActive: boolean;
}

export interface StrategicObjective extends BaseEntity {
  portfolioId: string;
  name: string;
  description?: string;
  category: string;
  targetDate?: string;
  progress: number;
  status: string;
  measurementCriteria?: string;
}

export interface PortfolioHealth {
  overall: "green" | "amber" | "red";
  schedule: "green" | "amber" | "red";
  budget: "green" | "amber" | "red";
  resource: "green" | "amber" | "red";
  risk: "green" | "amber" | "red";
  lastUpdated: string;
  notes?: string;
}

export interface PortfolioBudget extends BaseEntity {
  portfolioId: string;
  fiscalYear: number;
  totalBudget: number;
  allocatedBudget: number;
  spentBudget: number;
  remainingBudget: number;
  currencyCode: string;
}

export interface PortfolioRisk extends BaseEntity {
  portfolioId: string;
  riskId: string;
  impact: number;
  probability: number;
  score: number;
  mitigationStrategy?: string;
}
