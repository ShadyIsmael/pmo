"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Input } from "@/components/ui/input";
import { Search, Filter, Plus, Building2, Loader2 } from "lucide-react";
import { getInitials, formatCurrency } from "@/lib/utils";
import { useResources } from "@/hooks/resources/useResources";

export default function OutsourcedPage() {
  const [search, setSearch] = useState("");
  const { data, isLoading, error } = useResources({ search: search || undefined, type: "Outsourced", pageSize: 50 });

  const outsourced = data?.items ?? [];
  const employmentTypes = [...new Set(outsourced.map(r => r.employmentType).filter(Boolean))];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Outsourced Resources"
        description={`${outsourced.length} external resources`}
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Add Vendor</Button>}
      />

      <div className="grid gap-4 md:grid-cols-3">
        {employmentTypes.slice(0, 3).map((type) => {
          const typeResources = outsourced.filter(r => r.employmentType === type);
          const avgRate = typeResources.length > 0
            ? Math.round(typeResources.reduce((s, r) => s + (r.billableRate ?? 0), 0) / typeResources.length)
            : 0;
          return (
            <Card key={type}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium">{type}</CardTitle>
                <Building2 className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{typeResources.length}</div>
                <p className="text-xs text-muted-foreground">Avg rate: {formatCurrency(avgRate)}/hr</p>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search outsourced resources..." className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : error ? (
        <div className="rounded-md border bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load resources: {(error as Error).message}
        </div>
      ) : (
        <div className="rounded-md border">
          <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
            <div className="col-span-3">Name</div>
            <div className="col-span-2">Role</div>
            <div className="col-span-2">Type</div>
            <div className="col-span-1">Rate</div>
            <div className="col-span-2">Location</div>
            <div className="col-span-2">Status</div>
          </div>
          {outsourced.map((res) => (
            <div key={res.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
              <div className="col-span-3 flex items-center gap-2 font-medium">
                <Avatar className="h-6 w-6"><AvatarFallback className="text-[8px]">{getInitials(res.name)}</AvatarFallback></Avatar>
                {res.name}
              </div>
              <div className="col-span-2 text-muted-foreground">{res.jobTitle ?? "—"}</div>
              <div className="col-span-2"><Badge variant="secondary" className="text-[10px]">{res.employmentType ?? "Contractor"}</Badge></div>
              <div className="col-span-1 text-muted-foreground">
                {res.billableRate ? formatCurrency(res.billableRate) + "/hr" : "—"}
              </div>
              <div className="col-span-2 text-xs text-muted-foreground">{res.locationName ?? "—"}</div>
              <div className="col-span-2">
                <span className={`text-xs font-medium ${res.isActive ? "text-status-green" : "text-muted-foreground"}`}>
                  {res.isActive ? "Active" : "Inactive"}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
