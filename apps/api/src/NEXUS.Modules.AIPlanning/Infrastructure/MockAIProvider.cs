using NEXUS.Modules.AIPlanning.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.AIPlanning.Infrastructure;

public class MockAIProvider : IAIProvider
{
    private static readonly string[] Templates =
    {
        "Based on the provided context, this plan recommends initiating a phased approach across 3 sprints with focus on core deliverables first. Key risks identified include resource constraints and timeline dependencies.",
        "Analysis suggests a hybrid Agile-Waterfall approach. The critical path includes requirements gathering (2 weeks), development (6 weeks), testing (2 weeks), and deployment (1 week). Budget estimate: £150,000 - £200,000.",
        "Recommended strategy: Phase 1 - Foundation (Weeks 1-4): Setup infrastructure, core team assembly. Phase 2 - Execution (Weeks 5-12): Feature development, iterative testing. Phase 3 - Launch (Weeks 13-14): UAT, deployment, training.",
        "The AI planning engine suggests the following structure: 1) Define success criteria, 2) Allocate resources across 3 workstreams, 3) Establish weekly cadence for reviews, 4) Implement risk monitoring from Day 1.",
        "Optimization analysis complete. Recommended resource allocation: Senior developers (40%), QA (20%), DevOps (15%), PM (10%), Business analysts (15%). Timeline: 10 weeks to MVP with 80% confidence."
    };

    private static readonly Random _random = new();

    public Task<Result<string>> GeneratePlanAsync(string context, string? parameters = null, CancellationToken cancellationToken = default)
    {
        var template = Templates[_random.Next(Templates.Length)];
        var plan = $"""
            # AI-Generated Plan

            Context: {context}
            Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
            Provider: Mock AI Provider

            ## Summary
            {template}

            ## Key Deliverables
            1. Project Charter and Scope Definition
            2. Technical Architecture Document
            3. Sprint Backlog (estimated: 120 story points)
            4. Testing Strategy and Plan
            5. Deployment Runbook

            ## Resource Requirements
            - Project Manager: 1 FTE
            - Senior Developers: 3 FTE
            - QA Engineers: 2 FTE
            - DevOps: 1 FTE
            - Business Analyst: 1 FTE

            ## Estimated Budget
            - Personnel: £180,000
            - Infrastructure: £25,000
            - Tools & Licenses: £15,000
            - Contingency (15%): £33,000
            - Total: £253,000

            ## Timeline
            - Duration: 14 weeks
            - Start: {DateTime.UtcNow.AddDays(14):yyyy-MM-dd}
            - End: {DateTime.UtcNow.AddDays(112):yyyy-MM-dd}

            ## Risks
            1. Resource availability (Medium) - Mitigation: Cross-training
            2. Scope creep (High) - Mitigation: Strict change control
            3. Technology dependencies (Medium) - Mitigation: Early prototyping
            """;

        return Task.FromResult(Result<string>.Success(plan));
    }
}
