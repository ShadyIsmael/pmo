using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceCalendarConfiguration : IEntityTypeConfiguration<ResourceCalendar>
{
    public void Configure(EntityTypeBuilder<ResourceCalendar> builder)
    {
        builder.ToTable("ResourceCalendars");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DayType).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(500);

        builder.HasOne(c => c.Resource)
            .WithMany()
            .HasForeignKey(c => c.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ResourceId, c.Date }).IsUnique();
        builder.HasIndex(c => c.TenantId);
    }
}
