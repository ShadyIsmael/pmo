using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(ResourceFieldLengths.Name).IsRequired();
        builder.Property(r => r.EmployeeId).HasMaxLength(ResourceFieldLengths.EmployeeId);
        builder.Property(r => r.JobTitle).HasMaxLength(ResourceFieldLengths.JobTitle);
        builder.Property(r => r.Email).HasMaxLength(ResourceFieldLengths.Email);
        builder.Property(r => r.Phone).HasMaxLength(ResourceFieldLengths.Phone);
        builder.Property(r => r.AvatarUrl).HasMaxLength(ResourceFieldLengths.AvatarUrl);
        builder.Property(r => r.Type).HasMaxLength(ResourceFieldLengths.Type).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(ResourceFieldLengths.Status).IsRequired();
        builder.Property(r => r.EmploymentType).HasMaxLength(ResourceFieldLengths.EmploymentType);
        builder.Property(r => r.Seniority).HasMaxLength(ResourceFieldLengths.Seniority);
        builder.Property(r => r.Timezone).HasMaxLength(ResourceFieldLengths.Timezone);
        builder.Property(r => r.Notes).HasMaxLength(ResourceFieldLengths.Notes);

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Type);
        builder.HasIndex(r => r.Email);
        builder.HasIndex(r => r.EmployeeId);
        builder.HasIndex(r => r.VendorId);
        builder.HasOne(r => r.Vendor)
            .WithMany()
            .HasForeignKey(r => r.VendorId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(r => r.BaseCalendarId);
        builder.HasOne(r => r.BaseCalendar)
            .WithMany()
            .HasForeignKey(r => r.BaseCalendarId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
