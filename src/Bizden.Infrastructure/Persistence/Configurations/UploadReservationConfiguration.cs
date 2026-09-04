using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class UploadReservationConfiguration : IEntityTypeConfiguration<UploadReservation>
{
    public void Configure(EntityTypeBuilder<UploadReservation> builder)
    {
        builder.ToTable("upload_reservations", table => table.HasCheckConstraint("ck_upload_reservations_expiry", "\"ExpiresAt\" > \"CreatedAt\""));
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(reservation => reservation.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(reservation => reservation.ExpiresAt);
        builder.HasIndex(reservation => new { reservation.InvitationId, reservation.Status });
        builder.HasIndex(reservation => new { reservation.InvitationId, reservation.IdempotencyKey }).IsUnique();
        builder.HasIndex(reservation => reservation.PhotoId).IsUnique();
        builder.HasOne(reservation => reservation.Invitation)
            .WithMany(invitation => invitation.UploadReservations)
            .HasForeignKey(reservation => reservation.InvitationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.Photo)
            .WithOne(photo => photo.UploadReservation)
            .HasForeignKey<UploadReservation>(reservation => reservation.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
