using EasyCob.Core.Data;
using EasyCob.Core.Modules.Billing;
using EasyCob.Core.Modules.Tenancy;
using EasyCob.Core.Tenancy;
using EasyCob.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyCob.IntegrationTests;

public sealed class OverdueUpdaterTests
{
    [Fact]
    public async Task Update_DifferentTimeZones_UsesEachTenantsDateAndPreservesOtherStatuses()
    {
        var services = new ServiceCollection();
        var database = Guid.NewGuid().ToString();
        services.AddScoped<TenantContext>();
        services.AddDbContext<EasyCobDbContext>(options => options.UseInMemoryDatabase(database));
        using var provider = services.BuildServiceProvider();
        var dueDate = new DateOnly(2026, 9, 7);
        foreach (var zone in new[] { "America/Sao_Paulo", "UTC" })
        {
            using var scope = provider.CreateScope();
            var tenant = new Tenant { Name = zone, TimeZone = zone };
            scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenant.Id;
            var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
            db.Tenants.Add(tenant);
            foreach (var status in Enum.GetValues<ChargeStatus>())
                db.Charges.Add(new Charge { Description = status.ToString(), Amount = 100m, DueDate = dueDate, Status = status });
            db.Charges.Add(new Charge { Description = "Futura", Amount = 100m, DueDate = dueDate.AddDays(2) });
            await db.SaveChangesAsync();
        }

        using var updater = new OverdueUpdater(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OverdueUpdater>.Instance);
        var now = new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero);
        await updater.UpdateAsync(now);
        await updater.UpdateAsync(now);

        using var verification = provider.CreateScope();
        var query = verification.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        foreach (var tenant in await query.Tenants.ToListAsync())
        {
            var charges = await query.Charges.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id).ToListAsync();
            Assert.Equal(6, charges.Count);
            foreach (var charge in charges)
            {
                var original = charge.Description == "Futura" ? ChargeStatus.Open : Enum.Parse<ChargeStatus>(charge.Description);
                var expected = tenant.TimeZone == "UTC" && charge.DueDate < new DateOnly(2026, 9, 8)
                    && (original == ChargeStatus.Open || original == ChargeStatus.PartiallyPaid) ? ChargeStatus.Overdue : original;
                Assert.Equal(expected, charge.Status);
            }
        }
    }
}
