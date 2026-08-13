"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { FileText, Download } from "lucide-react";
import { formatDate, getInitials } from "@/lib/utils";
import { mockMeetings, mockUsers } from "@/lib/mock-data";

export default function MeetingMinutesPage() {
  const completed = mockMeetings.filter(m => m.status === "Completed");

  return (
    <div className="space-y-6">
      <PageHeader title="Meeting Minutes" description="Records of completed meetings" />

      <div className="space-y-3">
        {completed.slice(0, 8).map((meeting) => {
          const creator = mockUsers.find(u => u.id === meeting.createdBy);
          return (
            <Card key={meeting.id}>
              <CardContent className="flex items-start gap-4 pt-6">
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                  <FileText className="h-5 w-5 text-muted-foreground" />
                </div>
                <div className="flex-1">
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="font-medium">{meeting.title}</h3>
                      <CardDescription>{formatDate(meeting.startDate)}</CardDescription>
                    </div>
                    <div className="flex gap-2">
                      <Button size="sm" variant="outline"><Download className="mr-1 h-4 w-4" />Download</Button>
                      <Button size="sm" variant="outline">View</Button>
                    </div>
                  </div>
                  <div className="mt-2 flex items-center gap-3 text-xs text-muted-foreground">
                    <span>{meeting.type}</span>
                    <span>·</span>
                    <span>{meeting.location}</span>
                    <span>·</span>
                    <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{creator ? getInitials(creator.name) : ""}</AvatarFallback></Avatar>
                    <span>{creator?.name}</span>
                    <span>·</span>
                    <span>{meeting.attendees.length} attendees</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
