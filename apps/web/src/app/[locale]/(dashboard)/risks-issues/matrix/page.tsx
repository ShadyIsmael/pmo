"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { AlertTriangle } from "lucide-react";
import { mockRisks, mockProjects } from "@/lib/mock-data";

export default function RiskMatrixPage() {
  const heatMapData: { count: number; risks: typeof mockRisks }[][] = Array.from({ length: 5 }, () =>
    Array.from({ length: 5 }, () => ({ count: 0, risks: [] as typeof mockRisks }))
  );
  mockRisks.forEach(r => {
    const i = r.impact - 1;
    const p = r.probability - 1;
    if (i >= 0 && i < 5 && p >= 0 && p < 5) {
      heatMapData[i]![p]!.count++;
      heatMapData[i]![p]!.risks.push(r);
    }
  });

  const getHeatColor = (count: number) => {
    if (count === 0) return "bg-muted";
    if (count <= 1) return "bg-status-green/40";
    if (count <= 3) return "bg-status-amber/40";
    return "bg-status-red/50";
  };

  const getScoreLabel = (impact: number, probability: number) => {
    const score = impact * probability;
    if (score >= 15) return { label: "Critical", class: "text-status-red" };
    if (score >= 8) return { label: "High", class: "text-status-amber" };
    return { label: "Low", class: "text-status-green" };
  };

  return (
    <div className="space-y-6">
      <PageHeader title="Risk Matrix" description="Heat map of all risks by impact and probability" />

      <div className="grid gap-6 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle className="text-lg">Risk Heat Map</CardTitle>
            <CardDescription>Impact (rows) vs Probability (columns)</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-1">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <div className="w-20 text-right">Impact ↓</div>
                <div className="flex-1" />
                <div className="flex flex-1 text-center text-[10px]">Probability →</div>
              </div>
              {heatMapData.map((row, i) => (
                <div key={i} className="flex items-center gap-2 py-0.5">
                  <div className="w-20 text-right text-xs text-muted-foreground">{i + 1}</div>
                  <div className="flex flex-1 gap-1">
                    {row.map((cell, j) => {
                      const score = getScoreLabel(i + 1, j + 1);
                      return (
                        <div
                          key={j}
                          className={`flex h-12 flex-1 cursor-pointer items-center justify-center rounded text-xs font-medium transition-colors hover:opacity-80 ${getHeatColor(cell.count)}`}
                          title={`I:${i + 1} P:${j + 1} Score:${(i + 1) * (j + 1)}`}
                        >
                          {cell.count > 0 ? cell.count : ""}
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
              <div className="flex items-center gap-2 pt-1">
                <div className="w-20" />
                <div className="flex flex-1 gap-1">
                  {[1, 2, 3, 4, 5].map(v => (
                    <div key={v} className="flex-1 text-center text-[10px] text-muted-foreground">{v}</div>
                  ))}
                </div>
              </div>
              <div className="mt-4 flex items-center gap-3 text-xs text-muted-foreground">
                <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-green/40" /> Low (1-7)</span>
                <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-amber/40" /> Medium (8-14)</span>
                <span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-red/50" /> High (15-25)</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-lg">High-Risk Items</CardTitle>
            <CardDescription>Risks with score &ge; 15</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {mockRisks.filter(r => r.score >= 15).map((risk) => {
              const project = mockProjects.find(p => p.id === risk.projectId);
              return (
                <div key={risk.id} className="flex items-start gap-2 border-b pb-2 last:border-0">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-status-red" />
                  <div>
                    <p className="text-sm font-medium">{risk.title}</p>
                    <p className="text-xs text-muted-foreground">{project?.code} · Score: {risk.score} · {risk.category}</p>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
