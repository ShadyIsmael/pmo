import { BaseEntity } from "./common";

export interface Document extends BaseEntity {
  projectId?: string;
  organizationId?: string;
  name: string;
  description?: string;
  fileName: string;
  fileSize: number;
  mimeType: string;
  categoryId?: string;
  tags: readonly string[];
  uploadedBy: string;
  version: number;
  isArchived: boolean;
}

export interface DocumentVersion extends BaseEntity {
  documentId: string;
  versionNumber: number;
  fileName: string;
  fileSize: number;
  mimeType: string;
  storagePath: string;
  uploadedBy: string;
  changelog?: string;
  isCurrent: boolean;
}

export interface DocumentCategory extends BaseEntity {
  organizationId: string;
  name: string;
  description?: string;
  parentId?: string;
  icon?: string;
  isActive: boolean;
}

export interface DocumentReview extends BaseEntity {
  documentId: string;
  reviewerId: string;
  status: "pending" | "approved" | "rejected" | "changes_requested";
  comments?: string;
  reviewedAt?: string;
}
