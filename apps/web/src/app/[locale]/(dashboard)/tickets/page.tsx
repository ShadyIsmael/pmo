"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, Search, CheckCircle2, Clock, AlertCircle } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockTickets } from "@/lib/mock-data";

export default function TicketsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Tickets"
        description={`${mockTickets.length} total tickets`}
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Ticket</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Open</CardTitle>
            <AlertCircle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-red">{mockTickets.filter(t => t.status === "Open").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">In Progress</CardTitle>
            <Clock className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-amber">{mockTickets.filter(t => t.status === "In Progress").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Resolved</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-green">{mockTickets.filter(t => t.status === "Resolved").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Critical</CardTitle>
            <AlertCircle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-red">{mockTickets.filter(t => t.priority === "Critical").length}</div></CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1"><Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input placeholder="Search tickets..." className="pl-8" /></div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="open">Open</TabsTrigger>
          <TabsTrigger value="resolved">Resolved</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="nexus-table-surface">
            <div className="nexus-table-header grid grid-cols-12 gap-2">
              <div className="col-span-4">Title</div>
              <div className="col-span-2">Priority</div>
              <div className="col-span-2">Assignee</div>
              <div className="col-span-2">Created</div>
              <div className="col-span-2">Status</div>
            </div>
            {mockTickets.map((ticket) => (
              <div key={ticket.id} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
                <div className="col-span-4 font-medium">{ticket.title}</div>
                <div className="col-span-2">
                  <Badge status={ticket.priority} className="text-[10px]">{ticket.priority}</Badge>
                </div>
                <div className="col-span-2 text-muted-foreground">{ticket.assignee}</div>
                <div className="col-span-2 text-muted-foreground text-xs">{formatDate(ticket.createdAt)}</div>
                <div className="col-span-2">
                  <Badge status={ticket.status} className="text-[10px]">{ticket.status}</Badge>
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="open">
          <div className="nexus-table-surface">
            {mockTickets.filter(t => t.status === "Open" || t.status === "In Progress").map((ticket) => (
              <div key={ticket.id} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
                <div className="col-span-4 font-medium">{ticket.title}</div>
                <div className="col-span-2"><Badge status={ticket.priority} className="text-[10px]">{ticket.priority}</Badge></div>
                <div className="col-span-3 text-muted-foreground">{ticket.assignee}</div>
                <div className="col-span-3"><Badge status={ticket.status} className="text-[10px]">{ticket.status}</Badge></div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="resolved">
          <div className="nexus-table-surface">
            {mockTickets.filter(t => t.status === "Resolved" || t.status === "Closed").map((ticket) => (
              <div key={ticket.id} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
                <div className="col-span-6 font-medium">{ticket.title}</div>
                <div className="col-span-3 text-muted-foreground">{ticket.assignee}</div>
                <div className="col-span-3"><Badge status={ticket.status} className="text-[10px]">{ticket.status}</Badge></div>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
