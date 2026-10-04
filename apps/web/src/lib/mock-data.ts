// @ts-nocheck - Mock data file; will be replaced by API calls
export type ProjectHealth = "green" | "amber" | "red";
export type ProjectStatus = "Active" | "At Risk" | "Delayed" | "On Hold" | "Completed" | "Cancelled";

export interface Portfolio {
  id: string;
  name: string;
  description: string;
  programs: number;
  progress: number;
  budget: number;
  manager: string;
}

export interface Project {
  id: string;
  name: string;
  code: string;
  description: string;
  portfolio: string;
  program: string;
  status: ProjectStatus;
  health: ProjectHealth;
  progress: number;
  plannedFinish: string;
  forecastFinish: string;
  budget: number;
  forecast: number;
  actualCost: number;
  scheduleVariance: number;
  costVariance: number;
  criticalRisks: number;
  projectManager: string;
  sponsor: string;
  businessUnit: string;
  priority: "Critical" | "High" | "Medium" | "Low";
  methodology: "Waterfall" | "Agile" | "Hybrid";
  phase: string;
  startDate: string;
  completionDate: string;
  lastUpdate: string;
  source: string;
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: string;
  avatar?: string;
  department: string;
  jobTitle: string;
  phone: string;
  location: string;
  manager: string;
}

export interface Resource {
  id: string;
  name: string;
  email: string;
  role: string;
  skills: string[];
  dailyRate: number;
  currency: string;
  availability: number;
  allocated: number;
  department: string;
  location: string;
  type: "Internal" | "Outsourced";
  vendor?: string;
}

export interface Task {
  id: string;
  projectId: string;
  title: string;
  description: string;
  assignee: string;
  status: "Not Started" | "In Progress" | "Completed" | "Blocked";
  priority: "Critical" | "High" | "Medium" | "Low";
  startDate: string;
  endDate: string;
  estimatedHours: number;
  actualHours: number;
  dependencies: string[];
  isMilestone: boolean;
  isShared: boolean;
  deliverables: string[];
}

export interface Risk {
  id: string;
  projectId: string;
  title: string;
  description: string;
  probability: number;
  impact: number;
  score: number;
  category: string;
  owner: string;
  status: "Open" | "Mitigating" | "Closed";
  mitigationPlan: string;
  contingencyPlan: string;
  createdAt: string;
  updatedAt: string;
}

export interface Issue {
  id: string;
  projectId: string;
  title: string;
  description: string;
  severity: "Critical" | "High" | "Medium" | "Low";
  status: "Open" | "In Progress" | "Resolved" | "Closed";
  owner: string;
  createdAt: string;
  resolvedAt?: string;
}

export interface Meeting {
  id: string;
  projectId: string;
  title: string;
  description: string;
  startDate: string;
  endDate: string;
  location: string;
  type: string;
  attendees: string[];
  createdBy: string;
  status: "Scheduled" | "In Progress" | "Completed" | "Cancelled";
}

export interface MeetingAction {
  id: string;
  meetingId: string;
  description: string;
  owner: string;
  dueDate: string;
  status: "Open" | "In Progress" | "Completed";
}

export interface Approval {
  id: string;
  type: string;
  title: string;
  description: string;
  requestedBy: string;
  requestedAt: string;
  status: "Pending" | "Approved" | "Rejected";
  urgency: "Low" | "Medium" | "High";
}

export interface Ticket {
  id: string;
  projectId: string;
  title: string;
  description: string;
  priority: "Low" | "Medium" | "High" | "Critical";
  status: "Open" | "In Progress" | "Resolved" | "Closed";
  assignee: string;
  createdAt: string;
}

export interface TestCase {
  id: string;
  projectId: string;
  title: string;
  description: string;
  status: "Draft" | "Ready" | "Passed" | "Failed" | "Blocked";
  priority: "Low" | "Medium" | "High" | "Critical";
  assignedTo: string;
  lastRun: string;
}

export const mockPortfolios: Portfolio[] = [
  { id: "portfolio-1", name: "Strategic Initiatives", description: "High-priority strategic transformation initiatives aligned with organisational goals", programs: 2, progress: 72, budget: 185_000_000, manager: "user-1" },
  { id: "portfolio-2", name: "Digital Transformation", description: "Digital innovation and technology modernisation programmes", programs: 2, progress: 58, budget: 120_000_000, manager: "user-2" },
  { id: "portfolio-3", name: "Infrastructure & Operations", description: "Infrastructure upgrades, operational excellence and managed services", programs: 2, progress: 81, budget: 95_000_000, manager: "user-3" },
  { id: "portfolio-4", name: "Business Enablement", description: "Cross-functional business process improvement and compliance initiatives", programs: 1, progress: 65, budget: 65_000_000, manager: "user-4" },
];

export const mockProjects: Project[] = [
  { id: "proj-1", name: "Core Banking Modernisation", code: "CBM-2024", description: "Replace legacy core banking platform with cloud-native microservices architecture", portfolio: "portfolio-1", program: "program-1", status: "Active", health: "green", progress: 62, plannedFinish: "2025-06-30", forecastFinish: "2025-06-30", budget: 45_000_000, forecast: 43_200_000, actualCost: 27_500_000, scheduleVariance: 2.1, costVariance: 4.0, criticalRisks: 2, projectManager: "user-10", sponsor: "user-1", businessUnit: "Retail Banking", priority: "Critical", methodology: "Agile", phase: "Execution", startDate: "2024-01-15", completionDate: "2025-06-30", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-2", name: "ERP Cloud Migration", code: "ERP-2024", description: "Migrate on-premise SAP ECC to SAP S/4HANA Cloud", portfolio: "portfolio-1", program: "program-1", status: "Active", health: "green", progress: 78, plannedFinish: "2025-03-31", forecastFinish: "2025-03-31", budget: 32_000_000, forecast: 29_800_000, actualCost: 24_100_000, scheduleVariance: 1.5, costVariance: 6.9, criticalRisks: 1, projectManager: "user-11", sponsor: "user-1", businessUnit: "Group IT", priority: "Critical", methodology: "Waterfall", phase: "Testing", startDate: "2023-09-01", completionDate: "2025-03-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-3", name: "AI-Powered Risk Analytics Platform", code: "AIRA-2025", description: "Build machine learning platform for real-time credit and operational risk assessment", portfolio: "portfolio-2", program: "program-2", status: "Active", health: "green", progress: 35, plannedFinish: "2025-12-31", forecastFinish: "2026-01-15", budget: 18_500_000, forecast: 19_200_000, actualCost: 6_800_000, scheduleVariance: -2.0, costVariance: -3.8, criticalRisks: 3, projectManager: "user-12", sponsor: "user-2", businessUnit: "Risk Management", priority: "High", methodology: "Agile", phase: "Development", startDate: "2024-10-01", completionDate: "2025-12-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-4", name: "Omni-Channel Customer Hub", code: "OCH-2024", description: "Create unified customer engagement platform across web, mobile, and branch", portfolio: "portfolio-2", program: "program-2", status: "Active", health: "amber", progress: 45, plannedFinish: "2025-09-30", forecastFinish: "2025-11-15", budget: 22_000_000, forecast: 23_500_000, actualCost: 10_200_000, scheduleVariance: -8.5, costVariance: -6.8, criticalRisks: 4, projectManager: "user-13", sponsor: "user-2", businessUnit: "Customer Experience", priority: "Critical", methodology: "Agile", phase: "Development", startDate: "2024-04-01", completionDate: "2025-09-30", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-5", name: "Data Centre Consolidation", code: "DCC-2024", description: "Consolidate 12 regional data centres into 3 Tier-3 facilities", portfolio: "portfolio-3", program: "program-3", status: "Active", health: "green", progress: 88, plannedFinish: "2025-02-28", forecastFinish: "2025-02-28", budget: 28_000_000, forecast: 27_100_000, actualCost: 23_900_000, scheduleVariance: 3.2, costVariance: 3.2, criticalRisks: 1, projectManager: "user-14", sponsor: "user-3", businessUnit: "Infrastructure", priority: "High", methodology: "Waterfall", phase: "Closure", startDate: "2023-06-01", completionDate: "2025-02-28", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-6", name: "Regulatory Compliance Suite", code: "RCS-2025", description: "Implement integrated regulatory reporting and compliance management system", portfolio: "portfolio-4", program: "program-4", status: "Active", health: "amber", progress: 52, plannedFinish: "2025-08-31", forecastFinish: "2025-10-15", budget: 14_500_000, forecast: 15_800_000, actualCost: 7_600_000, scheduleVariance: -12.0, costVariance: -9.0, criticalRisks: 3, projectManager: "user-15", sponsor: "user-4", businessUnit: "Compliance", priority: "Critical", methodology: "Hybrid", phase: "Execution", startDate: "2024-07-01", completionDate: "2025-08-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-7", name: "Digital Lending Platform", code: "DLP-2025", description: "End-to-end digital lending platform for SME and retail customers", portfolio: "portfolio-2", program: "program-5", status: "Active", health: "amber", progress: 41, plannedFinish: "2025-10-31", forecastFinish: "2025-12-31", budget: 16_800_000, forecast: 17_500_000, actualCost: 6_900_000, scheduleVariance: -10.5, costVariance: -4.2, criticalRisks: 3, projectManager: "user-16", sponsor: "user-2", businessUnit: "Digital Banking", priority: "High", methodology: "Agile", phase: "Development", startDate: "2024-08-01", completionDate: "2025-10-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-8", name: "Network Security Upgrade", code: "NSU-2024", description: "Zero-trust network architecture implementation across all sites", portfolio: "portfolio-3", program: "program-3", status: "Active", health: "amber", progress: 71, plannedFinish: "2025-04-30", forecastFinish: "2025-05-30", budget: 11_200_000, forecast: 11_800_000, actualCost: 8_100_000, scheduleVariance: -6.7, costVariance: -5.4, criticalRisks: 2, projectManager: "user-17", sponsor: "user-3", businessUnit: "Cyber Security", priority: "Critical", methodology: "Waterfall", phase: "Testing", startDate: "2024-02-01", completionDate: "2025-04-30", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-9", name: "HR & Payroll Transformation", code: "HRP-2025", description: "Replace legacy HR systems with cloud HCM platform", portfolio: "portfolio-4", program: "program-4", status: "Active", health: "red", progress: 28, plannedFinish: "2025-11-30", forecastFinish: "2026-03-31", budget: 9_500_000, forecast: 11_200_000, actualCost: 3_200_000, scheduleVariance: -28.0, costVariance: -17.9, criticalRisks: 5, projectManager: "user-18", sponsor: "user-4", businessUnit: "Human Resources", priority: "High", methodology: "Hybrid", phase: "Execution", startDate: "2024-09-01", completionDate: "2025-11-30", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-10", name: "Open Banking Gateway", code: "OBG-2025", description: "PSD2/Open Banking compliant API gateway and developer portal", portfolio: "portfolio-2", program: "program-5", status: "Active", health: "red", progress: 32, plannedFinish: "2025-07-31", forecastFinish: "2025-10-31", budget: 13_200_000, forecast: 14_900_000, actualCost: 5_100_000, scheduleVariance: -22.0, costVariance: -12.9, criticalRisks: 4, projectManager: "user-19", sponsor: "user-2", businessUnit: "Digital Banking", priority: "High", methodology: "Agile", phase: "Development", startDate: "2024-06-01", completionDate: "2025-07-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-11", name: "Procurement Optimisation", code: "PO-2025", description: "AI-driven procurement and supply chain management platform", portfolio: "portfolio-1", program: "program-6", status: "At Risk", health: "red", progress: 38, plannedFinish: "2025-08-31", forecastFinish: "2025-11-30", budget: 8_200_000, forecast: 9_100_000, actualCost: 3_600_000, scheduleVariance: -20.0, costVariance: -11.0, criticalRisks: 4, projectManager: "user-20", sponsor: "user-1", businessUnit: "Procurement", priority: "Medium", methodology: "Agile", phase: "Development", startDate: "2024-11-01", completionDate: "2025-08-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-12", name: "Mobile Branch Platform", code: "MBP-2025", description: "Mobile-first digital branch with AI concierge and video banking", portfolio: "portfolio-2", program: "program-2", status: "At Risk", health: "amber", progress: 55, plannedFinish: "2025-05-31", forecastFinish: "2025-07-15", budget: 15_000_000, forecast: 15_800_000, actualCost: 8_900_000, scheduleVariance: -12.0, costVariance: -5.3, criticalRisks: 3, projectManager: "user-13", sponsor: "user-2", businessUnit: "Digital Banking", priority: "High", methodology: "Agile", phase: "Testing", startDate: "2024-03-01", completionDate: "2025-05-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-13", name: "Business Continuity Management", code: "BCM-2025", description: "Enterprise-wide business continuity and disaster recovery programme", portfolio: "portfolio-3", program: "program-3", status: "At Risk", health: "amber", progress: 44, plannedFinish: "2025-09-30", forecastFinish: "2025-11-15", budget: 7_800_000, forecast: 8_200_000, actualCost: 3_500_000, scheduleVariance: -8.0, costVariance: -5.1, criticalRisks: 2, projectManager: "user-17", sponsor: "user-3", businessUnit: "Risk Management", priority: "High", methodology: "Waterfall", phase: "Execution", startDate: "2024-10-01", completionDate: "2025-09-30", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-14", name: "Enterprise Data Lake", code: "EDL-2024", description: "Centralised data lake for analytics, reporting, and AI/ML workloads", portfolio: "portfolio-1", program: "program-6", status: "At Risk", health: "red", progress: 58, plannedFinish: "2025-03-31", forecastFinish: "2025-06-30", budget: 21_000_000, forecast: 22_500_000, actualCost: 13_200_000, scheduleVariance: -18.0, costVariance: -7.1, criticalRisks: 4, projectManager: "user-11", sponsor: "user-1", businessUnit: "Data & Analytics", priority: "Critical", methodology: "Hybrid", phase: "Testing", startDate: "2023-10-01", completionDate: "2025-03-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-15", name: "Cloud Contact Centre", code: "CCC-2025", description: "Migrate on-premise contact centre to cloud omnichannel platform", portfolio: "portfolio-3", program: "program-7", status: "At Risk", health: "amber", progress: 48, plannedFinish: "2025-07-31", forecastFinish: "2025-09-15", budget: 9_600_000, forecast: 10_100_000, actualCost: 4_800_000, scheduleVariance: -10.0, costVariance: -5.2, criticalRisks: 2, projectManager: "user-14", sponsor: "user-3", businessUnit: "Customer Operations", priority: "High", methodology: "Waterfall", phase: "Execution", startDate: "2024-05-01", completionDate: "2025-07-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-16", name: "ESG Reporting Framework", code: "ESG-2025", description: "Environmental, Social, and Governance data collection and reporting platform", portfolio: "portfolio-4", program: "program-4", status: "Delayed", health: "red", progress: 22, plannedFinish: "2025-12-31", forecastFinish: "2026-06-30", budget: 6_200_000, forecast: 7_500_000, actualCost: 1_400_000, scheduleVariance: -35.0, costVariance: -21.0, criticalRisks: 5, projectManager: "user-18", sponsor: "user-4", businessUnit: "Sustainability", priority: "Medium", methodology: "Agile", phase: "Planning", startDate: "2024-12-01", completionDate: "2025-12-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-17", name: "Payment Hub Modernisation", code: "PHM-2024", description: "Upgrade payments infrastructure to ISO 20022 with real-time capabilities", portfolio: "portfolio-1", program: "program-1", status: "Delayed", health: "amber", progress: 68, plannedFinish: "2025-01-31", forecastFinish: "2025-04-30", budget: 26_000_000, forecast: 27_200_000, actualCost: 18_500_000, scheduleVariance: -15.0, costVariance: -4.6, criticalRisks: 3, projectManager: "user-10", sponsor: "user-1", businessUnit: "Payments", priority: "Critical", methodology: "Waterfall", phase: "Testing", startDate: "2023-04-01", completionDate: "2025-01-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-18", name: "Identity & Access Management", code: "IAM-2025", description: "Implement zero-trust IAM with PAM, SSO, and MFA across all systems", portfolio: "portfolio-3", program: "program-7", status: "Delayed", health: "red", progress: 42, plannedFinish: "2025-05-31", forecastFinish: "2025-08-31", budget: 10_500_000, forecast: 11_400_000, actualCost: 4_900_000, scheduleVariance: -20.0, costVariance: -8.6, criticalRisks: 4, projectManager: "user-17", sponsor: "user-3", businessUnit: "Cyber Security", priority: "Critical", methodology: "Waterfall", phase: "Execution", startDate: "2024-06-01", completionDate: "2025-05-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-19", name: "Workforce Planning Tool", code: "WPT-2025", description: "Strategic workforce planning and skills gap analysis platform", portfolio: "portfolio-4", program: "program-4", status: "On Hold", health: "amber", progress: 15, plannedFinish: "2025-10-31", forecastFinish: "2026-02-28", budget: 4_800_000, forecast: 5_200_000, actualCost: 750_000, scheduleVariance: -40.0, costVariance: -8.3, criticalRisks: 2, projectManager: "user-15", sponsor: "user-4", businessUnit: "Human Resources", priority: "Low", methodology: "Agile", phase: "Planning", startDate: "2024-11-01", completionDate: "2025-10-31", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-20", name: "Trade Finance Platform", code: "TFP-2025", description: "Digitise trade finance operations with blockchain-based smart contracts", portfolio: "portfolio-1", program: "program-6", status: "On Hold", health: "amber", progress: 20, plannedFinish: "2025-11-30", forecastFinish: "2026-02-28", budget: 17_500_000, forecast: 18_200_000, actualCost: 3_500_000, scheduleVariance: -30.0, costVariance: -4.0, criticalRisks: 3, projectManager: "user-20", sponsor: "user-1", businessUnit: "Wholesale Banking", priority: "Medium", methodology: "Hybrid", phase: "Planning", startDate: "2024-12-01", completionDate: "2025-11-30", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-21", name: "Branch Transformation Programme", code: "BTP-2023", description: "Modernise branch network with digital kiosks and advisory hubs", portfolio: "portfolio-2", program: "program-5", status: "Completed", health: "green", progress: 100, plannedFinish: "2024-06-30", forecastFinish: "2024-06-30", budget: 24_000_000, forecast: 23_100_000, actualCost: 22_800_000, scheduleVariance: 0, costVariance: 3.8, criticalRisks: 0, projectManager: "user-16", sponsor: "user-2", businessUnit: "Retail Banking", priority: "High", methodology: "Waterfall", phase: "Closed", startDate: "2022-07-01", completionDate: "2024-06-30", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-22", name: "Legacy System Decommissioning", code: "LSD-2024", description: "Decommission 23 legacy applications and migrate data to archive", portfolio: "portfolio-3", program: "program-7", status: "Completed", health: "green", progress: 100, plannedFinish: "2024-12-31", forecastFinish: "2024-12-31", budget: 5_500_000, forecast: 5_200_000, actualCost: 5_100_000, scheduleVariance: 0, costVariance: 5.5, criticalRisks: 0, projectManager: "user-14", sponsor: "user-3", businessUnit: "Group IT", priority: "Medium", methodology: "Waterfall", phase: "Closed", startDate: "2023-01-01", completionDate: "2024-12-31", lastUpdate: "2026-07-29", source: "Internal" },
  { id: "proj-23", name: "Employee Experience Platform", code: "EXP-2024", description: "Unified employee portal with HR, IT, and facilities services", portfolio: "portfolio-4", program: "program-4", status: "Completed", health: "green", progress: 100, plannedFinish: "2024-09-30", forecastFinish: "2024-09-30", budget: 7_200_000, forecast: 6_900_000, actualCost: 6_800_000, scheduleVariance: 0, costVariance: 4.2, criticalRisks: 0, projectManager: "user-18", sponsor: "user-4", businessUnit: "Human Resources", priority: "Medium", methodology: "Agile", phase: "Closed", startDate: "2023-03-01", completionDate: "2024-09-30", lastUpdate: "2026-07-28", source: "Internal" },
  { id: "proj-24", name: "API Marketplace", code: "APIM-2023", description: "Internal API marketplace for system integration and ecosystem partners", portfolio: "portfolio-2", program: "program-2", status: "Completed", health: "green", progress: 100, plannedFinish: "2024-03-31", forecastFinish: "2024-03-31", budget: 8_800_000, forecast: 8_400_000, actualCost: 8_100_000, scheduleVariance: 0, costVariance: 7.9, criticalRisks: 0, projectManager: "user-12", sponsor: "user-2", businessUnit: "Group IT", priority: "Medium", methodology: "Agile", phase: "Closed", startDate: "2022-10-01", completionDate: "2024-03-31", lastUpdate: "2026-07-29", source: "Internal" },
];

export const mockUsers: User[] = [
  { id: "user-1", name: "Sarah Mitchell", email: "sarah.mitchell@nexus.com", role: "Executive", department: "Executive Office", jobTitle: "Chief Transformation Officer", phone: "+44 20 7123 4001", location: "London", manager: "" },
  { id: "user-2", name: "James Okonkwo", email: "james.okonkwo@nexus.com", role: "Executive", department: "Executive Office", jobTitle: "Chief Digital Officer", phone: "+44 20 7123 4002", location: "London", manager: "user-1" },
  { id: "user-3", name: "Priya Sharma", email: "priya.sharma@nexus.com", role: "Executive", department: "Executive Office", jobTitle: "Chief Technology Officer", phone: "+44 20 7123 4003", location: "Manchester", manager: "user-1" },
  { id: "user-4", name: "Michael Chen", email: "michael.chen@nexus.com", role: "Executive", department: "Executive Office", jobTitle: "Chief Operating Officer", phone: "+44 20 7123 4004", location: "London", manager: "" },
  { id: "user-5", name: "Emma Williams", email: "emma.williams@nexus.com", role: "Portfolio Manager", department: "PMO", jobTitle: "Portfolio Director - Strategic Initiatives", phone: "+44 20 7123 4010", location: "London", manager: "user-1" },
  { id: "user-6", name: "David Rodriguez", email: "david.rodriguez@nexus.com", role: "Portfolio Manager", department: "PMO", jobTitle: "Portfolio Director - Digital Transformation", phone: "+44 20 7123 4011", location: "London", manager: "user-2" },
  { id: "user-7", name: "Aisha Patel", email: "aisha.patel@nexus.com", role: "Portfolio Manager", department: "PMO", jobTitle: "Portfolio Director - Infrastructure", phone: "+44 20 7123 4012", location: "Manchester", manager: "user-3" },
  { id: "user-8", name: "Thomas Mueller", email: "thomas.mueller@nexus.com", role: "Portfolio Manager", department: "PMO", jobTitle: "Portfolio Director - Business Enablement", phone: "+44 20 7123 4013", location: "Edinburgh", manager: "user-4" },
  { id: "user-9", name: "Rachel Green", email: "rachel.green@nexus.com", role: "PMO Director", department: "PMO", jobTitle: "PMO Director", phone: "+44 20 7123 4020", location: "London", manager: "user-1" },
  { id: "user-10", name: "Daniel O'Brien", email: "daniel.obrien@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Senior Project Manager", phone: "+44 20 7123 4030", location: "London", manager: "user-9" },
  { id: "user-11", name: "Sophie Turner", email: "sophie.turner@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Senior Project Manager", phone: "+44 20 7123 4031", location: "Birmingham", manager: "user-9" },
  { id: "user-12", name: "Raj Kapoor", email: "raj.kapoor@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4032", location: "London", manager: "user-9" },
  { id: "user-13", name: "Liam O'Connor", email: "liam.oconnor@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4033", location: "Dublin", manager: "user-9" },
  { id: "user-14", name: "Nadia Khan", email: "nadia.khan@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Senior Project Manager", phone: "+44 20 7123 4034", location: "Manchester", manager: "user-9" },
  { id: "user-15", name: "Carlos Mendez", email: "carlos.mendez@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4035", location: "London", manager: "user-9" },
  { id: "user-16", name: "Yuki Tanaka", email: "yuki.tanaka@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4036", location: "London", manager: "user-9" },
  { id: "user-17", name: "Ahmed Hassan", email: "ahmed.hassan@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4037", location: "Birmingham", manager: "user-9" },
  { id: "user-18", name: "Olivia Brown", email: "olivia.brown@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4038", location: "Edinburgh", manager: "user-9" },
  { id: "user-19", name: "Wei Zhang", email: "wei.zhang@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4039", location: "London", manager: "user-9" },
  { id: "user-20", name: "Fatima Al-Sayed", email: "fatima.alsayed@nexus.com", role: "Project Manager", department: "PMO", jobTitle: "Project Manager", phone: "+44 20 7123 4040", location: "London", manager: "user-9" },
  { id: "user-21", name: "Jessica Taylor", email: "jessica.taylor@nexus.com", role: "Business Analyst", department: "PMO", jobTitle: "Senior Business Analyst", phone: "+44 20 7123 4050", location: "London", manager: "user-9" },
];

for (let i = 22; i <= 80; i++) {
  const firstNames = ["Alice", "Bob", "Charlie", "Diana", "Edward", "Fiona", "George", "Hannah", "Ivan", "Julia", "Kevin", "Laura", "Mark", "Nina", "Oscar", "Paula", "Quinn", "Rita", "Sam", "Tina", "Uma", "Victor", "Wendy", "Xavier", "Yara", "Zack", "Adam", "Bella", "Chris", "Daisy", "Evan", "Grace", "Henry", "Iris", "Jack", "Kate", "Leo", "Mia", "Noah", "Piper", "Ryan", "Stella", "Tom", "Uma", "Vince", "Willa", "Xander", "Yvonne", "Zane", "Aria", "Blake", "Cora", "Dylan", "Eden", "Finn", "Gwen", "Hank", "Isla", "Jade", "Kurt", "Luna", "Miles", "Nora", "Owen", "Pete", "Rosa", "Seth", "Tara", "Ulysses", "Vera", "Wade", "Xena", "Yves", "Zara"];
  const lastNames = ["Anderson", "Baker", "Clark", "Davis", "Evans", "Fox", "Garcia", "Hill", "Irvine", "Jones", "Kim", "Lee", "Martinez", "Nelson", "Ortega", "Parker", "Quinn", "Reed", "Smith", "Taylor", "Upton", "Vega", "Walker", "Xu", "Young", "Zimmerman"];
  const roles = ["Team Member", "Team Lead", "Developer", "Tester", "Designer", "DevOps Engineer", "Security Analyst", "Data Engineer", "Scrum Master", "Technical Architect", "Solution Architect", "Business Analyst"];
  const depts = ["Engineering", "Digital", "Infrastructure", "Security", "Data", "Architecture", "Quality Assurance", "DevOps", "Product", "Design"];
  const locs = ["London", "Manchester", "Edinburgh", "Birmingham", "Dublin", "Glasgow", "Bristol", "Leeds", "Cardiff", "Belfast"];

  const fi = i - 22;
  const fn = fi % firstNames.length;
  const ln = fi % lastNames.length;
  mockUsers.push({
    id: `user-${i}`,
    name: `${firstNames[fn]!} ${lastNames[ln]!}`,
    email: `${firstNames[fn]!.toLowerCase()}.${lastNames[ln]!.toLowerCase()}@nexus.com`,
    role: roles[fi % roles.length]!,
    department: depts[fi % depts.length]!,
    jobTitle: roles[fi % roles.length]!,
    phone: `+44 20 7123 4${String(100 + fi).slice(1)}`,
    location: locs[fi % locs.length]!,
    manager: `user-${9 + (fi % 12)}`,
  });
}

export const mockResources: Resource[] = [
  { id: "res-1", name: "Daniel O'Brien", email: "daniel.obrien@nexus.com", role: "Project Manager", skills: ["Project Management", "Risk Management", "Agile", "Waterfall"], dailyRate: 850, currency: "GBP", availability: 80, allocated: 80, department: "PMO", location: "London", type: "Internal" },
  { id: "res-2", name: "Sophie Turner", email: "sophie.turner@nexus.com", role: "Project Manager", skills: ["Project Management", "SAP", "ERP", "Cloud Migration"], dailyRate: 820, currency: "GBP", availability: 90, allocated: 90, department: "PMO", location: "Birmingham", type: "Internal" },
  { id: "res-3", name: "Raj Kapoor", email: "raj.kapoor@nexus.com", role: "Technical Architect", skills: ["Java", "Microservices", "Cloud Architecture", "AWS", "Kubernetes"], dailyRate: 950, currency: "GBP", availability: 70, allocated: 70, department: "Architecture", location: "London", type: "Internal" },
  { id: "res-4", name: "James Wilson", email: "james.wilson@nexus.com", role: "Senior Developer", skills: ["React", "TypeScript", "Node.js", "GraphQL", "PostgreSQL"], dailyRate: 700, currency: "GBP", availability: 100, allocated: 100, department: "Engineering", location: "Manchester", type: "Internal" },
  { id: "res-5", name: "Lisa Kumar", email: "lisa.kumar@nexus.com", role: "UX Designer", skills: ["Figma", "User Research", "Prototyping", "Design Systems"], dailyRate: 600, currency: "GBP", availability: 60, allocated: 60, department: "Design", location: "London", type: "Internal" },
  { id: "res-6", name: "Robert Davies", email: "robert.davies@nexus.com", role: "DevOps Engineer", skills: ["Docker", "Kubernetes", "Terraform", "CI/CD", "Azure"], dailyRate: 750, currency: "GBP", availability: 80, allocated: 70, department: "DevOps", location: "Edinburgh", type: "Internal" },
  { id: "res-7", name: "Maria Garcia", email: "maria.garcia@nexus.com", role: "Data Engineer", skills: ["Python", "Spark", "Snowflake", "Airflow", "dbt"], dailyRate: 720, currency: "GBP", availability: 90, allocated: 90, department: "Data", location: "London", type: "Internal" },
  { id: "res-8", name: "Tom Harris", email: "tom.harris@nexus.com", role: "Security Analyst", skills: ["Penetration Testing", "Zero Trust", "IAM", "SIEM", "DLP"], dailyRate: 780, currency: "GBP", availability: 70, allocated: 70, department: "Security", location: "Birmingham", type: "Internal" },
  { id: "res-9", name: "Anna Johansson", email: "anna.johansson@nexus.com", role: "Scrum Master", skills: ["Agile", "Scrum", "SAFe", "Team Coaching", "Facilitation"], dailyRate: 650, currency: "GBP", availability: 100, allocated: 100, department: "PMO", location: "London", type: "Internal" },
  { id: "res-10", name: "Peter Chang", email: "peter.chang@nexus.com", role: "QA Lead", skills: ["Test Automation", "Selenium", "Cypress", "Performance Testing", "API Testing"], dailyRate: 680, currency: "GBP", availability: 80, allocated: 80, department: "Quality Assurance", location: "Manchester", type: "Internal" },
];

const skillsPool = ["Java", "Python", "React", "Angular", "TypeScript", "Node.js", ".NET", "SQL", "MongoDB", "AWS", "Azure", "GCP", "Docker", "Kubernetes", "Terraform", "Ansible", "Jenkins", "GitLab CI", "Selenium", "Cypress", "JMeter", "Kafka", "RabbitMQ", "Redis", "Elasticsearch", "Tableau", "Power BI", "Snowflake", "Databricks", "SAP", "Salesforce", "ServiceNow", "UI Path", "Figma", "Adobe XD", "Jira", "Confluence", "SAFe", "ITIL", "TOGAF"];

for (let i = 11; i <= 120; i++) {
  const roles = ["Developer", "Senior Developer", "Tester", "Business Analyst", "System Analyst", "Data Analyst", "Network Engineer", "Cloud Engineer", "Infrastructure Engineer", "Database Administrator", "Solutions Architect", "Technical Writer"];
  const depts = ["Engineering", "Digital", "Infrastructure", "Security", "Data", "Architecture", "Quality Assurance", "DevOps", "Product", "Design", "Networks", "Cloud"];
  const locs = ["London", "Manchester", "Edinburgh", "Birmingham", "Dublin", "Glasgow", "Bristol", "Leeds", "Cardiff", "Belfast", "Liverpool", "Newcastle"];
  const numSkills = 2 + (i % 4);
  const skills: string[] = [];
  for (let s = 0; s < numSkills; s++) {
    skills.push(skillsPool[(i * 7 + s * 13) % skillsPool.length]);
  }
  mockResources.push({
    id: `res-${i}`,
    name: (mockUsers[i < mockUsers.length ? i : (i % 79) + 1] ?? mockUsers[1]!).name,
    email: `resource${i}@nexus.com`,
    role: roles[i % roles.length],
    skills,
    dailyRate: 400 + (i % 30) * 20,
    currency: "GBP",
    availability: 40 + (i % 60),
    allocated: 30 + (i % 50),
    department: depts[i % depts.length],
    location: locs[i % locs.length],
    type: i > 105 ? "Outsourced" : "Internal",
    ...(i > 105 ? { vendor: ["TechMahindra", "Infosys", "Wipro", "TCS", "HCL"][i % 5] } : {}),
  });
}

export const mockTasks: Task[] = [];
const taskNames = [
  "Requirements gathering", "System architecture design", "Database schema design", "API specification", "Frontend development", "Backend development", "Integration testing", "Unit testing", "User acceptance testing", "Deployment to staging", "Performance optimisation", "Security review", "Documentation", "Production deployment", "Sprint planning", "Daily standup", "Code review", "Sprint retrospective", "Data migration", "User training", "Go-live preparation", "Post-implementation support", "Risk assessment workshop", "Stakeholder presentation", "Vendor evaluation", "Proof of concept", "Technical design document", "Environment setup", "Continuous integration setup", "Monitoring setup",
];

for (let t = 0; t < 320; t++) {
  const projIdx = t % mockProjects.length;
  const project = mockProjects[projIdx]!;
  const assigneeIdx = 10 + (t % 12);
  const statuses: Task["status"][] = ["Not Started", "In Progress", "Completed", "Blocked"];
  const priorities: Task["priority"][] = ["Critical", "High", "Medium", "Low"];
  const tName = t % taskNames.length;
  mockTasks.push({
    id: `task-${t + 1}`,
    projectId: project.id,
    title: taskNames[tName]!,
    description: `Task description for ${taskNames[tName]!}`,
    assignee: (mockUsers[assigneeIdx < mockUsers.length ? assigneeIdx : 10] ?? mockUsers[10]!).id,
    status: statuses[t % 4],
    priority: priorities[t % 4],
    startDate: new Date(2024, 0, 1 + t * 2).toISOString().split("T")[0],
    endDate: new Date(2025, 0, 1 + t * 4).toISOString().split("T")[0],
    estimatedHours: 16 + (t % 20) * 8,
    actualHours: 12 + (t % 20) * 7,
    dependencies: t > 0 ? [`task-${t}`] : [],
    isMilestone: t % 8 === 0,
    isShared: t % 9 === 0,
    deliverables: t % 7 === 0 ? ["Deliverable document"] : [],
  });
}

for (let st = 0; st < 35; st++) {
  const statuses: Task["status"][] = ["Not Started", "In Progress", "Completed", "Blocked"];
  const priorities: Task["priority"][] = ["Critical", "High", "Medium", "Low"];
  const stName = st % taskNames.length;
  mockTasks.push({
    id: `task-shared-${st + 1}`,
    projectId: mockProjects[st % mockProjects.length]!.id,
    title: `Shared: ${taskNames[stName]!}`,
    description: `Shared task across projects`,
    assignee: (mockUsers[10 + (st % 12)] ?? mockUsers[10]!).id,
    status: statuses[st % 4],
    priority: priorities[st % 4],
    startDate: new Date(2024, 0, 1 + st * 3).toISOString().split("T")[0],
    endDate: new Date(2025, 0, 1 + st * 5).toISOString().split("T")[0],
    estimatedHours: 24 + (st % 15) * 8,
    actualHours: 20 + (st % 15) * 7,
    dependencies: [],
    isMilestone: false,
    isShared: true,
    deliverables: [],
  });
}

function generateTasks() {
  const allTasks = [...mockTasks];
  const milestoneNames = ["Phase 1 Complete", "Phase 2 Complete", "Architecture Sign-off", "UAT Complete", "Go/No-Go Decision", "Steering Committee Approval", "Budget Approval", "Resource Plan Sign-off", "Design Freeze", "Code Complete", "Integration Complete", "Security Clearance", "Data Migration Complete", "Training Complete", "Launch Approval"];
  for (let m = 0; m < 42; m++) {
    allTasks.push({
      id: `milestone-${m + 1}`,
      projectId: mockProjects[m % mockProjects.length]!.id,
      title: `Milestone: ${milestoneNames[m % 15]!}`,
      description: `Key project milestone`,
      assignee: (mockUsers[10 + (m % 12)] ?? mockUsers[10]!).id,
      status: m < 20 ? "Completed" : "In Progress",
      priority: "Critical",
      startDate: new Date(2024, 0, 1 + m * 5).toISOString().split("T")[0],
      endDate: new Date(2025, 0, 1 + m * 5).toISOString().split("T")[0],
      estimatedHours: 0,
      actualHours: 0,
      dependencies: [],
      isMilestone: true,
      isShared: false,
      deliverables: [],
    });
  }
  return allTasks;
}

export const mockRisks: Risk[] = [
  { id: "risk-1", projectId: "proj-1", title: "Core banking system integration failure", description: "Integration between new microservices and existing core may fail during cutover", probability: 4, impact: 5, score: 20, category: "Technical", owner: "user-10", status: "Open", mitigationPlan: "Run parallel simulation for 4 weeks before cutover", contingencyPlan: "Rollback to previous version within 2 hours", createdAt: "2024-02-15", updatedAt: "2026-07-20" },
  { id: "risk-2", projectId: "proj-1", title: "Regulatory compliance gap in new platform", description: "New platform may not fully comply with upcoming FCA regulations", probability: 3, impact: 5, score: 15, category: "Regulatory", owner: "user-10", status: "Mitigating", mitigationPlan: "Engage regulatory consultant for gap analysis", contingencyPlan: "Phased rollout with manual compliance checks", createdAt: "2024-03-01", updatedAt: "2026-07-25" },
  { id: "risk-3", projectId: "proj-3", title: "AI model accuracy below threshold", description: "Risk prediction models may not meet 95% accuracy target", probability: 4, impact: 4, score: 16, category: "Technical", owner: "user-12", status: "Open", mitigationPlan: "Expand training dataset and feature engineering", contingencyPlan: "Deploy with human-in-the-loop override", createdAt: "2024-11-01", updatedAt: "2026-07-28" },
  { id: "risk-4", projectId: "proj-4", title: "Omni-channel platform performance issues", description: "Platform may not handle peak load of 50k concurrent users", probability: 3, impact: 4, score: 12, category: "Performance", owner: "user-13", status: "Open", mitigationPlan: "Conduct load testing at 2x expected capacity", contingencyPlan: "Auto-scaling with CDN and caching layer", createdAt: "2024-05-15", updatedAt: "2026-07-22" },
  { id: "risk-5", projectId: "proj-6", title: "Regulatory reporting deadline change", description: "FCA may introduce new reporting requirements mid-project", probability: 3, impact: 4, score: 12, category: "Regulatory", owner: "user-15", status: "Mitigating", mitigationPlan: "Build flexible reporting engine with configurable rules", contingencyPlan: "Dedicated team to handle scope changes", createdAt: "2024-08-01", updatedAt: "2026-07-26" },
  { id: "risk-6", projectId: "proj-9", title: "HR data migration data quality issues", description: "Legacy HR systems have poor data quality affecting migration", probability: 5, impact: 4, score: 20, category: "Data", owner: "user-18", status: "Open", mitigationPlan: "Early data profiling and cleansing phase", contingencyPlan: "Parallel running for 3 months post go-live", createdAt: "2024-10-01", updatedAt: "2026-07-29" },
  { id: "risk-7", projectId: "proj-14", title: "Data lake scalability constraints", description: "Current data lake architecture may not scale to petabyte volumes", probability: 3, impact: 5, score: 15, category: "Technical", owner: "user-11", status: "Open", mitigationPlan: "Implement data partitioning and tiered storage", contingencyPlan: "Migrate to Snowflake as alternative", createdAt: "2024-01-15", updatedAt: "2026-07-24" },
  { id: "risk-8", projectId: "proj-17", title: "ISO 20022 migration timeline risk", description: "Industry-wide migration to ISO 20022 may cause payment processing delays", probability: 4, impact: 5, score: 20, category: "Market", owner: "user-10", status: "Mitigating", mitigationPlan: "Early adoption with phased industry testing", contingencyPlan: "Dual protocol support during transition", createdAt: "2023-06-01", updatedAt: "2026-07-27" },
  { id: "risk-9", projectId: "proj-10", title: "Open banking API security vulnerability", description: "PSD2 APIs may expose security vulnerabilities in third-party integrations", probability: 3, impact: 5, score: 15, category: "Security", owner: "user-19", status: "Open", mitigationPlan: "Regular penetration testing and security audits", contingencyPlan: "Kill switch for third-party API access", createdAt: "2024-07-01", updatedAt: "2026-07-28" },
  { id: "risk-10", projectId: "proj-18", title: "IAM vendor lock-in risk", description: "Selected IAM vendor may have limited interoperability", probability: 2, impact: 4, score: 8, category: "Vendor", owner: "user-17", status: "Open", mitigationPlan: "Build abstraction layer for multi-vendor support", contingencyPlan: "RFP for alternative vendors", createdAt: "2024-07-15", updatedAt: "2026-07-20" },
  { id: "risk-11", projectId: "proj-13", title: "Business continuity testing gaps", description: "BCM testing may not cover all critical scenarios", probability: 3, impact: 4, score: 12, category: "Operational", owner: "user-17", status: "Mitigating", mitigationPlan: "Comprehensive test scenario workshop with all BUs", contingencyPlan: "Quarterly tabletop exercises", createdAt: "2024-11-01", updatedAt: "2026-07-25" },
  { id: "risk-12", projectId: "proj-16", title: "ESG data availability from suppliers", description: "Suppliers may not provide required ESG data in time", probability: 4, impact: 3, score: 12, category: "Supply Chain", owner: "user-18", status: "Open", mitigationPlan: "Supplier engagement programme with incentives", contingencyPlan: "Use estimated data with regulatory disclosure", createdAt: "2025-01-01", updatedAt: "2026-07-29" },
];

for (let r = 13; r <= 28; r++) {
  const riskTypes = ["Technical", "Operational", "Financial", "Regulatory", "Market", "Security", "Data", "Vendor", "Resource", "Strategic"];
  const rstatuses: Risk["status"][] = ["Open", "Mitigating", "Closed"];
  const riskTitles = ["System outage", "Budget overrun", "Scope creep", "Staff turnover", "Vendor delay", "Technology obsolescence", "Data breach", "Compliance gap", "Integration failure", "Performance degradation", "Requirements change", "Testing gap", "Deployment failure", "Documentation gap", "Knowledge loss", "Contract dispute"];
  mockRisks.push({
    id: `risk-${r}`,
    projectId: `proj-${(r % 17) + 1}`,
    title: `Risk item ${r}: ${riskTitles[r % riskTitles.length]!}`,
    description: `Description for risk item ${r}`,
    probability: 2 + (r % 4),
    impact: 2 + (r % 4),
    score: (2 + (r % 4)) * (2 + (r % 4)),
    category: riskTypes[r % riskTypes.length]!,
    owner: (mockUsers[10 + (r % 10)] ?? mockUsers[10]!).id,
    status: rstatuses[r % 3]!,
    mitigationPlan: `Mitigation plan for risk ${r}`,
    contingencyPlan: `Contingency plan for risk ${r}`,
    createdAt: new Date(2024, 0, 1 + r * 10).toISOString().split("T")[0],
    updatedAt: new Date(2026, 6, 1 + r).toISOString().split("T")[0],
  });
}

export const mockIssues: Issue[] = [
  { id: "issue-1", projectId: "proj-4", title: "API Gateway performance bottleneck", description: "API Gateway maxing out at 15k TPS, needs 50k TPS", severity: "Critical", status: "In Progress", owner: "user-13", createdAt: "2024-09-15", resolvedAt: undefined },
  { id: "issue-2", projectId: "proj-10", title: "Developer portal authentication issue", description: "OAuth flow broken for third-party developers", severity: "High", status: "Open", owner: "user-19", createdAt: "2025-01-10", resolvedAt: undefined },
  { id: "issue-3", projectId: "proj-6", title: "Compliance report generation failure", description: "Automated report generation crashes for Q1 data", severity: "Critical", status: "In Progress", owner: "user-15", createdAt: "2025-02-20", resolvedAt: undefined },
  { id: "issue-4", projectId: "proj-9", title: "HR data mapping discrepancies", description: "Employee data fields not mapping correctly between legacy and new system", severity: "High", status: "Open", owner: "user-18", createdAt: "2025-03-05", resolvedAt: undefined },
  { id: "issue-5", projectId: "proj-14", title: "Data pipeline latency exceeds SLA", description: "Data lake ingestion pipeline latencies > 30 minutes", severity: "High", status: "In Progress", owner: "user-11", createdAt: "2024-12-01", resolvedAt: undefined },
  { id: "issue-6", projectId: "proj-17", title: "Test environment availability conflict", description: "Test environments double-booked across squads", severity: "Medium", status: "Resolved", owner: "user-10", createdAt: "2024-11-10", resolvedAt: "2024-11-20" },
  { id: "issue-7", projectId: "proj-1", title: "Database migration timeout error", description: "Large-volume data migration timing out after 2 hours", severity: "High", status: "In Progress", owner: "user-10", createdAt: "2025-04-15", resolvedAt: undefined },
  { id: "issue-8", projectId: "proj-18", title: "SSL certificate renewal failure", description: "Wildcard SSL cert expired causing service disruptions", severity: "Critical", status: "Open", owner: "user-17", createdAt: "2025-05-20", resolvedAt: undefined },
  { id: "issue-9", projectId: "proj-3", title: "GPU resource shortage for ML training", description: "Insufficient GPU capacity for model training schedule", severity: "Medium", status: "Resolved", owner: "user-12", createdAt: "2024-11-15", resolvedAt: "2025-01-10" },
  { id: "issue-10", projectId: "proj-11", title: "Procurement approval workflow broken", description: "Automated approval workflow not routing to correct managers", severity: "High", status: "Open", owner: "user-20", createdAt: "2025-06-01", resolvedAt: undefined },
];

for (let iss = 11; iss <= 17; iss++) {
  const severities: Issue["severity"][] = ["Critical", "High", "Medium", "Low"];
  const istatuses: Issue["status"][] = ["Open", "In Progress", "Resolved", "Closed"];
  const iTitles = ["Resource conflict", "Budget freeze", "Stakeholder disagreement", "Tooling limitation", "Network outage", "Third-party API deprecation", "Documentation gap"];
  mockIssues.push({
    id: `issue-${iss}`,
    projectId: `proj-${(iss % 18) + 1}`,
    title: `Issue ${iss}: ${iTitles[iss % iTitles.length]!}`,
    description: `Description for issue ${iss}`,
    severity: severities[iss % 4]!,
    status: istatuses[iss % 4]!,
    owner: (mockUsers[10 + (iss % 10)] ?? mockUsers[10]!).id,
    createdAt: new Date(2025, 0, iss * 5).toISOString().split("T")[0],
    resolvedAt: iss % 3 === 0 ? new Date(2025, 2, iss * 3).toISOString().split("T")[0] : undefined,
  });
}

export const mockMeetings: Meeting[] = [];
const meetingTitles = [
  "Steering Committee - Core Banking Modernisation",
  "Sprint Review - Risk Analytics Platform",
  "Portfolio Governance Board",
  "Risk Working Group",
  "Project Status Update - Omni-Channel Hub",
  "Architecture Review Board",
  "Resource Planning Meeting",
  "Change Control Board",
  "Vendor Selection Panel",
  "Executive Dashboard Review",
  "PMO Monthly Review",
  "Budget Review - Q3",
  "Security Incident Review",
  "Stakeholder Update - Digital Transformation",
  "Lessons Learned Workshop",
  "Technical Design Review",
  "UAT Sign-off Meeting",
  "Sprint Planning - AI Workspace",
  "Cross-Program Dependency Meeting",
  "Regulatory Compliance Review",
  "Team Retrospective",
  "Go/No-Go Decision Meeting",
];

for (let m = 0; m < 22; m++) {
  const meetingTypes = ["Workshop", "Review", "Ceremony", "Board", "Standup"];
  const locations = ["Board Room A", "Virtual - Teams", "Conference Room 3", "Virtual - Zoom", "Innovation Lab"];
  const mstatuses: Meeting["status"][] = ["Scheduled", "In Progress", "Completed", "Cancelled"];
  mockMeetings.push({
    id: `meeting-${m + 1}`,
    projectId: `proj-${(m % 18) + 1}`,
    title: meetingTitles[m % meetingTitles.length]!,
    description: `${meetingTypes[m % 5]!} session for project status and governance`,
    startDate: new Date(2025, 0, 15 + m * 14).toISOString().split("T")[0],
    endDate: new Date(2025, 0, 15 + m * 14).toISOString().split("T")[0],
    location: locations[m % 5]!,
    type: meetingTypes[m % 5]!,
    attendees: mockUsers.slice(0, 8 + (m % 5)).map((u) => u.id),
    createdBy: (mockUsers[10 + (m % 10)] ?? mockUsers[10]!).id,
    status: mstatuses[m % 4]!,
  });
}

export const mockMeetingActions: MeetingAction[] = [];
const actionDescriptions = [
  "Prepare risk mitigation plan for regulatory compliance gap",
  "Update project schedule with revised milestones",
  "Engage vendor for security audit",
  "Draft stakeholder communication plan",
  "Complete data quality assessment",
  "Review and approve architecture design document",
  "Schedule user acceptance testing sessions",
  "Update budget forecast for Q4",
  "Resolve API gateway performance issue",
  "Complete data migration dry run",
  "Finalise resource allocation plan",
  "Conduct lessons learned workshop",
  "Update risk register with new identified risks",
  "Prepare executive summary for board meeting",
  "Complete vendor evaluation scorecard",
  "Draft test closure report",
  "Review change request impact assessment",
  "Update training materials",
  "Complete security clearance process",
  "Finalise go-live checklist",
];

for (let a = 0; a < 46; a++) {
  const astatuses: MeetingAction["status"][] = ["Open", "In Progress", "Completed"];
  mockMeetingActions.push({
    id: `action-${a + 1}`,
    meetingId: `meeting-${(a % 22) + 1}`,
    description: actionDescriptions[a % actionDescriptions.length]!,
    owner: (mockUsers[10 + (a % 10)] ?? mockUsers[10]!).id,
    dueDate: new Date(2025, 0, 1 + a * 7).toISOString().split("T")[0],
    status: astatuses[a % 3]!,
  });
}

export const mockApprovals: Approval[] = [
  { id: "appr-1", type: "Project Charter", title: "Core Banking Modernisation - Phase 2 Charter", description: "Approval required for Phase 2 scope and budget", requestedBy: "user-10", requestedAt: "2026-07-25", status: "Pending", urgency: "High" },
  { id: "appr-2", type: "Budget Change", title: "ERP Cloud Migration - Budget Revision", description: "Request for additional £1.2M for data migration scope", requestedBy: "user-11", requestedAt: "2026-07-24", status: "Pending", urgency: "High" },
  { id: "appr-3", type: "Resource Request", title: "Additional Developers for Omni-Channel Hub", description: "Request for 3 senior React developers for 6 months", requestedBy: "user-13", requestedAt: "2026-07-23", status: "Pending", urgency: "Medium" },
  { id: "appr-4", type: "Vendor Selection", title: "Cloud Provider Selection - AI Platform", description: "AWS vs Azure final vendor recommendation", requestedBy: "user-12", requestedAt: "2026-07-22", status: "Pending", urgency: "Medium" },
  { id: "appr-5", type: "Milestone Sign-off", title: "Core Banking - Architecture Sign-off", description: "Final architecture design approval for core banking platform", requestedBy: "user-10", requestedAt: "2026-07-20", status: "Approved", urgency: "High" },
  { id: "appr-6", type: "Change Request", title: "Scope Change - Regulatory Compliance Module", description: "Addition of new FCA reporting requirements", requestedBy: "user-15", requestedAt: "2026-07-19", status: "Rejected", urgency: "Low" },
  { id: "appr-7", type: "Project Closure", title: "Data Centre Consolidation - Closure Report", description: "Final project closure documentation and handover", requestedBy: "user-14", requestedAt: "2026-07-18", status: "Approved", urgency: "Low" },
];

for (let a = 8; a <= 18; a++) {
  const types = ["Project Charter", "Budget Change", "Resource Request", "Vendor Selection", "Milestone Sign-off", "Change Request", "Project Closure", "Risk Acceptance", "Exception Request", "Contract Approval"];
  const astatuses: Approval["status"][] = ["Pending", "Approved", "Rejected"];
  const aurgs: Approval["urgency"][] = ["Low", "Medium", "High"];
  mockApprovals.push({
    id: `appr-${a}`,
    type: types[a % types.length]!,
    title: `Approval Request ${a}: ${types[a % types.length]!}`,
    description: `Description for approval request ${a}`,
    requestedBy: (mockUsers[10 + (a % 10)] ?? mockUsers[10]!).id,
    requestedAt: new Date(2026, 6, a).toISOString().split("T")[0],
    status: astatuses[a % 3]!,
    urgency: aurgs[a % 3]!,
  });
}

export const mockTickets: Ticket[] = [
  { id: "ticket-1", projectId: "proj-4", title: "Login page crashes on Safari", description: "Login page throws JS error on Safari 17.2", priority: "High", status: "Open", assignee: "user-24", createdAt: "2026-07-25" },
  { id: "ticket-2", projectId: "proj-1", title: "Database connection pool exhaustion", description: "Connection pool maxing out under load test", priority: "Critical", status: "In Progress", assignee: "user-28", createdAt: "2026-07-24" },
  { id: "ticket-3", projectId: "proj-3", title: "ML model training pipeline fails", description: "Training pipeline crashes with OOM error on GPU nodes", priority: "High", status: "Open", assignee: "user-31", createdAt: "2026-07-23" },
  { id: "ticket-4", projectId: "proj-6", title: "Report export generates wrong format", description: "PDF export contains HTML tags instead of formatted text", priority: "Medium", status: "Resolved", assignee: "user-35", createdAt: "2026-07-20" },
  { id: "ticket-5", projectId: "proj-10", title: "API rate limiting not working", description: "Rate limiting middleware not enforcing per-client limits", priority: "High", status: "In Progress", assignee: "user-38", createdAt: "2026-07-22" },
  { id: "ticket-6", projectId: "proj-14", title: "Data pipeline duplicate records", description: "ETL pipeline producing duplicate records for date partition", priority: "Medium", status: "Open", assignee: "user-40", createdAt: "2026-07-21" },
  { id: "ticket-7", projectId: "proj-7", title: "Mobile app push notifications not delivered", description: "iOS push notifications delayed by > 30 minutes", priority: "High", status: "Open", assignee: "user-42", createdAt: "2026-07-19" },
  { id: "ticket-8", projectId: "proj-18", title: "SSO token expiry not handled gracefully", description: "Users not redirected to login on token expiry", priority: "Low", status: "Closed", assignee: "user-45", createdAt: "2026-07-15" },
];

for (let t = 9; t <= 12; t++) {
  const tpriorities: Ticket["priority"][] = ["Low", "Medium", "High", "Critical"];
  const tstatuses: Ticket["status"][] = ["Open", "In Progress", "Resolved", "Closed"];
  const tTitles = ["UI rendering bug", "API timeout", "Data inconsistency", "Search not working"];
  mockTickets.push({
    id: `ticket-${t}`,
    projectId: `proj-${(t % 18) + 1}`,
    title: `Ticket ${t}: ${tTitles[t % 4]!}`,
    description: `Description for ticket ${t}`,
    priority: tpriorities[t % 4]!,
    status: tstatuses[t % 4]!,
    assignee: (mockUsers[20 + (t % 10)] ?? mockUsers[20]!).id,
    createdAt: new Date(2026, 6, t * 2).toISOString().split("T")[0],
  });
}

export const mockTestCases: TestCase[] = [
  { id: "tc-1", projectId: "proj-1", title: "User login with valid credentials", description: "Verify user can login with valid username and password", status: "Passed", priority: "Critical", assignedTo: "user-45", lastRun: "2026-07-28" },
  { id: "tc-2", projectId: "proj-1", title: "Account balance display", description: "Verify account balance displays correctly for all account types", status: "Passed", priority: "Critical", assignedTo: "user-46", lastRun: "2026-07-27" },
  { id: "tc-3", projectId: "proj-1", title: "Fund transfer between accounts", description: "Verify fund transfer completes with correct balances", status: "Ready", priority: "High", assignedTo: "user-47", lastRun: "2026-07-25" },
  { id: "tc-4", projectId: "proj-3", title: "Risk score calculation accuracy", description: "Verify risk score matches expected output for test scenarios", status: "Failed", priority: "Critical", assignedTo: "user-48", lastRun: "2026-07-26" },
  { id: "tc-5", projectId: "proj-3", title: "Model training pipeline execution", description: "Verify ML training pipeline completes successfully", status: "Ready", priority: "High", assignedTo: "user-49", lastRun: "2026-07-24" },
  { id: "tc-6", projectId: "proj-4", title: "Multi-channel customer profile sync", description: "Verify customer profile syncs across web, mobile, and branch", status: "Passed", priority: "High", assignedTo: "user-50", lastRun: "2026-07-28" },
  { id: "tc-7", projectId: "proj-4", title: "Product recommendation engine", description: "Verify AI recommendations are relevant and personalised", status: "Draft", priority: "Medium", assignedTo: "user-51", lastRun: "" },
  { id: "tc-8", projectId: "proj-6", title: "Regulatory report generation", description: "Verify FCA regulatory report is generated correctly", status: "Passed", priority: "Critical", assignedTo: "user-52", lastRun: "2026-07-27" },
  { id: "tc-9", projectId: "proj-6", title: "Compliance rule engine", description: "Verify compliance rules trigger correctly for edge cases", status: "Ready", priority: "High", assignedTo: "user-53", lastRun: "2026-07-23" },
  { id: "tc-10", projectId: "proj-7", title: "Loan application workflow", description: "Verify end-to-end loan application process", status: "Passed", priority: "Critical", assignedTo: "user-54", lastRun: "2026-07-26" },
  { id: "tc-11", projectId: "proj-7", title: "Credit scoring integration", description: "Verify credit score from external provider is correctly integrated", status: "Failed", priority: "High", assignedTo: "user-55", lastRun: "2026-07-25" },
  { id: "tc-12", projectId: "proj-10", title: "Open banking API conformance", description: "Verify APIs conform to PSD2/Open Banking standards", status: "Ready", priority: "Critical", assignedTo: "user-56", lastRun: "2026-07-24" },
  { id: "tc-13", projectId: "proj-14", title: "Data lake ingestion performance", description: "Verify data lake handles peak ingestion volumes", status: "Passed", priority: "High", assignedTo: "user-57", lastRun: "2026-07-22" },
  { id: "tc-14", projectId: "proj-17", title: "ISO 20022 message format validation", description: "Verify payment messages conform to ISO 20022 schema", status: "Draft", priority: "High", assignedTo: "user-58", lastRun: "" },
  { id: "tc-15", projectId: "proj-18", title: "Multi-factor authentication flow", description: "Verify MFA works across all access methods", status: "Ready", priority: "Critical", assignedTo: "user-59", lastRun: "2026-07-20" },
  { id: "tc-16", projectId: "proj-9", title: "Employee data import validation", description: "Verify bulk employee data import validates correctly", status: "Passed", priority: "High", assignedTo: "user-60", lastRun: "2026-07-19" },
  { id: "tc-17", projectId: "proj-11", title: "Purchase order approval workflow", description: "Verify PO approval routing and notifications work", status: "Ready", priority: "Medium", assignedTo: "user-61", lastRun: "2026-07-18" },
  { id: "tc-18", projectId: "proj-13", title: "DR failover testing", description: "Verify disaster recovery failover within RTO", status: "Failed", priority: "Critical", assignedTo: "user-62", lastRun: "2026-07-17" },
  { id: "tc-19", projectId: "proj-15", title: "Contact centre call routing", description: "Verify IVR and skill-based routing work correctly", status: "Passed", priority: "High", assignedTo: "user-63", lastRun: "2026-07-16" },
  { id: "tc-20", projectId: "proj-5", title: "Network latency monitoring", description: "Verify network monitoring alerts on latency thresholds", status: "Ready", priority: "Medium", assignedTo: "user-64", lastRun: "2026-07-15" },
];

export const allTasks = generateTasks();
