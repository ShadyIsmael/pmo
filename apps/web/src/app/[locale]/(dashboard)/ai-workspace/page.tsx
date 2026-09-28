"use client";

import { useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Bot, Send, Sparkles, ArrowRight, FileText, BarChart3 } from "lucide-react";

const suggestions = [
  "Create a project plan for a cloud migration initiative",
  "Analyse risks in the Core Banking Modernisation project",
  "Summarise the current portfolio health",
  "Generate a status report for the executive team",
  "Identify resource conflicts across active projects",
  "Draft meeting minutes from the last steering committee",
];

export default function AIWorkspacePage() {
  const [prompt, setPrompt] = useState("");
  const [messages, setMessages] = useState<{ role: string; content: string }[]>([
    { role: "ai", content: "Hello! I'm your NEXUS PMO AI Copilot. How can I help you manage your projects today?" },
  ]);

  const handleSend = () => {
    if (!prompt.trim()) return;
    setMessages(prev => [...prev, { role: "user", content: prompt }]);
    setTimeout(() => {
      setMessages(prev => [...prev, { role: "ai", content: "I've analysed your request. Based on the portfolio data, I can help you with that. Would you like me to generate a detailed plan or provide a summary?" }]);
    }, 1000);
    setPrompt("");
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="AI Copilot"
        description="Your intelligent project management assistant"
      />

      <div className="grid gap-4 lg:grid-cols-4">
        <div className="lg:col-span-3">
          <Card className="flex h-[500px] flex-col">
            <CardHeader className="border-b">
              <div className="flex items-center gap-2">
                <Bot className="h-5 w-5 text-primary" />
                <CardTitle className="text-base">Conversation</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="flex-1 overflow-auto p-4">
              <div className="space-y-4">
                {messages.map((msg, i) => (
                  <div key={i} className={`flex gap-3 ${msg.role === "user" ? "justify-end" : ""}`}>
                    {msg.role === "ai" && <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10"><Bot className="h-4 w-4 text-primary" /></div>}
                    <div className={`max-w-[80%] rounded-lg px-4 py-2 text-sm ${msg.role === "user" ? "bg-primary text-primary-foreground" : "bg-muted"}`}>
                      {msg.content}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
            <div className="border-t p-4">
              <div className="flex gap-2">
                <Input
                  placeholder="Ask me anything about your projects..."
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && handleSend()}
                />
                <Button onClick={handleSend}><Send className="h-4 w-4" /></Button>
              </div>
            </div>
          </Card>
        </div>

        <div className="space-y-3">
          <Card>
            <CardHeader><CardTitle className="text-sm">Quick Actions</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              <Link href="/ai-workspace/plan-generator">
                <Button variant="outline" className="w-full justify-start"><Sparkles className="mr-2 h-4 w-4" />Generate Plan</Button>
              </Link>
              <Button variant="outline" className="w-full justify-start"><FileText className="mr-2 h-4 w-4" />Draft Report</Button>
              <Button variant="outline" className="w-full justify-start"><BarChart3 className="mr-2 h-4 w-4" />Analyse Risks</Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle className="text-sm">Suggestions</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              {suggestions.slice(0, 4).map((s) => (
                <button key={s} onClick={() => setPrompt(s)} className="w-full rounded-md bg-muted px-3 py-2 text-left text-xs text-muted-foreground hover:bg-muted/80 transition-colors">
                  {s}
                </button>
              ))}
            </CardContent>
          </Card>
          <Link href="/ai-workspace/insights">
            <Card className="cursor-pointer hover:bg-muted/50 transition-colors">
              <CardContent className="flex items-center gap-3 py-4">
                <BarChart3 className="h-5 w-5 text-primary" />
                <div className="flex-1"><p className="text-sm font-medium">AI Insights</p><p className="text-xs text-muted-foreground">View generated insights</p></div>
                <ArrowRight className="h-4 w-4 text-muted-foreground" />
              </CardContent>
            </Card>
          </Link>
        </div>
      </div>
    </div>
  );
}
