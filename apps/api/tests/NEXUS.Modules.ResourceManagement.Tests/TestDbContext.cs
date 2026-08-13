using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();
    public DbSet<ResourceSkill> ResourceSkills => Set<ResourceSkill>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<WorkingCalendar> WorkingCalendars => Set<WorkingCalendar>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ResourceCalendar> ResourceCalendars => Set<ResourceCalendar>();
}
