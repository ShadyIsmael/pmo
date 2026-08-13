"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Save } from "lucide-react";

export default function AdminSettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Settings" description="Global application configuration" actions={<Button size="sm"><Save className="mr-2 h-4 w-4" />Save Changes</Button>} />

      <Card>
        <CardHeader><CardTitle className="text-base">General</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Application Name</Label>
            <Input defaultValue="NEXUS PMO" />
          </div>
          <div className="space-y-2">
            <Label>Tagline</Label>
            <Input defaultValue="Plan. Govern. Execute. Optimize." />
          </div>
          <div className="space-y-2">
            <Label>Default Time Zone</Label>
            <Select defaultValue="london">
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="london">Europe/London (GMT+1)</SelectItem>
                <SelectItem value="dubai">Asia/Dubai (GMT+4)</SelectItem>
                <SelectItem value="newyork">America/New_York (GMT-4)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Separator />
          <div className="flex items-center justify-between">
            <div><Label>Enable Email Notifications</Label><p className="text-xs text-muted-foreground">Send email notifications for task assignments, approvals, and alerts</p></div>
            <Switch defaultChecked />
          </div>
          <div className="flex items-center justify-between">
            <div><Label>Enable Audit Logging</Label><p className="text-xs text-muted-foreground">Log all user actions for compliance and security</p></div>
            <Switch defaultChecked />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
