const fs=require('fs');const root='apps/api/src/';
function edit(p,f){const s=fs.readFileSync(root+p,'utf8').replace(/\r/g,'');fs.writeFileSync(root+p,f(s));}
function tx(s,method){const start=s.indexOf(' '+method+'(');const brace=s.indexOf('\n    {',start)+6;return s.slice(0,brace)+'\n        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);'+s.slice(brace);}
edit('NEXUS.Modules.CostManagement/Application/BudgetService.cs',s=>{
for(const m of ['CreateAsync','UpdateAsync'])s=tx(s,m);
s=s.replace('        var budget = new Budget','        var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, dto.ProjectId, null, dto.ApprovedAmount, cancellationToken);\n        if (allocationError is not null) return Result<BudgetDto>.Failure(allocationError);\n        var budget = new Budget');
s=s.replace('        var original = JsonSerializer.Serialize(budget.ToDto());','        if (dto.ApprovedAmount != budget.ApprovedAmount)\n        {\n            var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, budget.ProjectId, id, dto.ApprovedAmount, cancellationToken);\n            if (allocationError is not null) return Result<BudgetDto>.Failure(allocationError);\n        }\n        var original = JsonSerializer.Serialize(budget.ToDto());');
s=s.replace('await _context.SaveChangesAsync(cancellationToken);\n        await AuditAsync("Budget", budget.Id, "Create"','await _context.SaveChangesAsync(cancellationToken);\n        await transaction.CommitAsync(cancellationToken);\n        await AuditAsync("Budget", budget.Id, "Create"');
s=s.replace('await _context.SaveChangesAsync(cancellationToken); await AuditAsync("Budget", budget.Id, "Update"','await _context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); await AuditAsync("Budget", budget.Id, "Update"');return s;});
edit('NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs',s=>{
s=tx(s,'ApproveAsync');s=s.replace('        var originalAmount = revision.Budget.ApprovedAmount;',`        var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, revision.Budget.ProjectId, revision.BudgetId, revision.ProposedApprovedAmount, cancellationToken);
        if (allocationError is not null) return Result.Failure(allocationError);
        if (revision.PreviousApprovedAmount != revision.Budget.ApprovedAmount)
            return Result.Failure(Error.Conflict("The budget allocation has changed. Cancel and resubmit the revision."));
        var originalAmount = revision.Budget.ApprovedAmount;`);
s=s.replace('await AuditAsync("BudgetRevision", revision.Id, "Approve"','await transaction.CommitAsync(cancellationToken);\n        await AuditAsync("BudgetRevision", revision.Id, "Approve"');return s;});
edit('NEXUS.Modules.CostManagement/Application/CommitmentService.cs',s=>{
for(const m of ['CreateAsync','UpdateAsync'])s=tx(s,m);
s=s.replace('        var commitment = new PurchaseCommitment',`        var capacityError = await BudgetAuthority.ValidateCommitmentAsync(_context, _projectLookup, dto.ProjectId, null, dto.Amount, cancellationToken);
        if (capacityError is not null) return Result<PurchaseCommitmentDto>.Failure(capacityError);
        var commitment = new PurchaseCommitment`);
s=s.replace('        var original = JsonSerializer.Serialize(new { commitment.Amount',`        if (dto.Amount > commitment.Amount)
        {
            var capacityError = await BudgetAuthority.ValidateCommitmentAsync(_context, _projectLookup, commitment.ProjectId!.Value, id, dto.Amount, cancellationToken);
            if (capacityError is not null) return Result<PurchaseCommitmentDto>.Failure(capacityError);
        }
        var original = JsonSerializer.Serialize(new { commitment.Amount`);
for(const m of ['Create','Update'])s=s.replace('await AuditAsync("PurchaseCommitment", commitment.Id, "'+m+'"','await transaction.CommitAsync(cancellationToken);\n        await AuditAsync("PurchaseCommitment", commitment.Id, "'+m+'"');return s;});
edit('NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs',s=>{
s=tx(s,'ApproveAsync');return s.replace('        await LogAuditAsync(request, "Approve"','        await transaction.CommitAsync(cancellationToken);\n        await LogAuditAsync(request, "Approve"');});
for(const p of ['NEXUS.SharedKernel/IProjectFinancialLookup.cs','NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs'])edit(p,s=>s.replace('    public decimal? ApprovedBudget',`    public decimal? CurrentApprovedProjectBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal? UnallocatedBudget { get; init; }
    public decimal? ApprovedBudget`));
edit('NEXUS.Modules.CostManagement/Application/ProjectFinancialLookup.cs',s=>{
s=s.replace('    private readonly DbContext _context;','    private readonly DbContext _context;\n    private readonly IProjectReferenceLookup _projects;');
s=s.replace('ProjectFinancialLookup(DbContext context)','ProjectFinancialLookup(DbContext context, IProjectReferenceLookup projects)');s=s.replace('        _context = context;','        _context = context;\n        _projects = projects;');
s=s.replace('        if (!hasAnyFinancialData) return null;','        var project = await _projects.FindAsync(projectId, cancellationToken);\n        if (project is null) return null;');
s=s.replace(/        var approvedBudget = matchingBudgets.Count > 0[\s\S]*?        \/\/ HasPendingRevision:/,`        var allocatedBudget = matchingBudgets.Sum(b => b.ApprovedAmount);
        var approvedBudget = project.CurrentApprovedProjectBudget;
        // Revision history remains allocation history, never project ceiling approval history.
        var originalApprovedBudget = matchingBudgets.Sum(b => originalAmounts.TryGetValue(b.Id, out var amount) ? amount : b.ApprovedAmount);
        var revisedBudget = allApprovedRevisions.Count > 0 ? allocatedBudget : (decimal?)null;

        // HasPendingRevision:`);
s=s.replace('matchingCostEntries.Count > 0 ? matchingCostEntries.Sum(c => c.Amount) : (decimal?)null','matchingCostEntries.Sum(c => c.Amount)');
s=s.replace(/var committedCost = matchingCommitments.Count > 0[\s\S]*?: \(decimal\?\)null;/,'var committedCost = matchingCommitments.Where(p => p.Status != "Cancelled" && p.Status != "Closed").Sum(p => p.Amount);');
s=s.replace(/        var remainingBudget =[\s\S]*?        var forecastVariance =/,`        var remainingBudget = approvedBudget - actualCost - committedCost;
        var budgetUtilization = approvedBudget.HasValue && approvedBudget.Value != 0m
            ? actualCost / approvedBudget * 100m : (decimal?)null;
        var forecastVariance =`);
s=s.replace('            ApprovedBudget = approvedBudget,','            ApprovedBudget = approvedBudget,\n            CurrentApprovedProjectBudget = approvedBudget,\n            AllocatedBudget = allocatedBudget,\n            UnallocatedBudget = approvedBudget - allocatedBudget,');
s=s.replace('HasBudgetData = budgets.Count > 0','HasBudgetData = approvedBudget.HasValue').replace('HasFinancialData = hasMatchingCurrencyData','HasFinancialData = hasMatchingCurrencyData || approvedBudget.HasValue');return s;});
edit('NEXUS.Modules.ProjectManagement/Application/ProjectOverviewService.cs',s=>s.replace(/        \/\/ Cost Management is the sole authoritative source for budget.[\s\S]*?        var budget = .*?;/,'        // Project Management owns the ceiling; Cost Management supplies allocations and costs.\n        var budget = project.Budget;').replace('            ApprovedBudget = budget,','            ApprovedBudget = budget,\n            CurrentApprovedProjectBudget = budget,\n            AllocatedBudget = financialSummary?.AllocatedBudget ?? 0,\n            UnallocatedBudget = budget - (financialSummary?.AllocatedBudget ?? 0),'));
