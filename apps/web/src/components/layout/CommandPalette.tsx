"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
} from "@/components/ui/command";
import { navigationConfig } from "@/lib/navigation";

interface CommandPaletteProps {
  open: boolean;
  onOpenChange: (_next: boolean) => void;
}

export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const router = useRouter();
  const t = useTranslations("nav");

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Search projects, tasks, documents..." />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>
        {navigationConfig.map((section) => (
          <CommandGroup key={section.id} heading={t(section.labelKey)}>
            {section.items.map((item) => (
              <CommandItem
                key={item.id}
                onSelect={() => {
                  router.push(item.href);
                  onOpenChange(false);
                }}
              >
                <item.icon className="me-2 h-4 w-4" />
                <span>{t(item.labelKey)}</span>
              </CommandItem>
            ))}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}
