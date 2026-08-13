"use client"

import { Dialog, DialogContent, DialogHeader, DialogBody, DialogTitle, DialogDescription } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Label } from "@/components/ui/label"
import { GripVertical } from "lucide-react"
import type { ColumnDef } from "./TaskGrid"

interface ColumnChooserProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  columns: ColumnDef[]
  onToggleColumn: (columnId: string) => void
}

export function ColumnChooser({ open, onOpenChange, columns, onToggleColumn }: ColumnChooserProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[400px]">
        <DialogHeader>
          <DialogTitle>Column Chooser</DialogTitle>
          <DialogDescription>Select columns to display in the task grid.</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-1">
          {columns.map((col) => (
            <div key={col.id} className="flex items-center gap-2 rounded px-2 py-1.5 hover:bg-muted text-sm">
              <GripVertical className="h-3.5 w-3.5 shrink-0 text-muted-foreground/40" />
              <Checkbox
                id={`col-${col.id}`}
                checked={col.visible}
                onCheckedChange={() => onToggleColumn(col.id)}
              />
              <Label htmlFor={`col-${col.id}`} className="flex-1 cursor-pointer text-xs">
                {col.label}
              </Label>
              <span className="text-[10px] text-muted-foreground/60">{col.width}px</span>
            </div>
          ))}
        </DialogBody>
      </DialogContent>
    </Dialog>
  )
}
