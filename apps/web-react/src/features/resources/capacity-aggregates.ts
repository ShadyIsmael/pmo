import type { CapacityMatrixDto } from "./api/capacity";

export interface DepartmentCapacitySummary {
  departmentName: string;
  resourceCount: number;
  weeklyCapacityHours: number;
  averageAvailableHours: number;
  averageUtilizationPercent: number;
}

export function summarizeCapacityByDepartment(matrix?: CapacityMatrixDto): DepartmentCapacitySummary[] {
  if (!matrix) return [];

  const departmentNames = [...new Set(matrix.rows
    .map((row) => row.departmentName)
    .filter((name): name is string => Boolean(name)))].slice(0, 8);
  const firstPeriodKey = matrix.periods[0]?.key;

  return departmentNames.map((departmentName) => {
    const resources = matrix.rows.filter((row) => row.departmentName === departmentName);
    const availableCells = resources.flatMap((resource) => resource.cells)
      .filter((cell) => cell.periodKey === firstPeriodKey);

    return {
      departmentName,
      resourceCount: resources.length,
      weeklyCapacityHours: Math.round(resources.reduce((total, resource) => total + resource.weeklyCapacityHours, 0)),
      averageAvailableHours: availableCells.length
        ? Math.round(availableCells.reduce((total, cell) => total + cell.availableHours, 0) / availableCells.length)
        : 0,
      averageUtilizationPercent: resources.length
        ? Math.round(resources.reduce((total, resource) => total + resource.currentUtilization, 0) / resources.length)
        : 0,
    };
  });
}
