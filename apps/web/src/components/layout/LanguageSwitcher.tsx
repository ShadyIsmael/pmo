"use client";

import * as React from "react";
import { useLocale } from "next-intl";
import { Globe, Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { usePathname, useRouter } from "@/navigation";
import { LOCALE_NAMES, SUPPORTED_LOCALES } from "@/lib/constants";

export function LanguageSwitcher() {
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();

  function handleLocaleChange(nextLocale: string) {
    if (nextLocale === locale) return;
    const search = window.location.search;
    router.push(search ? `${pathname}${search}` : pathname, { locale: nextLocale });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="text-muted-foreground h-9 w-9" aria-label="Change language">
          <Globe className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel className="px-2 py-1 text-xs font-medium text-muted-foreground">
          Language
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {SUPPORTED_LOCALES.map((code) => (
          <DropdownMenuItem
            key={code}
            className="rounded-md px-2 py-1.5 text-sm gap-2"
            onSelect={() => handleLocaleChange(code)}
          >
            <span className="text-base">{code === "en" ? "🇬🇧" : "🇸🇦"}</span>
            {LOCALE_NAMES[code]}
            {code === locale && <Check className="ms-auto h-4 w-4 text-muted-foreground" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
