using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyCob.Core.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EasyCobDbContext>
{
    public EasyCobDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EASYCOB_POSTGRES")
            ?? "Host=localhost;Port=5432;Database=easycob;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<EasyCobDbContext>().UseNpgsql(connection).Options;
        return new EasyCobDbContext(options, new TenantContext());
    }
}
