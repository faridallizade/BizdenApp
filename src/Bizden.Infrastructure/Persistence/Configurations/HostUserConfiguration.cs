using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bizden.Infrastructure.Persistence.Configurations;

public sealed class HostUserConfiguration : IEntityTypeConfiguration<HostUser>
{
    public void Configure(EntityTypeBuilder<HostUser> builder)
    {
        builder.ToTable("host_users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Name).HasMaxLength(120).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512);
        builder.Property(user => user.EmailVerificationCodeHash).HasMaxLength(512);
        builder.Property(user => user.PasswordResetCodeHash).HasMaxLength(512);
        builder.Property(user => user.PendingEmail).HasMaxLength(256);
        builder.Property(user => user.PendingNormalizedEmail).HasMaxLength(256);
        builder.Property(user => user.EmailChangeCodeHash).HasMaxLength(512);
        builder.Property(user => user.BlockedReason).HasMaxLength(500);
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
