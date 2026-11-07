using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");
        builder.HasKey(meeting => meeting.Id);
        builder.Property(meeting => meeting.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(meeting => meeting.PublicId).IsUnique();
        builder.Property(meeting => meeting.RowVersion).IsRowVersion();

        builder.HasMany(meeting => meeting.AgendaItems)
            .WithOne(item => item.Meeting)
            .HasForeignKey(item => item.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(meeting => meeting.Participants)
            .WithOne(participant => participant.Meeting)
            .HasForeignKey(participant => participant.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(meeting => meeting.Minutes)
            .WithOne(minutes => minutes.Meeting)
            .HasForeignKey(minutes => minutes.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(meeting => meeting.Actions)
            .WithOne(action => action.Meeting)
            .HasForeignKey(action => action.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(meeting => new { meeting.StartTime, meeting.ReminderOffsetMinutes });
    }
}

public sealed class MeetingAgendaConfiguration : IEntityTypeConfiguration<MeetingAgenda>
{
    public void Configure(EntityTypeBuilder<MeetingAgenda> builder)
    {
        builder.ToTable("MeetingAgendas");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.MeetingId);
    }
}

public sealed class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.ToTable("MeetingParticipants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.RowVersion).IsRowVersion();
        builder.HasIndex(participant => participant.MeetingId);

        builder.HasIndex(participant => new { participant.MeetingId, participant.UserId })
            .HasDatabaseName("IX_MeetingParticipants_MeetingId_UserId")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [UserId] IS NOT NULL");
        builder.Property(participant => participant.Name)
            .HasMaxLength(200);
        builder.Property(participant => participant.Email)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(participant => participant.InvitationResponseStatus)
            .HasMaxLength(20);
        builder.HasIndex(participant => new { participant.MeetingId, participant.Email })
            .HasDatabaseName("IX_MeetingParticipants_MeetingId_Email")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Email] IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_MeetingParticipants_UserOrEmail",
            "[UserId] IS NOT NULL OR [Email] IS NOT NULL"));
    }
}

public sealed class MeetingMinutesConfiguration : IEntityTypeConfiguration<MeetingMinutes>
{
    public void Configure(EntityTypeBuilder<MeetingMinutes> builder)
    {
        builder.ToTable("MeetingMinutes");
        builder.HasKey(minutes => minutes.Id);
        builder.Property(minutes => minutes.Summary).HasColumnName("Content");
        builder.Property(minutes => minutes.DiscussionNotes).HasColumnName("DiscussionPoints");
        builder.Property(minutes => minutes.State)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(MeetingMinutesStates.Draft);
        builder.Property(minutes => minutes.RowVersion).IsRowVersion();
        builder.HasIndex(minutes => minutes.MeetingId);
    }
}

public sealed class MeetingActionConfiguration : IEntityTypeConfiguration<MeetingAction>
{
    public void Configure(EntityTypeBuilder<MeetingAction> builder)
    {
        builder.ToTable("MeetingActions");
        builder.HasKey(action => action.Id);
        builder.Property(action => action.RowVersion).IsRowVersion();
        builder.HasIndex(action => action.MeetingId);
        builder.HasIndex(action => action.WorkItemId);
        builder.HasIndex(action => action.AgendaItemId);
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(action => action.WorkItemId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(action => action.AgendaItem)
            .WithMany()
            .HasForeignKey(action => action.AgendaItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MeetingMinutesCommentConfiguration : IEntityTypeConfiguration<MeetingMinutesComment>
{
    public void Configure(EntityTypeBuilder<MeetingMinutesComment> builder)
    {
        builder.ToTable("MeetingMinutesComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(comment => comment.PublicId).IsUnique();
        builder.Property(comment => comment.CommentText).HasMaxLength(4000).IsRequired();
        builder.Property(comment => comment.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(comment => comment.RowVersion).IsRowVersion();
        builder.HasIndex(comment => new { comment.MeetingMinutesId, comment.CreatedAt });
        builder.HasIndex(comment => comment.ParentCommentId);
        builder.HasOne(comment => comment.MeetingMinutes)
            .WithMany(minutes => minutes.Comments)
            .HasForeignKey(comment => comment.MeetingMinutesId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(comment => comment.ParentComment)
            .WithMany()
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MeetingMinutesChecklistItemConfiguration : IEntityTypeConfiguration<MeetingMinutesChecklistItem>
{
    public void Configure(EntityTypeBuilder<MeetingMinutesChecklistItem> builder)
    {
        builder.ToTable("MeetingMinutesChecklistItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(500).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.SortOrder });
        builder.HasOne(item => item.MeetingMinutes)
            .WithMany(minutes => minutes.ChecklistItems)
            .HasForeignKey(item => item.MeetingMinutesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingMinutesAttachmentConfiguration : IEntityTypeConfiguration<MeetingMinutesAttachment>
{
    public void Configure(EntityTypeBuilder<MeetingMinutesAttachment> builder)
    {
        builder.ToTable("MeetingMinutesAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(attachment => attachment.StoragePath).HasMaxLength(1000).IsRequired();
        builder.Property(attachment => attachment.RowVersion).IsRowVersion();
        builder.HasIndex(attachment => attachment.MeetingMinutesId);
        builder.HasOne(attachment => attachment.MeetingMinutes)
            .WithMany(minutes => minutes.Attachments)
            .HasForeignKey(attachment => attachment.MeetingMinutesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingMinutesWorkItemLinkConfiguration : IEntityTypeConfiguration<MeetingMinutesWorkItemLink>
{
    public void Configure(EntityTypeBuilder<MeetingMinutesWorkItemLink> builder)
    {
        builder.ToTable("MeetingMinutesWorkItemLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.RowVersion).IsRowVersion();
        builder.HasIndex(link => new { link.MeetingMinutesId, link.WorkItemId }).IsUnique();
        builder.HasOne(link => link.MeetingMinutes)
            .WithMany(minutes => minutes.WorkItemLinks)
            .HasForeignKey(link => link.MeetingMinutesId)
            .OnDelete(DeleteBehavior.Cascade);
        // WorkItem remains owned by WorkItems; Phase 3 performs cross-module validation.
    }
}

public sealed class MeetingReminderDispatchConfiguration : IEntityTypeConfiguration<MeetingReminderDispatch>
{
    public void Configure(EntityTypeBuilder<MeetingReminderDispatch> builder)
    {
        builder.ToTable("MeetingReminderDispatches");
        builder.HasKey(dispatch => dispatch.Id);
        builder.Property(dispatch => dispatch.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(MeetingReminderDispatchStatuses.Pending);
        builder.Property(dispatch => dispatch.ScheduledAtUtc).IsRequired();
        builder.Property(dispatch => dispatch.RowVersion).IsRowVersion();
        builder.HasOne(dispatch => dispatch.Meeting)
            .WithMany(meeting => meeting.ReminderDispatches)
            .HasForeignKey(dispatch => dispatch.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(dispatch => new { dispatch.MeetingId, dispatch.ScheduledAtUtc })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] = 'Pending'");
    }
}
