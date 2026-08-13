"use client";

import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Settings, Users, Shield, Key, Sliders, PuzzleIcon, FileText, ArrowRight } from "lucide-react";

const adminSections = [
  { id: "users", name: "Users", description: "Manage user accounts and profiles", icon: Users, href: "/administration/users", count: "24 users" },
  { id: "roles", name: "Roles & Permissions", description: "Define roles and access permissions", icon: Shield, href: "/administration/roles", count: "12 roles" },
  { id: "settings", name: "Settings", description: "Global application configuration", icon: Sliders, href: "/administration/settings", count: "28 settings" },
  { id: "personalization", name: "Personalization", description: "Branding, themes, and UI customization", icon: Settings, href: "/administration/personalization", count: "6 options" },
  { id: "plugins", name: "Plugins", description: "Manage installed plugins", icon: PuzzleIcon, href: "/administration/plugins", count: "3 installed" },
  { id: "license", name: "License", description: "License management and subscription", icon: FileText, href: "/administration/license", count: "Enterprise" },
];

export default function AdministrationPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Administration" description="Manage your NEXUS PMO instance" />

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {adminSections.map((section) => {
          const Icon = section.icon;
          return (
            <Link key={section.id} href={section.href}>
              <Card className="h-full transition-colors hover:bg-muted/50">
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <Icon className="h-6 w-6 text-muted-foreground" />
                    <span className="text-xs text-muted-foreground">{section.count}</span>
                  </div>
                  <CardTitle className="text-base mt-2">{section.name}</CardTitle>
                  <CardDescription>{section.description}</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="flex items-center text-sm text-primary">
                    Manage <ArrowRight className="ml-1 h-4 w-4" />
                  </div>
                </CardContent>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
