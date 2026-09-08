using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ActorType).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Action).HasMaxLength(64).IsRequired();
        builder.Property(item => item.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Metadata).HasMaxLength(1_000);
        builder.HasIndex(item => new { item.EntityType, item.EntityId, item.CreatedAt });
        builder.HasIndex(item => new { item.ActorId, item.CreatedAt });
    }
}
