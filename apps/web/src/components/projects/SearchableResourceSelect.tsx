"use client";

import * as React from "react";
import { useDebouncedCallback } from "use-debounce";
import { Loader2, UserRound, Mail, Briefcase } from "lucide-react";
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
import { useResources } from "@/hooks/resources/useResources";
import { Check, ChevronsUpDown, X } from "lucide-react";

export interface ResourceSearchOption {
  value: string;
  label: string;
  email?: string;
  type?: string;
  jobTitle?: string;
  userId?: string | null;
  disabled?: boolean;
  disabledReason?: string;
}

interface SearchableResourceSelectProps {
  value?: string;
  onValueChange?: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  className?: string;
  disabled?: boolean;
  error?: boolean;
  clearable?: boolean;
  /** Currently active project people to disable in results */
  activeProjectPeople?: { resourceId: string; relationshipType: string }[];
  /** Historical relationships for the selected resource */
  historicalRelationships?: { relationshipType: string; status: string }[];
  /** Current project sponsor to show conflict */
  currentSponsor?: { resourceId: string } | null;
  /** Currently selected relationship type to check historical match */
  selectedRelationshipType?: string;
  /** Optional scoped options supplied by a canonical domain source such as Project People. */
  staticOptions?: ResourceSearchOption[];
  loading?: boolean;
  clearLabel?: string;
}

export function SearchableResourceSelect({
  value,
  onValueChange,
  placeholder = "Select...",
  searchPlaceholder = "Search...",
  emptyText = "No results found.",
  className,
  disabled,
  error = false,
  clearable = false,
  activeProjectPeople = [],
  historicalRelationships = [],
  currentSponsor = null,
  selectedRelationshipType = "TeamMember",
  staticOptions,
  loading = false,
  clearLabel = "Clear selection",
}: SearchableResourceSelectProps) {
  const [open, setOpen] = React.useState(false);
  const [searchTerm, setSearchTerm] = React.useState("");
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const [portalContainer, setPortalContainer] = React.useState<HTMLElement | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  const resources = useResources(
    { search: searchTerm || undefined, page: 1, pageSize: 20, isActive: true },
    open && !staticOptions
  );

  const emitValueChange = onValueChange ?? (() => undefined);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setPortalContainer(
        (triggerRef.current?.closest('[role="dialog"]') as HTMLElement | null) ?? null
      );
    }
    setOpen(nextOpen);
  };

  const handleSearchChange = useDebouncedCallback((term: string) => {
    setSearchTerm(term);
  }, 300);

  const isResourceActiveInProject = (resourceId: string, relationshipType: string) => {
    return activeProjectPeople.some(
      (p) => p.resourceId === resourceId && p.relationshipType === relationshipType
    );
  };

  const hasHistoricalRelationship = (resourceId: string, relationshipType: string) => {
    return historicalRelationships.some(
      (h) => h.relationshipType === relationshipType && h.status !== "Active"
    );
  };

  const buildOptions = (): ResourceSearchOption[] => {
    const items = resources.data?.items ?? [];
    return items.map((resource) => {
      const isActiveDuplicate = isResourceActiveInProject(resource.id, selectedRelationshipType);
      const isSponsorConflict = currentSponsor && selectedRelationshipType === "Sponsor" && resource.id === currentSponsor.resourceId;
      const disabled = Boolean(isActiveDuplicate || isSponsorConflict);
      let disabledReason = "";
      if (isActiveDuplicate) disabledReason = "Already in project";
      if (isSponsorConflict) disabledReason = "Sponsor already assigned";

      return {
        value: resource.id,
        label: resource.name,
        email: resource.email,
        type: resource.type,
        jobTitle: resource.jobTitle,
        userId: resource.userId ?? undefined,
        disabled,
        disabledReason,
      };
    });
  };

  const remoteOptions = buildOptions();
  const options = staticOptions
    ? staticOptions.filter((option) => {
        const query = searchTerm.trim().toLowerCase();
        return !query || `${option.label} ${option.email ?? ""} ${option.jobTitle ?? ""} ${option.type ?? ""}`.toLowerCase().includes(query);
      })
    : remoteOptions;
  const selectedResource = options.find((option) => option.value === value);

  const handleSelect = (optionValue: string) => {
    const option = options.find((o) => o.value === optionValue);
    if (option?.disabled) return;
    emitValueChange(optionValue === value ? "" : optionValue);
    handleOpenChange(false);
  };

  const handleClear = (event: React.MouseEvent) => {
    event.stopPropagation();
    emitValueChange("");
    setSearchTerm("");
    if (inputRef.current) {
      inputRef.current.value = "";
    }
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
          <span className={cn("min-w-0 truncate", !selectedResource && !value && "text-muted-foreground")}>
            {selectedResource ? selectedResource.label : value ? "Selected" : placeholder}
          </span>
          <span className="ms-2 flex shrink-0 items-center gap-1">
            {clearable && (selectedResource || value) && (
              <X className="h-3.5 w-3.5 opacity-60 hover:opacity-100" onClick={handleClear} aria-label={clearLabel} />
            )}
            <ChevronsUpDown className="h-4 w-4 opacity-50" />
          </span>
        </Button>
      </PopoverTrigger>
      <PopoverContent
        container={portalContainer}
        className="z-[70] w-[var(--radix-popover-trigger-width)] min-w-[280px] max-w-[400px] p-0"
        align="start"
        sideOffset={5}
      >
        <Command>
          <CommandInput
            ref={inputRef}
            placeholder={searchPlaceholder}
            onValueChange={handleSearchChange}
            autoFocus
          />
          <CommandList>
            {loading || (!staticOptions && resources.isLoading) ? (
              <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span data-testid="search-loading">Searching...</span>
              </div>
            ) : options.length === 0 ? (
              <div className="py-6 text-center">
                <p className="text-sm text-muted-foreground">{emptyText}</p>
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                <CommandGroup>
                  {options.map((option) => (
                    <CommandItem
                      key={option.value}
                      value={option.value}
                      keywords={[option.label, option.email, option.jobTitle, option.type].filter(Boolean) as string[]}
                      disabled={option.disabled}
                      onSelect={() => handleSelect(option.value)}
                      className={cn(option.disabled && "opacity-50 cursor-not-allowed")}
                    >
                      <Check
                        className={cn(
                          "me-2 h-4 w-4",
                          value === option.value ? "opacity-100" : "opacity-0"
                        )}
                      />
                      <div className="flex flex-col min-w-0">
                        <span className="truncate font-medium">{option.label}</span>
                        <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                          {option.email && (
            <span className="flex items-center gap-1">
              <Mail className="h-3 w-3" />
              {option.email}
            </span>
                          )}
                          {option.type && (
            <span className="flex items-center gap-1">
              <Briefcase className="h-3 w-3" />
              {option.type}
            </span>
                          )}
                          {option.jobTitle && (
            <span className="flex items-center gap-1">
              <UserRound className="h-3 w-3" />
              {option.jobTitle}
            </span>
                          )}
                          {option.userId && (
            <span className="flex items-center gap-1 text-primary">
              <UserRound className="h-3 w-3" />
              System User
            </span>
                          )}
                          {option.disabledReason && (
            <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400">
              <span className="h-3 w-3" />
              {option.disabledReason}
            </span>
                          )}
                        </div>
                      </div>
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
