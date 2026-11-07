using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(ResourceFieldLengths.Name).IsRequired();
        // Preserve the retired single-Team column while historical values are
        // copied into effective-dated TeamMember rows by the additive migration.
        builder.Property<Guid?>("TeamId");
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
        builder.Property(r => r.HoursPerDay).HasPrecision(10, 2);
        builder.Property(r => r.HoursPerWeek).HasPrecision(10, 2);
        builder.ToTable("Resources", table =>
        {
            table.HasCheckConstraint("CK_Resources_HoursPerDay_Range", "[HoursPerDay] IS NULL OR ([HoursPerDay] > 0 AND [HoursPerDay] <= 24)");
            table.HasCheckConstraint("CK_Resources_HoursPerWeek_Range", "[HoursPerWeek] IS NULL OR ([HoursPerWeek] > 0 AND [HoursPerWeek] <= 168)");
        });

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Type);
        builder.HasIndex(r => r.Email);
        builder.HasIndex(r => r.EmployeeId);
        builder.HasIndex(r => r.VendorId);
        builder.HasIndex(r => new { r.TenantId, r.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(resource => new { resource.TenantId, resource.UserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);
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
