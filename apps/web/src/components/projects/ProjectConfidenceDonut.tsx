"use client";

type NullableNumber = number | null | undefined;

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function formatNullablePercentage(value: NullableNumber, fractionDigits = 0, signed = false) {
  if (!isFiniteNumber(value)) return "—";
  return `${signed && value > 0 ? "+" : ""}${value.toFixed(fractionDigits)}%`;
}

export function ProjectConfidenceDonut({ value }: { value: NullableNumber }) {
  const pct = isFiniteNumber(value) ? Math.min(100, Math.max(0, value)) : null;
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const filled = pct === null ? 0 : (pct / 100) * circumference;

  return (
    <div className="relative flex h-44 w-full items-center justify-center">
      <svg viewBox="0 0 100 100" className="h-40 w-40 -rotate-90">
        <circle cx="50" cy="50" r={radius} fill="none" strokeWidth="12" stroke="hsl(var(--muted))" />
        {pct !== null && (
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            strokeWidth="12"
            stroke="hsl(var(--primary))"
            strokeLinecap="round"
            strokeDasharray={`${filled} ${circumference - filled}`}
          />
        )}
      </svg>
      <div className="absolute text-2xl font-semibold tracking-tight text-foreground">
        {formatNullablePercentage(pct)}
      </div>
    </div>
  );
}
