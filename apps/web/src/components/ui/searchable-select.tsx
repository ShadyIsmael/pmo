"use client";

import * as React from "react";
import { Check, ChevronsUpDown, Loader2, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Badge } from "@/components/ui/badge";

export interface SelectOption {
  value: string;
  label: string;
  /** Additional terms matched by the search input (e.g. email, job title). */
  keywords?: string[];
}

interface SearchableSelectProps {
  options: SelectOption[];
  value?: string;
  onValueChange?: (value: string) => void;
  onSelect?: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  loading?: boolean;
  /** Optional node rendered below the empty state (e.g. a "Manage users" link). */
  emptyAction?: React.ReactNode;
  className?: string;
  disabled?: boolean;
  error?: boolean;
  clearable?: boolean;
}

export function SearchableSelect({
  options,
  value,
  onValueChange,
  onSelect,
  placeholder = "Select...",
  searchPlaceholder = "Search...",
  emptyText = "No results found.",
  loading = false,
  emptyAction,
  className,
  disabled,
  error = false,
  clearable = false,
}: SearchableSelectProps) {
  const [open, setOpen] = React.useState(false);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const [portalContainer, setPortalContainer] = React.useState<HTMLElement | null>(null);
  const selected = options.find((o) => o.value === value);
  const emitValueChange = onValueChange ?? onSelect ?? (() => undefined);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setPortalContainer(
        (triggerRef.current?.closest('[role="dialog"]') as HTMLElement | null) ?? null
      );
    }
    setOpen(nextOpen);
  };

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          ref={triggerRef}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-invalid={error || undefined}
          className={cn("w-full justify-between font-normal", error && "border-destructive focus-visible:ring-destructive", className)}
          disabled={disabled}
        >
          <span className={cn("min-w-0 truncate", !selected && "text-muted-foreground")}>{selected ? selected.label : placeholder}</span>
          <span className="ms-2 flex shrink-0 items-center gap-1">
            {clearable && selected && <X className="h-3.5 w-3.5 opacity-60 hover:opacity-100" onClick={(event) => { event.stopPropagation(); emitValueChange(""); }} aria-label="Clear selection" />}
            <ChevronsUpDown className="h-4 w-4 opacity-50" />
          </span>
        </Button>
      </PopoverTrigger>
      <PopoverContent
        container={portalContainer}
        className="z-[70] w-[var(--radix-popover-trigger-width)] min-w-[200px] p-0"
        align="start"
      >
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            {loading ? (
              <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading...
              </div>
            ) : options.length === 0 ? (
              <div className="py-6 text-center">
                <p className="text-sm text-muted-foreground">{emptyText}</p>
                {emptyAction && <div className="mt-3">{emptyAction}</div>}
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                <CommandGroup>
                  {options.map((option) => (
                    <CommandItem
                      key={option.value}
                      value={option.value}
                      keywords={[option.label, ...(option.keywords ?? [])]}
                      onSelect={() => {
                        emitValueChange(option.value === value ? "" : option.value);
                        handleOpenChange(false);
                      }}
                    >
                      <Check
                        className={cn(
                          "me-2 h-4 w-4",
                          value === option.value ? "opacity-100" : "opacity-0"
                        )}
                      />
                      {option.label}
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

interface SearchableMultiSelectProps {
  options: SelectOption[];
  values: string[];
  onValueChange?: (values: string[]) => void;
  onSelect?: (values: string[]) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  loading?: boolean;
  className?: string;
  disabled?: boolean;
  error?: boolean;
}

export function SearchableMultiSelect({
  options,
  values,
  onValueChange,
  onSelect,
  placeholder = "Select...",
  searchPlaceholder = "Search...",
  emptyText = "No results found.",
  loading = false,
  className,
  disabled,
  error = false,
}: SearchableMultiSelectProps) {
  const [open, setOpen] = React.useState(false);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const [portalContainer, setPortalContainer] = React.useState<HTMLElement | null>(null);
  const emitValueChange = onValueChange ?? onSelect ?? (() => undefined);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setPortalContainer(
        (triggerRef.current?.closest('[role="dialog"]') as HTMLElement | null) ?? null
      );
    }
    setOpen(nextOpen);
  };

  const toggleValue = (val: string) => {
    if (values.includes(val)) {
      emitValueChange(values.filter((v) => v !== val));
    } else {
      emitValueChange([...values, val]);
    }
  };

  const removeValue = (val: string) => {
    emitValueChange(values.filter((v) => v !== val));
  };

  const selectedLabels = options
    .filter((o) => values.includes(o.value))
    .map((o) => o.label);

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          ref={triggerRef}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className={cn(
            "w-full justify-between font-normal h-auto min-h-9",
            error && "border-destructive focus-visible:ring-destructive",
            className
          )}
          disabled={disabled}
        >
          <div className="flex flex-wrap gap-1">
            {selectedLabels.length > 0 ? (
              selectedLabels.map((label, i) => (
                <Badge
                  key={i}
                  variant="secondary"
                  className="text-[10px] gap-1"
                >
                  {label}
                  <X
                    className="h-3 w-3 cursor-pointer"
                    onClick={(e) => {
                      e.stopPropagation();
                      const opt = options.find((o) => o.label === label);
                      if (opt) removeValue(opt.value);
                    }}
                  />
                </Badge>
              ))
            ) : (
              <span className="text-muted-foreground">{placeholder}</span>
            )}
          </div>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        container={portalContainer}
        className="z-[70] w-[var(--radix-popover-trigger-width)] min-w-[200px] p-0"
        align="start"
      >
        <Command>
          <CommandInput placeholder={searchPlaceholder} />
          <CommandList>
            {loading ? (
              <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading...
              </div>
            ) : options.length === 0 ? (
              <div className="py-6 text-center text-sm text-muted-foreground">
                {emptyText}
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                <CommandGroup>
                  {options.map((option) => (
                    <CommandItem
                      key={option.value}
                      value={option.value}
                      keywords={[option.label, ...(option.keywords ?? [])]}
                      onSelect={() => toggleValue(option.value)}
                    >
                      <Check
                        className={cn(
                          "mr-2 h-4 w-4",
                          values.includes(option.value)
                            ? "opacity-100"
                            : "opacity-0"
                        )}
                      />
                      {option.label}
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
