using EasyCob.Core.Data;
using EasyCob.Core.Modules.Billing;
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
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var charges = await db.Charges.IgnoreQueryFilters()
                    .Where(x => (x.Status == ChargeStatus.Open || x.Status == ChargeStatus.PartiallyPaid) && x.DueDate < today)
                    .ToListAsync(stoppingToken);
                foreach (var charge in charges) charge.Status = ChargeStatus.Overdue;
                if (charges.Count != 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Falha ao atualizar cobranças vencidas"); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
