import { BaseEntity } from "./common";

export interface Organization extends BaseEntity {
  name: string;
  legalName: string;
  registrationNumber?: string;
  taxId?: string;
  logoUrl?: string;
  website?: string;
  email: string;
  phone?: string;
  address?: string;
  isActive: boolean;
}

export interface BusinessUnit extends BaseEntity {
  organizationId: string;
  name: string;
  code: string;
  description?: string;
  managerId?: string;
  isActive: boolean;
}

export interface Department extends BaseEntity {
  businessUnitId: string;
  name: string;
  code: string;
  description?: string;
  managerId?: string;
  isActive: boolean;
}

export interface Team extends BaseEntity {
  departmentId: string;
  name: string;
  code: string;
  description?: string;
  leadId?: string;
  isActive: boolean;
}

export interface Location extends BaseEntity {
  organizationId: string;
  name: string;
  address: string;
  city: string;
  state?: string;
  country: string;
  postalCode?: string;
  latitude?: number;
  longitude?: number;
  isActive: boolean;
}

export interface WorkingCalendar extends BaseEntity {
  organizationId: string;
  name: string;
  description?: string;
  timezone: string;
  workDays: readonly number[];
  workHoursStart: string;
  workHoursEnd: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface Holiday extends BaseEntity {
  calendarId: string;
  name: string;
  date: string;
  isRecurring: boolean;
  recurrencePattern?: string;
}

export interface Lookup extends BaseEntity {
  type: string;
  code: string;
  displayName: string;
  displayNameAr?: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  parentId?: string;
  metadata?: Record<string, unknown>;
}

export interface Currency extends BaseEntity {
  code: string;
  name: string;
  symbol: string;
  decimalPlaces: number;
  isActive: boolean;
}

export interface CostCentre extends BaseEntity {
  organizationId: string;
  code: string;
  name: string;
  description?: string;
  managerId?: string;
  budget: number;
  currencyCode: string;
  isActive: boolean;
}
