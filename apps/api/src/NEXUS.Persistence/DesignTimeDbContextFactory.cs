using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NEXUS.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=NexusPmo;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true");
        return new AppDbContext(optionsBuilder.Options);
    }
}
