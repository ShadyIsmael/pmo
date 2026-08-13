export enum AIProviderType {
  OpenAI = "openai",
  AzureOpenAI = "azure_openai",
  Anthropic = "anthropic",
  Google = "google",
  Custom = "custom",
}

export interface AIPlanRequest {
  projectId?: string;
  portfolioId?: string;
  context: string;
  constraints?: string;
  objectives: readonly string[];
  provider: AIProviderType;
  model?: string;
  includeRecommendations: boolean;
}

export interface AIPlanResponse {
  plan: GeneratedPlan;
  recommendations: readonly PlanRecommendation[];
  provider: AIProviderType;
  model: string;
  tokensUsed: number;
  processingTimeMs: number;
}

export interface GeneratedPlan {
  summary: string;
  phases: readonly PlanPhase[];
  timeline: string;
  resources: readonly string[];
  risks: readonly string[];
  estimatedDuration: number;
  estimatedCost: number;
  confidence: number;
}

export interface PlanPhase {
  name: string;
  description: string;
  duration: string;
  dependencies: readonly string[];
  deliverables: readonly string[];
  resources: readonly string[];
}

export interface PlanRecommendation {
  category: string;
  title: string;
  description: string;
  impact: "high" | "medium" | "low";
  effort: "high" | "medium" | "low";
  confidence: number;
}
