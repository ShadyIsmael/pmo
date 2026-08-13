import { BaseEntity } from "./common";

export interface Resource extends BaseEntity {
  userId: string;
  name: string;
  employeeId?: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  avatarUrl?: string;
  type: string;
  status: string;
  costPerHour?: number;
  billableRate?: number;
  currency?: string;
  employmentType?: string;
  seniority?: string;
  dateOfJoining?: string;
  businessUnitId?: string;
  departmentId?: string;
  departmentName?: string;
  teamId?: string;
  locationId?: string;
  locationName?: string;
  timezone?: string;
  utilizationTarget?: number;
  currentUtilization?: number;
  notes?: string;
  isActive: boolean;
  skills: ResourceSkill[];
  allocations?: ResourceAllocation[];
}

export interface ResourceSkill extends BaseEntity {
  resourceId: string;
  skillId: string;
  skillName: string;
  skillCategory?: string;
  proficiency: string;
  yearsOfExperience: number;
}

export interface Skill extends BaseEntity {
  name: string;
  category?: string;
  description?: string;
  isActive: boolean;
}

export interface ResourceAllocation extends BaseEntity {
  resourceId: string;
  projectId?: string;
  projectName?: string;
  role: string;
  allocationPercentage: number;
  plannedHours?: number;
  actualHours?: number;
  startDate: string;
  endDate: string;
  status?: string;
  notes?: string;
}

export interface ResourceRequest extends BaseEntity {
  projectId?: string;
  projectName: string;
  role: string;
  skillRequirements?: string;
  requiredAllocation: number;
  requiredFrom: string;
  requiredTo: string;
  priority: string;
  status: string;
  justification?: string;
  requestedBy?: string;
  requestedAt: string;
}

export interface RateCard extends BaseEntity {
  name: string;
  role: string;
  seniority?: string;
  hourlyRate: number;
  dailyRate?: number;
  monthlyRate?: number;
  currency?: string;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
}

export interface Contract extends BaseEntity {
  resourceId: string;
  type: string;
  startDate: string;
  endDate?: string;
  value?: number;
  currency?: string;
  terms?: string;
  vendorName?: string;
  status: string;
}

export interface ResourceCalendar extends BaseEntity {
  resourceId: string;
  date: string;
  dayType: string;
  availableHours?: number;
  notes?: string;
}

export interface Vendor extends BaseEntity {
  name: string;
  code?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  category?: string;
  status?: string;
  contractValue?: number;
  contractStart?: string;
  contractEnd?: string;
  isActive: boolean;
}
