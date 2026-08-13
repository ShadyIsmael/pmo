"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Search, Filter, FileText, FolderOpen, Download, Upload, MoreHorizontal } from "lucide-react";
import { formatDate } from "@/lib/utils";

const documents = [
  { id: "doc-1", name: "Project Charter - Core Banking Modernisation", type: "PDF", size: "2.4 MB", project: "Core Banking Modernisation", uploadedBy: "Daniel O'Brien", uploadedAt: "2026-07-15", category: "Governance" },
  { id: "doc-2", name: "Architecture Design Document v2.1", type: "DOCX", size: "4.8 MB", project: "Core Banking Modernisation", uploadedBy: "Raj Kapoor", uploadedAt: "2026-07-20", category: "Technical" },
  { id: "doc-3", name: "Risk Register - Q3 2026", type: "XLSX", size: "1.2 MB", project: "All Projects", uploadedBy: "Rachel Green", uploadedAt: "2026-07-22", category: "Risk" },
  { id: "doc-4", name: "Steering Committee Minutes - July", type: "PDF", size: "0.8 MB", project: "Core Banking Modernisation", uploadedBy: "Daniel O'Brien", uploadedAt: "2026-07-25", category: "Meeting" },
  { id: "doc-5", name: "Budget Review - FY2026 Q3", type: "XLSX", size: "3.1 MB", project: "ERP Cloud Migration", uploadedBy: "Sophie Turner", uploadedAt: "2026-07-18", category: "Financial" },
  { id: "doc-6", name: "Test Plan - Omni-Channel Hub", type: "PDF", size: "1.5 MB", project: "Omni-Channel Customer Hub", uploadedBy: "Liam O'Connor", uploadedAt: "2026-07-21", category: "Testing" },
  { id: "doc-7", name: "Vendor Evaluation Scorecard", type: "XLSX", size: "0.6 MB", project: "AI-Powered Risk Analytics", uploadedBy: "Raj Kapoor", uploadedAt: "2026-07-19", category: "Procurement" },
  { id: "doc-8", name: "Deployment Runbook v1.0", type: "PDF", size: "3.2 MB", project: "Data Centre Consolidation", uploadedBy: "Nadia Khan", uploadedAt: "2026-07-23", category: "Technical" },
];

export default function DocumentsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Document Centre"
        description={`${documents.length} documents across all projects`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Upload className="mr-2 h-4 w-4" />Upload</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />New Folder</Button>
          </div>
        }
      />

      <div className="flex items-center gap-2">
        <div className="relative flex-1"><Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input placeholder="Search documents..." className="pl-8" /></div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All Documents</TabsTrigger>
          <TabsTrigger value="governance">Governance</TabsTrigger>
          <TabsTrigger value="technical">Technical</TabsTrigger>
          <TabsTrigger value="financial">Financial</TabsTrigger>
          <TabsTrigger value="meeting">Meeting</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-4">Name</div>
              <div className="col-span-1">Type</div>
              <div className="col-span-1">Size</div>
              <div className="col-span-2">Project</div>
              <div className="col-span-1">Category</div>
              <div className="col-span-2">Uploaded</div>
              <div className="col-span-1"></div>
            </div>
            {documents.map((doc) => (
              <div key={doc.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 flex items-center gap-2 font-medium">
                  <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                  {doc.name}
                </div>
                <div className="col-span-1 text-muted-foreground">{doc.type}</div>
                <div className="col-span-1 text-muted-foreground">{doc.size}</div>
                <div className="col-span-2 truncate text-muted-foreground">{doc.project}</div>
                <div className="col-span-1"><Badge variant="secondary" className="text-[10px]">{doc.category}</Badge></div>
                <div className="col-span-2 text-xs text-muted-foreground">
                  <p>{doc.uploadedBy}</p>
                  <p>{formatDate(doc.uploadedAt)}</p>
                </div>
                <div className="col-span-1 flex items-center gap-1">
                  <Button variant="ghost" size="icon" className="h-7 w-7"><Download className="h-3.5 w-3.5" /></Button>
                  <Button variant="ghost" size="icon" className="h-7 w-7"><MoreHorizontal className="h-3.5 w-3.5" /></Button>
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        {["governance", "technical", "financial", "meeting"].map(cat => (
          <TabsContent key={cat} value={cat}>
            <div className="rounded-md border">
              {documents.filter(d => d.category.toLowerCase() === cat).map((doc) => (
                <div key={doc.id} className="flex items-center gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                  <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium">{doc.name}</p>
                    <p className="text-xs text-muted-foreground">{doc.type} · {doc.size} · {doc.project}</p>
                  </div>
                  <Button variant="ghost" size="icon" className="h-7 w-7"><Download className="h-3.5 w-3.5" /></Button>
                </div>
              ))}
            </div>
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
