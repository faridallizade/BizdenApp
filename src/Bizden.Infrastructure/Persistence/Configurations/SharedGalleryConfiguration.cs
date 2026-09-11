using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class SharedGalleryConfiguration : IEntityTypeConfiguration<SharedGallery>
{
    public void Configure(EntityTypeBuilder<SharedGallery> builder)
    {
        builder.ToTable("shared_galleries");
        builder.HasKey(gallery => gallery.Id);
        builder.Property(gallery => gallery.Name).HasMaxLength(120).IsRequired();
        builder.Property(gallery => gallery.PinHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(gallery => gallery.PublicId).IsUnique();
        builder.HasIndex(gallery => new { gallery.EventId, gallery.DeletedAt });
        builder.HasOne(gallery => gallery.Event).WithMany(@event => @event.SharedGalleries).HasForeignKey(gallery => gallery.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SharedGalleryPhotoConfiguration : IEntityTypeConfiguration<SharedGalleryPhoto>
{
    public void Configure(EntityTypeBuilder<SharedGalleryPhoto> builder)
    {
        builder.ToTable("shared_gallery_photos");
        builder.HasKey(item => new { item.SharedGalleryId, item.PhotoId });
        builder.HasIndex(item => item.PhotoId);
        builder.HasOne(item => item.SharedGallery).WithMany(gallery => gallery.Photos).HasForeignKey(item => item.SharedGalleryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Photo).WithMany(photo => photo.SharedGalleries).HasForeignKey(item => item.PhotoId).OnDelete(DeleteBehavior.Restrict);
    }
}
