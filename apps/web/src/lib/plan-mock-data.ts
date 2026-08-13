// @ts-nocheck - Mock data for Project Plan POV prototype

export type TaskMode = "Auto" | "Manual";
export type ConstraintType = "As Soon As Possible" | "As Late As Possible" | "Must Start On" | "Must Finish On" | "Start No Earlier Than" | "Start No Later Than" | "Finish No Earlier Than" | "Finish No Later Than";
export type DependencyType = "FS" | "SS" | "FF" | "SF";
export type TaskType = "Summary" | "Task" | "Subtask" | "Milestone" | "Recurring" | "External" | "ApprovalGate" | "Deliverable" | "Meeting" | "JiraLinked" | "TicketLinked";

export interface PlanTask {
  id: string;
  parentId: string | null;
  wbs: string;
  name: string;
  type: TaskType;
  mode: TaskMode;
  duration: string;
  start: string;
  finish: string;
  actualStart: string | null;
  actualFinish: string | null;
  progress: number;
  predecessors: string[];
  successorIds: string[];
  resourceNames: string[];
  resources: PlanResourceRef[];
  work: number; // hours
  actualWork: number;
  remainingWork: number;
  cost: number;
  actualCost: number;
  remainingCost: number;
  plannedCost: number;
  forecastCost: number;
  committedCost: number;
  baselineStart: string | null;
  baselineFinish: string | null;
  baselineDuration: string | null;
  baselineWork: number | null;
  baselineCost: number | null;
  variance: number;
  totalSlack: number;
  freeSlack: number;
  constraintType: ConstraintType;
  constraintDate: string | null;
  deadline: string | null;
  priority: "Critical" | "High" | "Medium" | "Low";
  isCritical: boolean;
  status: "Not Started" | "In Progress" | "Completed" | "Delayed" | "On Hold";
  owner: string;
  source: string;
  externalId: string | null;
  externalKey: string | null;
  syncStatus: string | null;
  lastSync: string | null;
  externalLink: string | null;
  calendar: string;
  description: string;
  notes: string;
  isExpanded: boolean;
  children: PlanTask[];
  tags: string[];
}

export interface PlanResourceRef {
  id: string;
  name: string;
  role: string;
  allocation: number;
  type: "Internal" | "Outsourced";
  hourlyRate: number;
}

export interface PlanResource {
  id: string;
  name: string;
  role: string;
  email: string;
  type: "Internal" | "Outsourced";
  hourlyRate: number;
  department: string;
  maxAllocation: number;
}

export interface PlanSummary {
  totalTasks: number;
  completed: number;
  inProgress: number;
  notStarted: number;
  delayed: number;
  criticalCount: number;
  milestones: number;
  totalPlannedWork: number;
  totalActualWork: number;
  totalPlannedCost: number;
  totalForecastCost: number;
  plannedDuration: string;
  forecastDuration: string;
  scheduleVariance: number;
  lastBaselineDate: string;
}

function w(amount: number): string {
  return `£${amount.toLocaleString("en-GB")}`;
}

export const planResources: PlanResource[] = [
  { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", email: "daniel.obrien@nexus.com", type: "Internal", hourlyRate: 106, department: "PMO", maxAllocation: 100 },
  { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", email: "raj.kapoor@nexus.com", type: "Internal", hourlyRate: 119, department: "Architecture", maxAllocation: 100 },
  { id: "pr-3", name: "James Wilson", role: "Senior Developer", email: "james.wilson@nexus.com", type: "Internal", hourlyRate: 88, department: "Engineering", maxAllocation: 100 },
  { id: "pr-4", name: "Lisa Kumar", role: "UX Designer", email: "lisa.kumar@nexus.com", type: "Internal", hourlyRate: 75, department: "Design", maxAllocation: 100 },
  { id: "pr-5", name: "Maria Garcia", role: "Data Engineer", email: "maria.garcia@nexus.com", type: "Internal", hourlyRate: 90, department: "Data", maxAllocation: 100 },
  { id: "pr-6", name: "Tom Harris", role: "Security Analyst", email: "tom.harris@nexus.com", type: "Internal", hourlyRate: 98, department: "Security", maxAllocation: 100 },
  { id: "pr-7", name: "Peter Chang", role: "QA Lead", email: "peter.chang@nexus.com", type: "Internal", hourlyRate: 85, department: "Quality Assurance", maxAllocation: 100 },
  { id: "pr-8", name: "Anna Johansson", role: "Scrum Master", email: "anna.johansson@nexus.com", type: "Internal", hourlyRate: 81, department: "PMO", maxAllocation: 100 },
  { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", email: "delivery@nexusdigital.io", type: "Outsourced", hourlyRate: 65, department: "External", maxAllocation: 400 },
  { id: "pr-10", name: "Compliance Consultants Ltd", role: "Regulatory Advisor", email: "advisory@compliant.uk", type: "Outsourced", hourlyRate: 150, department: "External", maxAllocation: 100 },
  { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", email: "robert.davies@nexus.com", type: "Internal", hourlyRate: 94, department: "DevOps", maxAllocation: 100 },
  { id: "pr-12", name: "Sophie Turner", role: "Data Migration Lead", email: "sophie.turner@nexus.com", type: "Internal", hourlyRate: 103, department: "PMO", maxAllocation: 100 },
];

const T = (override: Partial<PlanTask>): PlanTask => ({
  id: "",
  parentId: null,
  wbs: "",
  name: "",
  type: "Task",
  mode: "Auto",
  duration: "1d",
  start: "2024-01-15",
  finish: "2024-01-15",
  actualStart: null,
  actualFinish: null,
  progress: 0,
  predecessors: [],
  successorIds: [],
  resourceNames: [],
  resources: [],
  work: 0,
  actualWork: 0,
  remainingWork: 0,
  cost: 0,
  actualCost: 0,
  remainingCost: 0,
  plannedCost: 0,
  forecastCost: 0,
  committedCost: 0,
  baselineStart: null,
  baselineFinish: null,
  baselineDuration: null,
  baselineWork: null,
  baselineCost: null,
  variance: 0,
  totalSlack: 0,
  freeSlack: 0,
  constraintType: "As Soon As Possible",
  constraintDate: null,
  deadline: null,
  priority: "Medium",
  isCritical: false,
  status: "Not Started",
  owner: "",
  source: "Internal",
  externalId: null,
  externalKey: null,
  syncStatus: null,
  lastSync: null,
  externalLink: null,
  calendar: "Standard",
  description: "",
  notes: "",
  isExpanded: true,
  children: [],
  tags: [],
  ...override,
});

function shiftDate(base: string, days: number): string {
  const d = new Date(base);
  d.setDate(d.getDate() + days);
  return d.toISOString().split("T")[0];
}

function addWorkingDays(base: string, workingDays: number): string {
  let d = new Date(base);
  let added = 0;
  while (added < workingDays) {
    d.setDate(d.getDate() + 1);
    if (d.getDay() !== 0 && d.getDay() !== 6) added++;
  }
  return d.toISOString().split("T")[0];
}

function workingDaysBetween(start: string, end: string): number {
  const s = new Date(start);
  const e = new Date(end);
  let count = 0;
  let cur = new Date(s);
  while (cur <= e) {
    if (cur.getDay() !== 0 && cur.getDay() !== 6) count++;
    cur.setDate(cur.getDate() + 1);
  }
  return count;
}

function durationStr(days: number): string {
  if (days === 0) return "0d";
  if (days === 1) return "1d";
  return `${days}d`;
}

function formatDate(d: string): string {
  return new Date(d).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
}

const PHASE_START = "2024-01-15";

// ============================================================
// PHASE 1: Initiation (Jan 15 - Feb 9, 2024)
// ============================================================
const p1Start = PHASE_START;

export const planTasks: PlanTask[] = [];

let taskCounter = 0;
function tid(): string { taskCounter++; return `pt-${taskCounter}`; }

// Build flat tasks first, then we'll treeify
const flatTasks: PlanTask[] = [];

// Phase 1: Initiation
flatTasks.push(T({
  id: tid(), wbs: "1", name: "1. Initiation", type: "Summary", duration: "20d",
  start: p1Start, finish: addWorkingDays(p1Start, 19), progress: 100, status: "Completed",
  priority: "Critical", owner: "Daniel O'Brien", isExpanded: true,
  work: 320, actualWork: 320, remainingWork: 0, cost: 38400, actualCost: 38400, remainingCost: 0,
  plannedCost: 38000, forecastCost: 38400, committedCost: 38400,
  baselineStart: p1Start, baselineFinish: addWorkingDays(p1Start, 19), baselineDuration: "20d", baselineWork: 320, baselineCost: 38000,
  variance: 0, totalSlack: 0, freeSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.1", name: "Project Charter Development", type: "Task", duration: "5d",
  start: p1Start, finish: addWorkingDays(p1Start, 4), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien"], resources: [{ id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 50, type: "Internal", hourlyRate: 106 }],
  work: 40, actualWork: 40, cost: 4240, actualCost: 4240,
  baselineStart: p1Start, baselineFinish: addWorkingDays(p1Start, 4), baselineDuration: "5d",
  predecessors: [], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.2", name: "Stakeholder Identification & Analysis", type: "Task", duration: "3d",
  start: p1Start, finish: addWorkingDays(p1Start, 2), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien"], resources: [{ id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 30, type: "Internal", hourlyRate: 106 }],
  work: 24, actualWork: 24, cost: 2544, actualCost: 2544,
  baselineStart: p1Start, baselineFinish: addWorkingDays(p1Start, 2),
  predecessors: [], totalSlack: 2,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.3", name: "Business Case Finalisation", type: "Task", duration: "5d",
  start: addWorkingDays(p1Start, 5), finish: addWorkingDays(p1Start, 9), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien"], resources: [{ id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 40, type: "Internal", hourlyRate: 106 }],
  work: 40, actualWork: 40, cost: 4240, actualCost: 4240,
  baselineStart: addWorkingDays(p1Start, 5), baselineFinish: addWorkingDays(p1Start, 9),
  predecessors: ["pt-2"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.4", name: "Initial Risk Assessment", type: "Task", duration: "3d",
  start: addWorkingDays(p1Start, 5), finish: addWorkingDays(p1Start, 7), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien"], resources: [{ id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 25, type: "Internal", hourlyRate: 106 }],
  work: 20, actualWork: 20, cost: 2120, actualCost: 2120,
  baselineStart: addWorkingDays(p1Start, 5), baselineFinish: addWorkingDays(p1Start, 7),
  predecessors: [], totalSlack: 2,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.5", name: "Project Kick-off Meeting", type: "Meeting", duration: "1d",
  start: addWorkingDays(p1Start, 10), finish: addWorkingDays(p1Start, 10), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien", "Raj Kapoor"], resources: [{ id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 100, type: "Internal", hourlyRate: 106 }, { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 100, type: "Internal", hourlyRate: 119 }],
  work: 16, actualWork: 16, cost: 1800, actualCost: 1800,
  predecessors: ["pt-4"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-1", wbs: "1.6", name: "Steering Committee Approval", type: "ApprovalGate", duration: "0d",
  start: addWorkingDays(p1Start, 11), finish: addWorkingDays(p1Start, 11), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, actualWork: 0, cost: 0, actualCost: 0,
  predecessors: ["pt-6"], totalSlack: 0,
}));

// Phase 2: Discovery and Requirements
const p2Start = addWorkingDays(p1Start, 12);
flatTasks.push(T({
  id: tid(), wbs: "2", name: "2. Discovery and Requirements", type: "Summary", duration: "30d",
  start: p2Start, finish: addWorkingDays(p2Start, 29), progress: 100, status: "Completed",
  priority: "Critical", owner: "Daniel O'Brien", isExpanded: true,
  work: 640, actualWork: 638, remainingWork: 2, cost: 82000, actualCost: 81800, remainingCost: 200,
  plannedCost: 80000, forecastCost: 82000, committedCost: 81800,
  baselineStart: p2Start, baselineFinish: addWorkingDays(p2Start, 29), baselineDuration: "30d", baselineWork: 640, baselineCost: 80000,
  variance: 1, totalSlack: 0, freeSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.1", name: "Requirements Gathering Workshops", type: "Task", duration: "10d",
  start: p2Start, finish: addWorkingDays(p2Start, 9), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien", "Raj Kapoor", "Lisa Kumar"], resources: [
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 40, type: "Internal", hourlyRate: 106 },
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 50, type: "Internal", hourlyRate: 119 },
    { id: "pr-4", name: "Lisa Kumar", role: "UX Designer", allocation: 30, type: "Internal", hourlyRate: 75 },
  ],
  work: 200, actualWork: 200, cost: 24160, actualCost: 24160,
  baselineStart: p2Start, baselineFinish: addWorkingDays(p2Start, 9),
  predecessors: ["pt-8"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.2", name: "Business Process Mapping", type: "Task", duration: "8d",
  start: addWorkingDays(p2Start, 10), finish: addWorkingDays(p2Start, 17), progress: 100, status: "Completed",
  owner: "Lisa Kumar", resourceNames: ["Lisa Kumar", "Daniel O'Brien"], resources: [
    { id: "pr-4", name: "Lisa Kumar", role: "UX Designer", allocation: 60, type: "Internal", hourlyRate: 75 },
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 20, type: "Internal", hourlyRate: 106 },
  ],
  work: 100, actualWork: 100, cost: 11240, actualCost: 11240,
  baselineStart: addWorkingDays(p2Start, 10), baselineFinish: addWorkingDays(p2Start, 17),
  predecessors: ["pt-9"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.3", name: "Technical Requirements Specification", type: "Task", duration: "12d",
  start: addWorkingDays(p2Start, 0), finish: addWorkingDays(p2Start, 11), progress: 100, status: "Completed",
  owner: "Raj Kapoor", resourceNames: ["Raj Kapoor", "Nexus Digital Partners"], resources: [
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 60, type: "Internal", hourlyRate: 119 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 40, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 160, actualWork: 158, cost: 17704, actualCost: 17470, remainingWork: 2,
  baselineStart: p2Start, baselineFinish: addWorkingDays(p2Start, 11),
  predecessors: [], totalSlack: 2,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.4", name: "Regulatory Requirements Analysis", type: "Task", duration: "8d",
  start: addWorkingDays(p2Start, 12), finish: addWorkingDays(p2Start, 19), progress: 100, status: "Completed",
  owner: "Compliance Consultants Ltd", resourceNames: ["Compliance Consultants Ltd"], resources: [
    { id: "pr-10", name: "Compliance Consultants Ltd", role: "Regulatory Advisor", allocation: 40, type: "Outsourced", hourlyRate: 150 },
  ],
  work: 64, actualWork: 64, cost: 9600, actualCost: 9600,
  baselineStart: addWorkingDays(p2Start, 12), baselineFinish: addWorkingDays(p2Start, 19),
  predecessors: ["pt-9"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.5", name: "Requirements Sign-off", type: "ApprovalGate", duration: "0d",
  start: addWorkingDays(p2Start, 20), finish: addWorkingDays(p2Start, 20), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-10", "pt-13"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-7", wbs: "2.6", name: "Discovery Phase Complete", type: "Milestone", duration: "0d",
  start: addWorkingDays(p2Start, 20), finish: addWorkingDays(p2Start, 20), progress: 100, status: "Completed",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-14"], totalSlack: 0,
}));

// Phase 3: Solution Design
const p3Start = addWorkingDays(p2Start, 21);
flatTasks.push(T({
  id: tid(), wbs: "3", name: "3. Solution Design", type: "Summary", duration: "35d",
  start: p3Start, finish: addWorkingDays(p3Start, 34), progress: 100, status: "Completed",
  priority: "Critical", owner: "Raj Kapoor", isExpanded: true,
  work: 720, actualWork: 720, cost: 95000, actualCost: 95000,
  plannedCost: 92000, forecastCost: 95000,
  baselineStart: p3Start, baselineFinish: addWorkingDays(p3Start, 34), baselineDuration: "35d",
  variance: 1, totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.1", name: "Solution Architecture Design", type: "Task", duration: "15d",
  start: p3Start, finish: addWorkingDays(p3Start, 14), progress: 100, status: "Completed",
  owner: "Raj Kapoor", resourceNames: ["Raj Kapoor", "Nexus Digital Partners"], resources: [
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 80, type: "Internal", hourlyRate: 119 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 60, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 270, actualWork: 270, cost: 33522, actualCost: 33522,
  baselineStart: p3Start, baselineFinish: addWorkingDays(p3Start, 14),
  predecessors: ["pt-14"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.2", name: "Data Architecture Design", type: "Task", duration: "12d",
  start: addWorkingDays(p3Start, 15), finish: addWorkingDays(p3Start, 26), progress: 100, status: "Completed",
  owner: "Maria Garcia", resourceNames: ["Maria Garcia", "Raj Kapoor"], resources: [
    { id: "pr-5", name: "Maria Garcia", role: "Data Engineer", allocation: 70, type: "Internal", hourlyRate: 90 },
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 30, type: "Internal", hourlyRate: 119 },
  ],
  work: 140, actualWork: 140, cost: 16548, actualCost: 16548,
  baselineStart: addWorkingDays(p3Start, 15), baselineFinish: addWorkingDays(p3Start, 26),
  predecessors: ["pt-17"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.3", name: "Integration Architecture Design", type: "Task", duration: "10d",
  start: addWorkingDays(p3Start, 15), finish: addWorkingDays(p3Start, 24), progress: 100, status: "Completed",
  owner: "Raj Kapoor", resourceNames: ["Raj Kapoor", "Nexus Digital Partners"], resources: [
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 40, type: "Internal", hourlyRate: 119 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 50, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 130, actualWork: 130, cost: 14862, actualCost: 14862,
  baselineStart: addWorkingDays(p3Start, 15), baselineFinish: addWorkingDays(p3Start, 24),
  predecessors: ["pt-17"], totalSlack: 2,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.4", name: "UX/UI Design", type: "Task", duration: "12d",
  start: addWorkingDays(p3Start, 0), finish: addWorkingDays(p3Start, 11), progress: 100, status: "Completed",
  owner: "Lisa Kumar", resourceNames: ["Lisa Kumar"], resources: [
    { id: "pr-4", name: "Lisa Kumar", role: "UX Designer", allocation: 80, type: "Internal", hourlyRate: 75 },
  ],
  work: 120, actualWork: 120, cost: 9000, actualCost: 9000,
  baselineStart: p3Start, baselineFinish: addWorkingDays(p3Start, 11),
  predecessors: ["pt-14"], totalSlack: 3,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.5", name: "Architecture Review Board", type: "Meeting", duration: "1d",
  start: addWorkingDays(p3Start, 27), finish: addWorkingDays(p3Start, 27), progress: 100, status: "Completed",
  owner: "Raj Kapoor", resourceNames: ["Raj Kapoor", "Daniel O'Brien"], resources: [
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 100, type: "Internal", hourlyRate: 119 },
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 100, type: "Internal", hourlyRate: 106 },
  ],
  work: 16, actualWork: 16, cost: 1800, actualCost: 1800,
  predecessors: ["pt-18", "pt-19"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-16", wbs: "3.6", name: "Design Phase Complete", type: "Milestone", duration: "0d",
  start: addWorkingDays(p3Start, 28), finish: addWorkingDays(p3Start, 28), progress: 100, status: "Completed",
  owner: "Raj Kapoor", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-22"], totalSlack: 0,
}));

// Phase 4: Core Platform Implementation
const p4Start = addWorkingDays(p3Start, 29);
flatTasks.push(T({
  id: tid(), wbs: "4", name: "4. Core Platform Implementation", type: "Summary", duration: "55d",
  start: p4Start, finish: addWorkingDays(p4Start, 54), progress: 72, status: "In Progress",
  priority: "Critical", owner: "Raj Kapoor", isExpanded: true,
  work: 2400, actualWork: 1800, remainingWork: 600, cost: 320000, actualCost: 240000, remainingCost: 80000,
  plannedCost: 310000, forecastCost: 325000, committedCost: 318000,
  baselineStart: p4Start, baselineFinish: addWorkingDays(p4Start, 49), baselineDuration: "50d",
  variance: -5, totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.1", name: "Core Services Development (Sprint 1)", type: "Task", duration: "10d",
  start: p4Start, finish: addWorkingDays(p4Start, 9), progress: 100, status: "Completed",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 80, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 200, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 320, actualWork: 320, cost: 35680, actualCost: 35680,
  baselineStart: p4Start, baselineFinish: addWorkingDays(p4Start, 9),
  predecessors: ["pt-23"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.2", name: "Core Services Development (Sprint 2)", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 10), finish: addWorkingDays(p4Start, 19), progress: 100, status: "Completed",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 80, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 200, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 320, actualWork: 320, cost: 35680, actualCost: 35680,
  baselineStart: addWorkingDays(p4Start, 10), baselineFinish: addWorkingDays(p4Start, 19),
  predecessors: ["pt-25"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.3", name: "API Gateway Implementation", type: "Task", duration: "15d",
  start: addWorkingDays(p4Start, 0), finish: addWorkingDays(p4Start, 14), progress: 100, status: "Completed",
  owner: "Nexus Digital Partners", resourceNames: ["Nexus Digital Partners", "Raj Kapoor"], resources: [
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 100, type: "Outsourced", hourlyRate: 65 },
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 30, type: "Internal", hourlyRate: 119 },
  ],
  work: 220, actualWork: 220, cost: 21356, actualCost: 21356,
  baselineStart: p4Start, baselineFinish: addWorkingDays(p4Start, 14),
  predecessors: ["pt-23"], totalSlack: 4,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.4", name: "Core Services Development (Sprint 3)", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 20), finish: addWorkingDays(p4Start, 29), progress: 100, status: "Completed",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 80, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 200, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 320, actualWork: 320, cost: 35680, actualCost: 35680,
  baselineStart: addWorkingDays(p4Start, 20), baselineFinish: addWorkingDays(p4Start, 29),
  predecessors: ["pt-26"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.5", name: "Core Services Development (Sprint 4)", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 30), finish: addWorkingDays(p4Start, 39), progress: 80, status: "In Progress",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 80, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 200, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 320, actualWork: 256, remainingWork: 64, cost: 35680, actualCost: 28544, remainingCost: 7136,
  baselineStart: addWorkingDays(p4Start, 30), baselineFinish: addWorkingDays(p4Start, 39),
  predecessors: ["pt-28"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.6", name: "Core Services Development (Sprint 5)", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 40), finish: addWorkingDays(p4Start, 49), progress: 40, status: "In Progress",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 80, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 200, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 320, actualWork: 128, remainingWork: 192, cost: 35680, actualCost: 14272, remainingCost: 21408,
  baselineStart: addWorkingDays(p4Start, 40), baselineFinish: addWorkingDays(p4Start, 49),
  predecessors: ["pt-29"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.7", name: "Security Architecture Implementation", type: "Task", duration: "20d",
  start: addWorkingDays(p4Start, 10), finish: addWorkingDays(p4Start, 29), progress: 100, status: "Completed",
  owner: "Tom Harris", resourceNames: ["Tom Harris", "Nexus Digital Partners"], resources: [
    { id: "pr-6", name: "Tom Harris", role: "Security Analyst", allocation: 50, type: "Internal", hourlyRate: 98 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 30, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 200, actualWork: 200, cost: 22940, actualCost: 22940,
  baselineStart: addWorkingDays(p4Start, 10), baselineFinish: addWorkingDays(p4Start, 29),
  predecessors: ["pt-23"], totalSlack: 20,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.8", name: "CI/CD Pipeline Setup", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 0), finish: addWorkingDays(p4Start, 9), progress: 100, status: "Completed",
  owner: "Robert Davies", resourceNames: ["Robert Davies"], resources: [
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 60, type: "Internal", hourlyRate: 94 },
  ],
  work: 60, actualWork: 60, cost: 5640, actualCost: 5640,
  baselineStart: p4Start, baselineFinish: addWorkingDays(p4Start, 9),
  predecessors: ["pt-23"], totalSlack: 20,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.9", name: "Code Complete Milestone", type: "Milestone", duration: "0d",
  start: addWorkingDays(p4Start, 50), finish: addWorkingDays(p4Start, 50), progress: 0, status: "Not Started",
  owner: "James Wilson", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-30"], totalSlack: 0,
  isCritical: true,
}));

// Phase 5: Integration and Data Migration
const p5Start = addWorkingDays(p4Start, 40);
flatTasks.push(T({
  id: tid(), wbs: "5", name: "5. Integration and Data Migration", type: "Summary", duration: "40d",
  start: p5Start, finish: addWorkingDays(p5Start, 39), progress: 35, status: "In Progress",
  priority: "Critical", owner: "Sophie Turner", isExpanded: true,
  work: 1200, actualWork: 420, remainingWork: 780, cost: 164000, actualCost: 57400, remainingCost: 106600,
  plannedCost: 150000, forecastCost: 164000,
  baselineStart: p5Start, baselineFinish: addWorkingDays(p5Start, 34), baselineDuration: "35d",
  variance: -8, totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.1", name: "Data Mapping & Transformation Rules", type: "Task", duration: "12d",
  start: p5Start, finish: addWorkingDays(p5Start, 11), progress: 100, status: "Completed",
  owner: "Sophie Turner", resourceNames: ["Sophie Turner", "Maria Garcia"], resources: [
    { id: "pr-12", name: "Sophie Turner", role: "Data Migration Lead", allocation: 60, type: "Internal", hourlyRate: 103 },
    { id: "pr-5", name: "Maria Garcia", role: "Data Engineer", allocation: 50, type: "Internal", hourlyRate: 90 },
  ],
  work: 160, actualWork: 160, cost: 17076, actualCost: 17076,
  baselineStart: p5Start, baselineFinish: addWorkingDays(p5Start, 11),
  predecessors: ["pt-23"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.2", name: "Data Migration Script Development", type: "Task", duration: "15d",
  start: addWorkingDays(p5Start, 12), finish: addWorkingDays(p5Start, 26), progress: 80, status: "In Progress",
  owner: "Maria Garcia", resourceNames: ["Maria Garcia", "Sophie Turner"], resources: [
    { id: "pr-5", name: "Maria Garcia", role: "Data Engineer", allocation: 80, type: "Internal", hourlyRate: 90 },
    { id: "pr-12", name: "Sophie Turner", role: "Data Migration Lead", allocation: 30, type: "Internal", hourlyRate: 103 },
  ],
  work: 200, actualWork: 160, remainingWork: 40, cost: 23220, actualCost: 18576, remainingCost: 4644,
  baselineStart: addWorkingDays(p5Start, 12), baselineFinish: addWorkingDays(p5Start, 26),
  predecessors: ["pt-35"], totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.3", name: "Integration Testing (Phase 1)", type: "Task", duration: "10d",
  start: addWorkingDays(p5Start, 12), finish: addWorkingDays(p5Start, 21), progress: 60, status: "In Progress",
  owner: "Peter Chang", resourceNames: ["Peter Chang", "Nexus Digital Partners"], resources: [
    { id: "pr-7", name: "Peter Chang", role: "QA Lead", allocation: 60, type: "Internal", hourlyRate: 85 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 80, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 160, actualWork: 96, remainingWork: 64, cost: 15440, actualCost: 9264, remainingCost: 6176,
  baselineStart: addWorkingDays(p5Start, 12), baselineFinish: addWorkingDays(p5Start, 21),
  predecessors: ["pt-25", "pt-27"], totalSlack: 5,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.4", name: "System Integration Testing (SIT)", type: "Task", duration: "15d",
  start: addWorkingDays(p5Start, 22), finish: addWorkingDays(p5Start, 36), progress: 10, status: "In Progress",
  owner: "Peter Chang", resourceNames: ["Peter Chang", "Nexus Digital Partners"], resources: [
    { id: "pr-7", name: "Peter Chang", role: "QA Lead", allocation: 80, type: "Internal", hourlyRate: 85 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 120, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 300, actualWork: 30, remainingWork: 270, cost: 29420, actualCost: 2942, remainingCost: 26478,
  baselineStart: addWorkingDays(p5Start, 22), baselineFinish: addWorkingDays(p5Start, 36),
  predecessors: ["pt-28", "pt-36"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.5", name: "Data Migration Dry Run", type: "Task", duration: "5d",
  start: addWorkingDays(p5Start, 22), finish: addWorkingDays(p5Start, 26), progress: 0, status: "Not Started",
  owner: "Sophie Turner", resourceNames: ["Sophie Turner", "Maria Garcia"], resources: [
    { id: "pr-12", name: "Sophie Turner", role: "Data Migration Lead", allocation: 50, type: "Internal", hourlyRate: 103 },
    { id: "pr-5", name: "Maria Garcia", role: "Data Engineer", allocation: 50, type: "Internal", hourlyRate: 90 },
  ],
  work: 60, actualWork: 0, remainingWork: 60, cost: 6180, actualCost: 0, remainingCost: 6180,
  baselineStart: addWorkingDays(p5Start, 22), baselineFinish: addWorkingDays(p5Start, 26),
  predecessors: ["pt-36"], totalSlack: 5,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.6", name: "Integration Phase Complete", type: "Milestone", duration: "0d",
  start: addWorkingDays(p5Start, 37), finish: addWorkingDays(p5Start, 37), progress: 0, status: "Not Started",
  owner: "Sophie Turner", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-38"], totalSlack: 0,
  isCritical: true,
}));

// Phase 6: Testing and Regulatory Validation
const p6Start = addWorkingDays(p5Start, 30);
flatTasks.push(T({
  id: tid(), wbs: "6", name: "6. Testing and Regulatory Validation", type: "Summary", duration: "35d",
  start: p6Start, finish: addWorkingDays(p6Start, 34), progress: 10, status: "In Progress",
  priority: "Critical", owner: "Peter Chang", isExpanded: true,
  work: 800, actualWork: 80, remainingWork: 720, cost: 112000, actualCost: 11200, remainingCost: 100800,
  plannedCost: 105000, forecastCost: 112000,
  baselineStart: p6Start, baselineFinish: addWorkingDays(p6Start, 29), baselineDuration: "30d",
  variance: -10, totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.1", name: "User Acceptance Testing (UAT) Preparation", type: "Task", duration: "5d",
  start: p6Start, finish: addWorkingDays(p6Start, 4), progress: 20, status: "In Progress",
  owner: "Peter Chang", resourceNames: ["Peter Chang", "Daniel O'Brien"], resources: [
    { id: "pr-7", name: "Peter Chang", role: "QA Lead", allocation: 50, type: "Internal", hourlyRate: 85 },
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 20, type: "Internal", hourlyRate: 106 },
  ],
  work: 50, actualWork: 10, remainingWork: 40, cost: 5285, actualCost: 1057, remainingCost: 4228,
  baselineStart: p6Start, baselineFinish: addWorkingDays(p6Start, 4),
  predecessors: ["pt-28"], totalSlack: 10,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.2", name: "User Acceptance Testing Execution", type: "Task", duration: "15d",
  start: addWorkingDays(p6Start, 5), finish: addWorkingDays(p6Start, 19), progress: 5, status: "In Progress",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien", "Nexus Digital Partners"], resources: [
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 30, type: "Internal", hourlyRate: 106 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 60, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 160, actualWork: 8, remainingWork: 152, cost: 18312, actualCost: 916, remainingCost: 17396,
  baselineStart: addWorkingDays(p6Start, 5), baselineFinish: addWorkingDays(p6Start, 19),
  predecessors: ["pt-42"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.3", name: "Performance Testing", type: "Task", duration: "10d",
  start: addWorkingDays(p6Start, 5), finish: addWorkingDays(p6Start, 14), progress: 20, status: "In Progress",
  owner: "Peter Chang", resourceNames: ["Peter Chang", "Robert Davies"], resources: [
    { id: "pr-7", name: "Peter Chang", role: "QA Lead", allocation: 40, type: "Internal", hourlyRate: 85 },
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 40, type: "Internal", hourlyRate: 94 },
  ],
  work: 80, actualWork: 16, remainingWork: 64, cost: 7160, actualCost: 1432, remainingCost: 5728,
  baselineStart: addWorkingDays(p6Start, 5), baselineFinish: addWorkingDays(p6Start, 14),
  predecessors: ["pt-42"], totalSlack: 5,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.4", name: "Security Penetration Testing", type: "Task", duration: "10d",
  start: addWorkingDays(p6Start, 5), finish: addWorkingDays(p6Start, 14), progress: 15, status: "In Progress",
  owner: "Tom Harris", resourceNames: ["Tom Harris"], resources: [
    { id: "pr-6", name: "Tom Harris", role: "Security Analyst", allocation: 60, type: "Internal", hourlyRate: 98 },
  ],
  work: 60, actualWork: 9, remainingWork: 51, cost: 5880, actualCost: 882, remainingCost: 4998,
  baselineStart: addWorkingDays(p6Start, 5), baselineFinish: addWorkingDays(p6Start, 14),
  predecessors: ["pt-42"], totalSlack: 5,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.5", name: "Regulatory Compliance Audit", type: "Task", duration: "8d",
  start: addWorkingDays(p6Start, 20), finish: addWorkingDays(p6Start, 27), progress: 0, status: "Not Started",
  owner: "Compliance Consultants Ltd", resourceNames: ["Compliance Consultants Ltd", "Daniel O'Brien"], resources: [
    { id: "pr-10", name: "Compliance Consultants Ltd", role: "Regulatory Advisor", allocation: 50, type: "Outsourced", hourlyRate: 150 },
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 20, type: "Internal", hourlyRate: 106 },
  ],
  work: 90, actualWork: 0, remainingWork: 90, cost: 13206, actualCost: 0, remainingCost: 13206,
  baselineStart: addWorkingDays(p6Start, 20), baselineFinish: addWorkingDays(p6Start, 27),
  predecessors: ["pt-43"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.6", name: "Regulatory Sign-off", type: "ApprovalGate", duration: "0d",
  start: addWorkingDays(p6Start, 28), finish: addWorkingDays(p6Start, 28), progress: 0, status: "Not Started",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-47"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.7", name: "UAT Sign-off Milestone", type: "Milestone", duration: "0d",
  start: addWorkingDays(p6Start, 20), finish: addWorkingDays(p6Start, 20), progress: 0, status: "Not Started",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-43"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-41", wbs: "6.8", name: "Testing Phase Complete", type: "Milestone", duration: "0d",
  start: addWorkingDays(p6Start, 29), finish: addWorkingDays(p6Start, 29), progress: 0, status: "Not Started",
  owner: "Peter Chang", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-48", "pt-49"], totalSlack: 0,
  isCritical: true,
}));

// Phase 7: Deployment and Hypercare
const p7Start = addWorkingDays(p6Start, 30);
flatTasks.push(T({
  id: tid(), wbs: "7", name: "7. Deployment and Hypercare", type: "Summary", duration: "30d",
  start: p7Start, finish: addWorkingDays(p7Start, 29), progress: 0, status: "Not Started",
  priority: "Critical", owner: "Daniel O'Brien", isExpanded: true,
  work: 480, actualWork: 0, remainingWork: 480, cost: 68000, actualCost: 0, remainingCost: 68000,
  plannedCost: 65000, forecastCost: 68000,
  baselineStart: p7Start, baselineFinish: addWorkingDays(p7Start, 24), baselineDuration: "25d",
  variance: 0, totalSlack: 0,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.1", name: "Production Deployment Plan", type: "Task", duration: "5d",
  start: p7Start, finish: addWorkingDays(p7Start, 4), progress: 0, status: "Not Started",
  owner: "Robert Davies", resourceNames: ["Robert Davies", "Daniel O'Brien"], resources: [
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 60, type: "Internal", hourlyRate: 94 },
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 30, type: "Internal", hourlyRate: 106 },
  ],
  work: 60, cost: 5730,
  baselineStart: p7Start, baselineFinish: addWorkingDays(p7Start, 4),
  predecessors: ["pt-50"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.2", name: "Go/No-Go Decision Meeting", type: "ApprovalGate", duration: "0d",
  start: addWorkingDays(p7Start, 5), finish: addWorkingDays(p7Start, 5), progress: 0, status: "Not Started",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-52"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.3", name: "Production Deployment (Wave 1)", type: "Task", duration: "5d",
  start: addWorkingDays(p7Start, 6), finish: addWorkingDays(p7Start, 10), progress: 0, status: "Not Started",
  owner: "Robert Davies", resourceNames: ["Robert Davies", "Nexus Digital Partners", "James Wilson"], resources: [
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 80, type: "Internal", hourlyRate: 94 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 80, type: "Outsourced", hourlyRate: 65 },
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 40, type: "Internal", hourlyRate: 88 },
  ],
  work: 120, cost: 13876,
  baselineStart: addWorkingDays(p7Start, 6), baselineFinish: addWorkingDays(p7Start, 10),
  predecessors: ["pt-53"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.4", name: "Production Deployment (Wave 2)", type: "Task", duration: "5d",
  start: addWorkingDays(p7Start, 11), finish: addWorkingDays(p7Start, 15), progress: 0, status: "Not Started",
  owner: "Robert Davies", resourceNames: ["Robert Davies", "Nexus Digital Partners", "James Wilson"], resources: [
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 80, type: "Internal", hourlyRate: 94 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 80, type: "Outsourced", hourlyRate: 65 },
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 40, type: "Internal", hourlyRate: 88 },
  ],
  work: 120, cost: 13876,
  baselineStart: addWorkingDays(p7Start, 11), baselineFinish: addWorkingDays(p7Start, 15),
  predecessors: ["pt-55"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.5", name: "Hypercare Support (Week 1)", type: "Task", duration: "5d",
  start: addWorkingDays(p7Start, 16), finish: addWorkingDays(p7Start, 20), progress: 0, status: "Not Started",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners", "Robert Davies"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 50, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 60, type: "Outsourced", hourlyRate: 65 },
    { id: "pr-11", name: "Robert Davies", role: "DevOps Engineer", allocation: 50, type: "Internal", hourlyRate: 94 },
  ],
  work: 100, cost: 12060,
  baselineStart: addWorkingDays(p7Start, 16), baselineFinish: addWorkingDays(p7Start, 20),
  predecessors: ["pt-55", "pt-56"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.6", name: "Hypercare Support (Week 2)", type: "Task", duration: "5d",
  start: addWorkingDays(p7Start, 21), finish: addWorkingDays(p7Start, 25), progress: 0, status: "Not Started",
  owner: "James Wilson", resourceNames: ["James Wilson", "Nexus Digital Partners"], resources: [
    { id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 30, type: "Internal", hourlyRate: 88 },
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 40, type: "Outsourced", hourlyRate: 65 },
  ],
  work: 50, cost: 5660,
  baselineStart: addWorkingDays(p7Start, 21), baselineFinish: addWorkingDays(p7Start, 25),
  predecessors: ["pt-57"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.7", name: "Project Closure Report", type: "Task", duration: "3d",
  start: addWorkingDays(p7Start, 26), finish: addWorkingDays(p7Start, 28), progress: 0, status: "Not Started",
  owner: "Daniel O'Brien", resourceNames: ["Daniel O'Brien"], resources: [
    { id: "pr-1", name: "Daniel O'Brien", role: "Senior Project Manager", allocation: 50, type: "Internal", hourlyRate: 106 },
  ],
  work: 30, cost: 3180,
  baselineStart: addWorkingDays(p7Start, 26), baselineFinish: addWorkingDays(p7Start, 28),
  predecessors: ["pt-58"], totalSlack: 0,
  isCritical: true,
}));
flatTasks.push(T({
  id: tid(), parentId: "pt-51", wbs: "7.8", name: "Project Complete", type: "Milestone", duration: "0d",
  start: addWorkingDays(p7Start, 29), finish: addWorkingDays(p7Start, 29), progress: 0, status: "Not Started",
  owner: "Daniel O'Brien", resourceNames: [], resources: [],
  work: 0, cost: 0,
  predecessors: ["pt-59"], totalSlack: 0,
  isCritical: true,
}));

// Add Jira-linked task
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.10", name: "Jira: CORE-142 — Transaction Processing Module", type: "JiraLinked", duration: "8d",
  start: addWorkingDays(p4Start, 20), finish: addWorkingDays(p4Start, 27), progress: 90, status: "In Progress",
  owner: "James Wilson", resourceNames: ["James Wilson"], resources: [{ id: "pr-3", name: "James Wilson", role: "Senior Developer", allocation: 60, type: "Internal", hourlyRate: 88 }],
  work: 60, actualWork: 54, remainingWork: 6, cost: 5280, actualCost: 4752,
  predecessors: ["pt-26"], totalSlack: 2,
  source: "Jira", externalId: "CORE-142", externalKey: "CORE-142", syncStatus: "Synced", lastSync: "2024-08-15", externalLink: "https://jira.nexus.com/browse/CORE-142",
}));

// Add Ticket-linked task
flatTasks.push(T({
  id: tid(), parentId: "pt-34", wbs: "5.7", name: "TKT-2026-0032 — Legacy Data Cleanup", type: "TicketLinked", duration: "5d",
  start: addWorkingDays(p5Start, 27), finish: addWorkingDays(p5Start, 31), progress: 0, status: "Not Started",
  owner: "Maria Garcia", resourceNames: ["Maria Garcia"], resources: [{ id: "pr-5", name: "Maria Garcia", role: "Data Engineer", allocation: 40, type: "Internal", hourlyRate: 90 }],
  work: 20, cost: 1800,
  predecessors: ["pt-36"], totalSlack: 5,
  source: "Internal Ticket", externalId: "TKT-2026-0032", externalKey: "TKT-2026-0032", syncStatus: "Linked",
}));

// Add delayed task for illustration
flatTasks.push(T({
  id: tid(), parentId: "pt-24", wbs: "4.11", name: "Third-Party Vendor SDK Integration", type: "Task", duration: "10d",
  start: addWorkingDays(p4Start, 20), finish: addWorkingDays(p4Start, 34), progress: 30, status: "Delayed",
  owner: "Nexus Digital Partners", resourceNames: ["Nexus Digital Partners", "Raj Kapoor"], resources: [
    { id: "pr-9", name: "Nexus Digital Partners", role: "Development Team", allocation: 60, type: "Outsourced", hourlyRate: 65 },
    { id: "pr-2", name: "Raj Kapoor", role: "Technical Architect", allocation: 20, type: "Internal", hourlyRate: 119 },
  ],
  work: 100, actualWork: 30, remainingWork: 70, cost: 9728, actualCost: 2918, remainingCost: 6810,
  baselineStart: addWorkingDays(p4Start, 20), baselineFinish: addWorkingDays(p4Start, 29),
  predecessors: ["pt-26"], totalSlack: -5,
  isCritical: true, status: "Delayed",
}));

// --- Build tree ---
const taskMap = new Map<string, PlanTask>();
flatTasks.forEach(t => taskMap.set(t.id, { ...t, children: [] }));

const rootTasks: PlanTask[] = [];
flatTasks.forEach(t => {
  const node = taskMap.get(t.id)!;
  if (t.parentId && taskMap.has(t.parentId)) {
    taskMap.get(t.parentId)!.children.push(node);
  } else {
    rootTasks.push(node);
  }
});

export const projectPlanData = {
  projectId: "proj-1",
  projectName: "Core Banking Modernisation",
  projectCode: "CBM-2024",
  portfolio: "Strategic Initiatives",
  program: "Core Banking Programme",
  projectManager: "Daniel O'Brien",
  health: "amber" as const,
  plannedStart: "2024-01-15",
  plannedFinish: "2024-12-20",
  forecastFinish: "2025-01-31",
  progress: 62,
  baselineName: "Baseline v1.0",
  lastUpdate: "2024-09-15",
  scheduleSource: "Native" as const,
  rootTasks,
  projectCalendar: {
    name: "Standard",
    workingDays: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    workingHoursStart: "08:00",
    workingHoursEnd: "17:00",
    breakDuration: 1,
  },
};

export function getPlanSummary(tasks?: PlanTask[]): PlanSummary {
  const all = tasks ?? getAllTasks();
  return {
    totalTasks: all.length,
    completed: all.filter(t => t.status === "Completed").length,
    inProgress: all.filter(t => t.status === "In Progress").length,
    notStarted: all.filter(t => t.status === "Not Started").length,
    delayed: all.filter(t => t.status === "Delayed").length,
    criticalCount: all.filter(t => t.isCritical).length,
    milestones: all.filter(t => t.type === "Milestone").length,
    totalPlannedWork: all.reduce((s, t) => s + t.work, 0),
    totalActualWork: all.reduce((s, t) => s + t.actualWork, 0),
    totalPlannedCost: all.reduce((s, t) => s + t.plannedCost, 0),
    totalForecastCost: all.reduce((s, t) => s + t.forecastCost, 0),
    plannedDuration: "240d",
    forecastDuration: "260d",
    scheduleVariance: -8.3,
    lastBaselineDate: "2024-01-20",
  };
}

export function getAllTasks(): PlanTask[] {
  const result: PlanTask[] = [];
  function walk(tasks: PlanTask[]) {
    for (const t of tasks) {
      result.push(t);
      if (t.children.length) walk(t.children);
    }
  }
  walk(rootTasks);
  return result;
}

export function getTaskById(id: string): PlanTask | undefined {
  return getAllTasks().find(t => t.id === id);
}
