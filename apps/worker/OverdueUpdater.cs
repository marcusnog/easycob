using EasyCob.Core.Data;
using EasyCob.Core.Modules.Billing;
using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Worker;

public sealed class OverdueUpdater(IServiceScopeFactory scopeFactory, ILogger<OverdueUpdater> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Falha ao atualizar cobranças vencidas"); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public async Task UpdateAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        var tenants = await db.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            using var tenantScope = scopeFactory.CreateScope();
            tenantScope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenant.Id;
            var tenantDb = tenantScope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZone)).DateTime);
            var charges = await tenantDb.Charges
                .Where(x => (x.Status == ChargeStatus.Open || x.Status == ChargeStatus.PartiallyPaid) && x.DueDate < today)
                .ToListAsync(cancellationToken);
            foreach (var charge in charges) charge.Status = ChargeStatus.Overdue;
            if (charges.Count != 0) await tenantDb.SaveChangesAsync(cancellationToken);
        }
    }
}
