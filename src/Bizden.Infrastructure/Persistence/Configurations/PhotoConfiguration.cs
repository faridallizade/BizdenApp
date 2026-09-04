using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("photos", table => table.HasCheckConstraint("ck_photos_positive_file_size", "\"FileSize\" > 0"));
        builder.HasKey(photo => photo.Id);
        builder.Property(photo => photo.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(photo => photo.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(photo => photo.MimeType).HasMaxLength(127).IsRequired();
        builder.Property(photo => photo.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(photo => new { photo.EventId, photo.CreatedAt });
        builder.HasIndex(photo => new { photo.InvitationId, photo.CreatedAt });
        builder.HasIndex(photo => photo.StorageKey).IsUnique();
        builder.HasOne(photo => photo.Event)
            .WithMany(@event => @event.Photos)
            .HasForeignKey(photo => photo.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(photo => photo.Invitation)
            .WithMany(invitation => invitation.Photos)
            .HasForeignKey(photo => photo.InvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
