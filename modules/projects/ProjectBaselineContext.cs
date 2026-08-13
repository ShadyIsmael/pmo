using Microsoft.EntityFrameworkCore;
using Pmo.Planing.Domain.Entities;

namespace Pmo.Planing.Infrastructure.Contexts;

public class ProjectBaselineContext : DbContext
{
    public ProjectBaselineContext(DbContextOptions<ProjectBaselineContext> options) : base(options)
    {
    }

    public DbSet<ProjectBaselineVersion> ProjectBaselineVersions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProjectBaselineVersionConfiguration());
    }
}