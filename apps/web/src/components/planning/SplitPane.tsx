"use client"

import { useState, useCallback, useRef, type ReactNode } from "react"
import { cn } from "@/lib/utils"

type PaneLayout = "left" | "split" | "right"

interface SplitPaneProps {
  left: ReactNode
  right: ReactNode
  defaultLeftWidth?: number
  minLeftWidth?: number
  minRightWidth?: number
  leftCollapsible?: boolean
  rightCollapsible?: boolean
  leftLabel?: string
  rightLabel?: string
  syncVerticalScroll?: boolean
  className?: string
}

export function SplitPane({
  left,
  right,
  defaultLeftWidth = 45,
  minLeftWidth = 20,
  minRightWidth = 20,
  leftCollapsible = true,
  rightCollapsible = true,
  leftLabel = "Tasks",
  rightLabel = "Gantt",
  syncVerticalScroll = false,
  className,
}: SplitPaneProps) {
  const [leftWidth, setLeftWidth] = useState(defaultLeftWidth)
  const [layout, setLayout] = useState<PaneLayout>(() => {
    if (typeof window !== "undefined" && window.innerWidth < 1024) return "left"
    return "split"
  })
  const dragging = useRef(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const leftScrollRef = useRef<HTMLDivElement>(null)
  const rightScrollRef = useRef<HTMLDivElement>(null)
  const syncingScroll = useRef(false)

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault()
    dragging.current = true

    const handleMouseMove = (e: MouseEvent) => {
      if (!dragging.current) return
      const container = containerRef.current
      if (!container) return
      const rect = container.getBoundingClientRect()
      const pct = ((e.clientX - rect.left) / rect.width) * 100
      setLeftWidth(Math.max(minLeftWidth, Math.min(100 - minRightWidth, pct)))
    }

    const handleMouseUp = () => {
      dragging.current = false
      document.removeEventListener("mousemove", handleMouseMove)
      document.removeEventListener("mouseup", handleMouseUp)
    }

    document.addEventListener("mousemove", handleMouseMove)
    document.addEventListener("mouseup", handleMouseUp)
  }, [minLeftWidth, minRightWidth])

  const layouts: Array<{ value: PaneLayout; label: string; title: string }> = [
    ...(rightCollapsible
      ? [{ value: "left" as const, label: leftLabel, title: `Show ${leftLabel} only` }]
      : []),
    { value: "split", label: "Split", title: `Show ${leftLabel} and ${rightLabel}` },
    ...(leftCollapsible
      ? [{ value: "right" as const, label: rightLabel, title: `Show ${rightLabel} only` }]
      : []),
  ]

  const syncScroll = (source: HTMLDivElement, target: HTMLDivElement | null) => {
    if (!syncVerticalScroll || syncingScroll.current || !target) return
    syncingScroll.current = true
    target.scrollTop = source.scrollTop
    requestAnimationFrame(() => {
      syncingScroll.current = false
    })
  }

  return (
    <div className={cn("flex h-full min-h-0 flex-col overflow-hidden", className)}>
      <div className="flex shrink-0 items-center justify-center border-b bg-background/95 px-3 py-1.5">
        <div
          role="group"
          aria-label="Plan layout"
          className="inline-flex items-center rounded-md border bg-muted/40 p-0.5 shadow-sm"
        >
          {layouts.map((option) => (
            <button
              key={option.value}
              type="button"
              title={option.title}
              aria-pressed={layout === option.value}
              onClick={() => setLayout(option.value)}
              className={cn(
                "min-w-16 rounded px-3 py-1 text-[11px] font-medium text-muted-foreground transition-colors",
                "hover:bg-background/70 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1",
                layout === option.value && "bg-background text-foreground shadow-sm"
              )}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div ref={containerRef} className="relative flex min-h-0 flex-1 overflow-hidden">
        <div
          className="overflow-hidden transition-[width] duration-200"
          style={{
            width: layout === "right" ? "0%" : layout === "left" ? "100%" : `${leftWidth}%`,
            minWidth: layout === "right" ? 0 : layout === "split" ? `${minLeftWidth}%` : 0,
          }}
        >
          <div
            ref={leftScrollRef}
            className="h-full overflow-auto"
            onScroll={(event) => syncScroll(event.currentTarget, rightScrollRef.current)}
          >
            {layout !== "right" && left}
          </div>
        </div>
        {layout === "split" && (
          <div
            role="separator"
            aria-label={`Resize ${leftLabel} and ${rightLabel} panels`}
            aria-orientation="vertical"
            className="flex w-1.5 shrink-0 cursor-col-resize items-center justify-center bg-border/30 transition-colors hover:bg-border/60"
            onMouseDown={handleMouseDown}
          >
            <div className="h-8 w-0.5 rounded-full bg-muted-foreground/30" />
          </div>
        )}
        <div
          className="overflow-hidden transition-[width] duration-200"
          style={{
            width: layout === "left" ? "0%" : layout === "right" ? "100%" : `${100 - leftWidth}%`,
            minWidth: layout === "left" ? 0 : layout === "split" ? `${minRightWidth}%` : 0,
          }}
        >
          <div
            ref={rightScrollRef}
            className="h-full overflow-auto"
            onScroll={(event) => syncScroll(event.currentTarget, leftScrollRef.current)}
          >
            {layout !== "left" && right}
          </div>
        </div>
      </div>
    </div>
  )
}
