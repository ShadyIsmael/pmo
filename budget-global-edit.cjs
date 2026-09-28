const fs=require('fs');const root='apps/api/src/';
let p=root+'NEXUS.Modules.CostManagement/Application/GlobalFinancialSummaryService.cs';let s=fs.readFileSync(p,'utf8').replace(/\r/g,'');
let a=s.indexOf('        var projectIds ='),b=s.indexOf('        var byCurrency =');
s=s.slice(0,a)+`        var projects = await _projectLookup.GetFinancialReferencesAsync(cancellationToken);
        var budgetGroups = budgets.Where(b => b.ProjectId.HasValue).ToLookup(b => b.ProjectId!.Value);
        var costGroups = costEntries.Where(c => c.ProjectId.HasValue).ToLookup(c => c.ProjectId!.Value);
        var commitmentGroups = commitments.Where(c => c.ProjectId.HasValue).ToLookup(c => c.ProjectId!.Value);
        var byProject = projects.Select(project =>
        {
            var currency = NormalizeCurrency(project.Currency);
            var lines = budgetGroups[project.Id].Where(b => NormalizeCurrency(b.Currency) == currency).ToList();
            var allocated = lines.Sum(b => b.ApprovedAmount);
            var ceiling = project.CurrentApprovedProjectBudget ?? 0;
            var actual = costGroups[project.Id].Where(c => NormalizeCurrency(c.Currency) == currency).Sum(c => c.Amount);
            var committed = commitmentGroups[project.Id].Where(c => NormalizeCurrency(c.Currency) == currency && c.Status != "Cancelled" && c.Status != "Closed").Sum(c => c.Amount);
            var forecast = lines.Sum(b => b.ForecastAmount);
            return new ProjectFinancialBreakdown
            {
                ProjectId = project.Id, ProjectName = project.Name, Currency = currency,
                OriginalApprovedBudget = lines.Sum(b => originalAmounts.TryGetValue(b.Id, out var amount) ? amount : b.ApprovedAmount),
                ApprovedBudget = ceiling, AllocatedBudget = allocated, UnallocatedBudget = ceiling - allocated,
                ActualCost = actual, CommittedCost = committed, AvailableBudget = ceiling - actual - committed,
                ForecastAtCompletion = forecast, ForecastVariance = ceiling - forecast,
                BudgetUtilizationPercentage = ceiling > 0 ? actual / ceiling * 100m : null
            };
        }).OrderBy(p => p.Currency).ThenBy(p => p.ProjectName).ToList();

`+s.slice(b);
s=s.replace('                ApprovedBudget = g.Sum(p => p.ApprovedBudget),','                ApprovedBudget = g.Sum(p => p.ApprovedBudget),\n                AllocatedBudget = g.Sum(p => p.AllocatedBudget),\n                UnallocatedBudget = g.Sum(p => p.UnallocatedBudget),\n                AvailableBudget = g.Sum(p => p.AvailableBudget),');fs.writeFileSync(p,s);
p=root+'NEXUS.Modules.CostManagement/Application/DTOs/GlobalFinancialSummaryDtos.cs';s=fs.readFileSync(p,'utf8');s=s.replaceAll('    public decimal ApprovedBudget { get; init; }','    public decimal ApprovedBudget { get; init; }\n    public decimal AllocatedBudget { get; init; }\n    public decimal UnallocatedBudget { get; init; }\n    public decimal AvailableBudget { get; init; }');fs.writeFileSync(p,s);
