import { BaseEntity } from "./common";
import { ApprovalStatus } from "./approval";

export enum CostType {
  Direct = "direct",
  Indirect = "indirect",
  Labor = "labor",
  Material = "material",
  Equipment = "equipment",
  Travel = "travel",
  Software = "software",
  Hardware = "hardware",
  Consulting = "consulting",
  Training = "training",
  Other = "other",
}

export interface Budget extends BaseEntity {
  projectId?: string;
  portfolioId?: string;
  fiscalYear: number;
  totalAmount: number;
  spentAmount: number;
  committedAmount: number;
  remainingAmount: number;
  currencyCode: string;
  status: "draft" | "approved" | "active" | "closed" | "overwritten";
  approvedBy?: string;
  approvedAt?: string;
  notes?: string;
}

export interface CostEntry extends BaseEntity {
  projectId: string;
  costType: CostType;
  amount: number;
  currencyCode: string;
  description: string;
  entryDate: string;
  categoryId?: string;
  invoiceId?: string;
  purchaseOrderId?: string;
  approvedBy?: string;
  approvedAt?: string;
  isBillable: boolean;
}

export interface CostCategory extends BaseEntity {
  organizationId: string;
  name: string;
  code: string;
  description?: string;
  parentId?: string;
  isActive: boolean;
}

export interface CostApproval extends BaseEntity {
  costEntryId: string;
  approverId: string;
  status: ApprovalStatus;
  comments?: string;
  decidedAt?: string;
}

export interface Invoice extends BaseEntity {
  projectId?: string;
  vendorId?: string;
  invoiceNumber: string;
  amount: number;
  taxAmount: number;
  totalAmount: number;
  currencyCode: string;
  issueDate: string;
  dueDate: string;
  paidDate?: string;
  status: "draft" | "submitted" | "approved" | "paid" | "overdue" | "cancelled";
  notes?: string;
}

export interface PurchaseCommitment extends BaseEntity {
  projectId?: string;
  vendorId?: string;
  poNumber: string;
  description: string;
  amount: number;
  currencyCode: string;
  issuedDate: string;
  expectedDeliveryDate?: string;
  status: "draft" | "issued" | "accepted" | "partial" | "completed" | "cancelled";
}


