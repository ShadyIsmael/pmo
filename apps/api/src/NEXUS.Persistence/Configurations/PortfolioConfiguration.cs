using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;
namespace NEXUS.Persistence.Configurations;
public sealed class PortfolioConfiguration:IEntityTypeConfiguration<Portfolio>{public void Configure(EntityTypeBuilder<Portfolio>b){b.ToTable("Portfolios");b.HasKey(x=>x.Id);b.Property(x=>x.Name).HasMaxLength(300).IsRequired();b.Property(x=>x.Code).HasMaxLength(50).IsRequired();b.Property(x=>x.Status).HasMaxLength(50);b.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique().HasFilter("[IsDeleted] = 0");b.HasIndex(x=>new{x.TenantId,x.Status});b.Property(x=>x.RowVersion).IsRowVersion();}}
