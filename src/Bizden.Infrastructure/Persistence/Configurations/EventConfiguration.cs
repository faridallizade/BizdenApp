using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events", table => table.HasCheckConstraint("ck_events_upload_window", "\"UploadStartAt\" <= \"UploadEndAt\""));
        builder.HasKey(@event => @event.Id);
        builder.Property(@event => @event.Name).HasMaxLength(160).IsRequired();
        builder.Property(@event => @event.Slug).HasMaxLength(180).IsRequired();
        builder.Property(@event => @event.Description).HasMaxLength(2_000);
        builder.Property(@event => @event.CoverImageKey).HasMaxLength(512);
        builder.Property(@event => @event.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(@event => @event.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(@event => @event.PublicId).IsUnique();
        builder.HasIndex(@event => @event.Slug).IsUnique();
        builder.HasIndex(@event => @event.OwnerId);
    }
}
