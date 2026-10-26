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
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<OrganizationalUnit> OrganizationalUnits => Set<OrganizationalUnit>();
    public DbSet<ResourceOrganizationalUnit> ResourceOrganizationalUnits => Set<ResourceOrganizationalUnit>();
    public DbSet<ResourceReportingLine> ResourceReportingLines => Set<ResourceReportingLine>();
    public DbSet<ResourceCalendar> ResourceCalendars => Set<ResourceCalendar>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ResourceCostProfile> ResourceCostProfiles => Set<ResourceCostProfile>();
    public DbSet<Lookup> Lookups => Set<Lookup>();
}
