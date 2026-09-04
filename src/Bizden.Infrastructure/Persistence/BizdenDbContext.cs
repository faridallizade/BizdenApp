using Bizden.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Persistence;

public sealed class BizdenDbContext(DbContextOptions<BizdenDbContext> options) : DbContext(options)
{
    public DbSet<HostUser> HostUsers => Set<HostUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<UploadReservation> UploadReservations => Set<UploadReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BizdenDbContext).Assembly);
    }
}
