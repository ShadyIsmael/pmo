"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Save } from "lucide-react";

export default function PersonalizationPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Personalization" description="Branding, themes, and UI customization" actions={<Button size="sm"><Save className="mr-2 h-4 w-4" />Save Changes</Button>} />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle className="text-base">Branding</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2"><Label>Logo URL</Label><Input placeholder="https://..." /></div>
            <div className="space-y-2"><Label>Favicon URL</Label><Input placeholder="https://..." /></div>
            <div className="space-y-2"><Label>Primary Color</Label><Input type="color" defaultValue="#2563eb" className="h-10 w-20" /></div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-base">Theme</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2"><Label>Default Theme</Label>
              <Select defaultValue="system">
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="light">Light</SelectItem>
                  <SelectItem value="dark">Dark</SelectItem>
                  <SelectItem value="system">System</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center justify-between">
              <div><Label>Compact Mode</Label><p className="text-xs text-muted-foreground">Reduce spacing for a denser layout</p></div>
              <Switch />
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
