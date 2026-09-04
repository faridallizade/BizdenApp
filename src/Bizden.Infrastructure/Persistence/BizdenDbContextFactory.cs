using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bizden.Infrastructure.Persistence;

public sealed class BizdenDbContextFactory : IDesignTimeDbContextFactory<BizdenDbContext>
{
    public BizdenDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=bizden_design_time;Username=postgres";

        var options = new DbContextOptionsBuilder<BizdenDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BizdenDbContext(options);
    }
}
