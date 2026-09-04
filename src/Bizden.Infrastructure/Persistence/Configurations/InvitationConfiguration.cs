using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations", table =>
        {
            table.HasCheckConstraint("ck_invitations_positive_limit", "\"UploadLimit\" > 0");
            table.HasCheckConstraint("ck_invitations_non_negative_counters", "\"ReservedUploads\" >= 0 AND \"CompletedUploads\" >= 0");
            table.HasCheckConstraint("ck_invitations_limit_not_exceeded", "\"ReservedUploads\" + \"CompletedUploads\" <= \"UploadLimit\"");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.Label).HasMaxLength(120);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => invitation.EventId);
        builder.HasOne(invitation => invitation.Event)
            .WithMany(@event => @event.Invitations)
            .HasForeignKey(invitation => invitation.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
