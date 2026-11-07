using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NEXUS.Persistence;

namespace NEXUS.Persistence.DesignTime;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ModelOnlyConnectionString =
        "Server=127.0.0.1,1;Database=EFDesignTimeOnly;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=1;Application Name=FOPX-EF-DesignTime";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ModelOnlyConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
