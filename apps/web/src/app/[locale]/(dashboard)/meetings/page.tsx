"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, CalendarCheck, Video, MapPin, Clock } from "lucide-react";
import { formatDate, getInitials } from "@/lib/utils";
import { mockMeetings, mockUsers } from "@/lib/mock-data";

export default function MeetingsPage() {
  const upcoming = mockMeetings.filter(m => m.status === "Scheduled" || m.status === "In Progress");
  const past = mockMeetings.filter(m => m.status === "Completed" || m.status === "Cancelled");

  return (
    <div className="space-y-6">
      <PageHeader
        title="Meetings"
        description={`${upcoming.length} upcoming meetings`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />Schedule Meeting</Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Upcoming</CardTitle>
            <CalendarCheck className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{upcoming.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">This Week</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{mockMeetings.filter(m => m.status === "Scheduled").slice(0, 3).length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Completed</CardTitle>
            <CalendarCheck className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-green">{past.filter(m => m.status === "Completed").length}</div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="upcoming">
        <TabsList>
          <TabsTrigger value="upcoming">Upcoming ({upcoming.length})</TabsTrigger>
          <TabsTrigger value="past">Past ({past.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="upcoming">
          <div className="space-y-3">
            {upcoming.map((meeting) => {
              const creator = mockUsers.find(u => u.id === meeting.createdBy);
              return (
                <Card key={meeting.id}>
                  <CardContent className="flex items-start gap-4 pt-6">
                    <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10">
                      <CalendarCheck className="h-6 w-6 text-primary" />
                    </div>
                    <div className="flex-1">
                      <div className="flex items-start justify-between">
                        <div>
                          <h3 className="font-medium">{meeting.title}</h3>
                          <p className="text-sm text-muted-foreground">{meeting.description}</p>
                        </div>
                        <Badge variant={meeting.status === "In Progress" ? "default" : "secondary"} className="text-[10px]">{meeting.status}</Badge>
                      </div>
                      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
                        <span className="flex items-center gap-1"><CalendarCheck className="h-3.5 w-3.5" />{formatDate(meeting.startDate)}</span>
                        <span className="flex items-center gap-1"><Clock className="h-3.5 w-3.5" />{meeting.startDate}</span>
                        <span className="flex items-center gap-1">
                          {meeting.location.startsWith("Virtual") ? <Video className="h-3.5 w-3.5" /> : <MapPin className="h-3.5 w-3.5" />}
                          {meeting.location}
                        </span>
                        <span className="flex items-center gap-1">
                          <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{creator ? getInitials(creator.name) : ""}</AvatarFallback></Avatar>
                          {creator?.name}
                        </span>
                        <span>{meeting.attendees.length} attendees</span>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="past">
          <div className="space-y-3">
            {past.slice(0, 5).map((meeting) => {
              const creator = mockUsers.find(u => u.id === meeting.createdBy);
              return (
                <Card key={meeting.id}>
                  <CardContent className="flex items-start gap-4 pt-6">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                      <CalendarCheck className="h-5 w-5 text-muted-foreground" />
                    </div>
                    <div className="flex-1">
                      <div className="flex items-center justify-between">
                        <h3 className="font-medium">{meeting.title}</h3>
                        <Badge variant="outline" className="text-[10px]">{meeting.status}</Badge>
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {formatDate(meeting.startDate)} · {meeting.location} · {creator?.name}
                      </p>
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
