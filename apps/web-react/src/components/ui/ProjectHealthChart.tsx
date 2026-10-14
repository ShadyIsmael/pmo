import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import { useI18n } from "@i18n/messages";

const FILL: Record<string, string> = {
  Healthy: "hsl(var(--chart-1))",
  Warning: "hsl(var(--chart-4))",
  Critical: "hsl(var(--chart-5))",
  Unknown: "hsl(var(--muted-foreground))",
};

export function ProjectHealthChart({ data }: { data?: Record<string, number> }) {
  const { t } = useI18n();
  const entries = Object.entries(data ?? {});
  if (entries.length === 0) return <div className="flex h-48 items-center justify-center text-muted-foreground">{t("dashboard.noData")}</div>;

  const chartData = entries.map(([name, value]) => ({ name, value, fill: FILL[name] ?? "hsl(var(--chart-2))" }));

  return (
    <div className="h-48">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={chartData} layout="vertical">
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis type="number" tick={{ fontSize: 10 }} />
          <YAxis dataKey="name" type="category" width={80} tick={{ fontSize: 11 }} />
          <Tooltip
            contentStyle={{ backgroundColor: "hsl(var(--popover))", border: "1px solid hsl(var(--border))", borderRadius: "8px" }}
            labelFormatter={(_, payload) => payload[0]?.payload?.name ?? ""}
            formatter={(value: number) => [value, ""]}
          />
          <Bar dataKey="value" radius={[0, 4, 4, 0]}>
            {chartData.map((row) => (
              <rect key={row.name} fill={row.fill} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
