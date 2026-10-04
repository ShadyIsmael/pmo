import * as React from "react";
import { useCreateProject } from "../api/projects";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { X } from "lucide-react";

interface CreateProjectSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CreateProjectSheet({ open, onOpenChange }: CreateProjectSheetProps) {
  const { t } = useI18n();
  const createProject = useCreateProject();
  const [formData, setFormData] = React.useState({
    name: "",
    code: "",
    description: "",
    programId: "",
    status: "Active",
    priority: "Medium",
    category: "",
    projectManagerId: "",
    sponsorId: "",
    portfolioId: "",
    departmentId: "",
    startDate: "",
    endDate: "",
    budget: "",
    currency: "USD",
    businessCase: "",
    notes: "",
    confidenceLevel: "Medium",
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await createProject.mutateAsync({
        ...formData,
        budget: formData.budget ? parseFloat(formData.budget) : undefined,
        startDate: formData.startDate || undefined,
        endDate: formData.endDate || undefined,
      });
      onOpenChange(false);
    } catch (error) {
      console.error("Failed to create project:", error);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end md:items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={() => onOpenChange(false)} />
      <div className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto bg-background rounded-t-lg md:rounded-lg shadow-xl">
        <Card>
          <CardHeader className="flex items-center justify-between">
            <CardTitle>{t("projects.create")}</CardTitle>
            <button onClick={() => onOpenChange(false)} className="p-1 hover:bg-accent rounded">
              <X className="h-4 w-4" />
            </button>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="name">{t("projects.projectDescription")}</Label>
                  <Input id="name" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} required placeholder={t("projects.projectDescription")} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="code">{t("common.code")}</Label>
                  <Input id="code" value={formData.code} onChange={(e) => setFormData({ ...formData, code: e.target.value })} placeholder={t("common.code")} />
                </div>
                <div className="md:col-span-2 space-y-1.5">
                  <Label htmlFor="description">{t("projects.projectDescription")}</Label>
                  <textarea
                    id="description"
                    className="min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    value={formData.description}
                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                    placeholder={t("projects.projectDescription")}
                  />
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-3">
                <div className="space-y-1.5">
                  <Label htmlFor="status">{t("common.status")}</Label>
                  <Select value={formData.status} onValueChange={(value) => setFormData({ ...formData, status: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder={t("common.select")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Active">Active</SelectItem>
                      <SelectItem value="Planning">Planning</SelectItem>
                      <SelectItem value="On Hold">On Hold</SelectItem>
                      <SelectItem value="Completed">Completed</SelectItem>
                      <SelectItem value="Cancelled">Cancelled</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="priority">{t("common.priority")}</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({ ...formData, priority: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder={t("common.select")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="Low">Low</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="currency">{t("projects.projectBudget")}</Label>
                  <Select value={formData.currency} onValueChange={(value) => setFormData({ ...formData, currency: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder={t("common.select")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="USD">USD</SelectItem>
                      <SelectItem value="EUR">EUR</SelectItem>
                      <SelectItem value="GBP">GBP</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="startDate">{t("projects.plannedStartDate")}</Label>
                  <Input id="startDate" type="date" value={formData.startDate} onChange={(e) => setFormData({ ...formData, startDate: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="endDate">{t("projects.plannedEndDate")}</Label>
                  <Input id="endDate" type="date" value={formData.endDate} onChange={(e) => setFormData({ ...formData, endDate: e.target.value })} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="budget">{t("projects.projectBudget")}</Label>
                  <Input id="budget" type="number" step="0.01" value={formData.budget} onChange={(e) => setFormData({ ...formData, budget: e.target.value })} placeholder="0.00" />
                </div>
              </div>

              <div className="space-y-1.5 md:col-span-2">
                <Label htmlFor="businessCase">{t("projects.initiationSummary")}</Label>
                <textarea
                  id="businessCase"
                  className="min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  value={formData.businessCase}
                  onChange={(e) => setFormData({ ...formData, businessCase: e.target.value })}
                  placeholder={t("projects.initiationSummaryDescription")}
                />
              </div>

              <div className="flex justify-end gap-3 pt-4 border-t">
                <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                  {t("common.cancel")}
                </Button>
                <Button type="submit" disabled={createProject.isPending}>
                  {createProject.isPending ? t("common.saving") : t("common.create")}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}