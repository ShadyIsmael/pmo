using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class TestBase : IDisposable
{
    private readonly List<TestDbContext> _contexts = new();

    protected static readonly Guid DepartmentEngineeringId = Guid.Parse("A1000000-0000-0000-0000-000000000001");
    protected static readonly Guid DepartmentDesignId = Guid.Parse("A2000000-0000-0000-0000-000000000002");
    protected static readonly Guid DefaultCalendarId = Guid.Parse("B1000000-0000-0000-0000-000000000001");
    protected static readonly Guid NonDefaultCalendarId = Guid.Parse("B2000000-0000-0000-0000-000000000002");
    protected static readonly Guid HolidayNewYearId = Guid.Parse("C1000000-0000-0000-0000-000000000001");
    protected static readonly Guid SkillCSharpId = Guid.Parse("D1000000-0000-0000-0000-000000000001");
    protected static readonly Guid SkillJavaScriptId = Guid.Parse("D2000000-0000-0000-0000-000000000002");
    protected static readonly Guid SkillUiDesignId = Guid.Parse("D3000000-0000-0000-0000-000000000003");
    protected static readonly Guid ResourceAliceId = Guid.Parse("E1000000-0000-0000-0000-000000000001");
    protected static readonly Guid ResourceBobId = Guid.Parse("E2000000-0000-0000-0000-000000000002");
    protected static readonly Guid ResourceCharlieId = Guid.Parse("E3000000-0000-0000-0000-000000000003");
    protected static readonly Guid ResourceDianaId = Guid.Parse("E4000000-0000-0000-0000-000000000004");
    protected static readonly Guid ExistingAllocationId = Guid.Parse("F1000000-0000-0000-0000-000000000001");

    protected TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestDbContext(options);
        SeedDefaults(context);
        context.SaveChanges();
        _contexts.Add(context);
        return context;
    }

    protected virtual void SeedDefaults(TestDbContext context)
    {
        SeedDepartments(context);
        SeedCalendars(context);
        SeedSkills(context);
        SeedResources(context);
        SeedResourceSkills(context);
        SeedAllocations(context);
        SeedHolidays(context);
    }

    private void SeedDepartments(TestDbContext context)
    {
        context.Departments.Add(new Department
        {
            Id = DepartmentEngineeringId,
            Name = "Engineering",
            IsActive = true
        });
        context.Departments.Add(new Department
        {
            Id = DepartmentDesignId,
            Name = "Design",
            IsActive = true
        });
    }

    private void SeedCalendars(TestDbContext context)
    {
        context.WorkingCalendars.Add(new WorkingCalendar
        {
            Id = DefaultCalendarId,
            Name = "Standard",
            WorkingDaysPerWeek = 5,
            HoursPerDay = 8,
            IsDefault = true
        });
        context.WorkingCalendars.Add(new WorkingCalendar
        {
            Id = NonDefaultCalendarId,
            Name = "Part Time",
            WorkingDaysPerWeek = 4,
            HoursPerDay = 6,
            IsDefault = false
        });
    }

    private void SeedSkills(TestDbContext context)
    {
        context.Skills.Add(new Skill { Id = SkillCSharpId, Name = "C#", Category = "Programming", IsActive = true });
        context.Skills.Add(new Skill { Id = SkillJavaScriptId, Name = "JavaScript", Category = "Programming", IsActive = true });
        context.Skills.Add(new Skill { Id = SkillUiDesignId, Name = "UI Design", Category = "Design", IsActive = true });
    }

    private void SeedResources(TestDbContext context)
    {
        context.Resources.Add(new Resource
        {
            Id = ResourceAliceId,
            Name = "Alice Johnson",
            JobTitle = "Developer",
            Type = "Employee",
            Seniority = "Senior",
            DepartmentId = DepartmentEngineeringId,
            IsActive = true,
            Email = "alice@example.com",
            CurrentUtilization = 50
        });
        context.Resources.Add(new Resource
        {
            Id = ResourceBobId,
            Name = "Bob Smith",
            JobTitle = "Designer",
            Type = "Employee",
            Seniority = "Mid",
            DepartmentId = DepartmentDesignId,
            IsActive = true,
            Email = "bob@example.com",
            CurrentUtilization = 30
        });
        context.Resources.Add(new Resource
        {
            Id = ResourceCharlieId,
            Name = "Charlie Brown",
            JobTitle = "Developer",
            Type = "Contractor",
            Seniority = "Junior",
            DepartmentId = DepartmentEngineeringId,
            IsActive = true,
            Email = "charlie@example.com",
            CurrentUtilization = 10
        });
        context.Resources.Add(new Resource
        {
            Id = ResourceDianaId,
            Name = "Diana Prince",
            JobTitle = "Developer",
            Type = "Employee",
            Seniority = "Senior",
            DepartmentId = DepartmentEngineeringId,
            IsActive = false,
            Email = "diana@example.com"
        });
    }

    private void SeedResourceSkills(TestDbContext context)
    {
        context.ResourceSkills.Add(new ResourceSkill
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            SkillId = SkillCSharpId,
            Proficiency = "Advanced",
            YearsOfExperience = 5
        });
        context.ResourceSkills.Add(new ResourceSkill
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            SkillId = SkillJavaScriptId,
            Proficiency = "Intermediate",
            YearsOfExperience = 3
        });
        context.ResourceSkills.Add(new ResourceSkill
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceBobId,
            SkillId = SkillUiDesignId,
            Proficiency = "Advanced",
            YearsOfExperience = 4
        });
        context.ResourceSkills.Add(new ResourceSkill
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            SkillId = SkillJavaScriptId,
            Proficiency = "Beginner",
            YearsOfExperience = 1
        });
    }

    private void SeedAllocations(TestDbContext context)
    {
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = ExistingAllocationId,
            ResourceId = ResourceAliceId,
            ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000001"),
            ProjectName = "Project Alpha",
            Role = "Developer",
            AllocationPercentage = 50,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 19),
            Status = ResourceAllocation.StatusConfirmed,
            IsBillable = true
        });
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.Parse("F2000000-0000-0000-0000-000000000002"),
            ResourceId = ResourceBobId,
            ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000001"),
            ProjectName = "Project Alpha",
            Role = "Designer",
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 19),
            Status = ResourceAllocation.StatusProposed,
            IsBillable = true
        });
    }

    private void SeedHolidays(TestDbContext context)
    {
        context.Holidays.Add(new Holiday
        {
            Id = HolidayNewYearId,
            WorkingCalendarId = DefaultCalendarId,
            Name = "New Year",
            Date = new DateTime(2026, 1, 1),
            IsRecurring = true
        });
    }

    public void Dispose()
    {
        foreach (var ctx in _contexts)
            ctx.Dispose();
        _contexts.Clear();
    }
}
