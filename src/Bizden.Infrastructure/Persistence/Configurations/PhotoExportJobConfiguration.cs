using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class PhotoExportJobConfiguration : IEntityTypeConfiguration<PhotoExportJob>
{
    public void Configure(EntityTypeBuilder<PhotoExportJob> builder)
    {
        builder.ToTable("photo_export_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(job => job.StorageKey).HasMaxLength(512);
        builder.Property(job => job.FileName).HasMaxLength(255);
        builder.Property(job => job.Error).HasMaxLength(512);
        builder.HasIndex(job => new { job.EventId, job.Status }).HasFilter("\"Status\" IN ('Queued', 'Processing')").IsUnique();
        builder.HasIndex(job => new { job.Status, job.CreatedAt });
        builder.HasOne(job => job.Event).WithMany().HasForeignKey(job => job.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
