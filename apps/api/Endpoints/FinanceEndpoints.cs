using EasyCob.Core.Data;
using EasyCob.Core.Modules.Billing;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Api.Endpoints;

internal static class FinanceEndpoints
{
    public static void MapFinance(this WebApplication app)
    {
        var group = app.MapGroup("/finance").RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance", "Viewer"));
        group.MapGet("/summary", async (EasyCobDbContext db, CancellationToken ct) =>
        {
            var active = db.Charges.Where(x => x.Status != ChargeStatus.Cancelled && x.Status != ChargeStatus.Paid);
            var overdueCharges = db.Charges.Where(x => x.Status == ChargeStatus.Overdue);
            var receivable = (await active.SumAsync(x => (decimal?)x.Amount, ct) ?? 0)
                - (await db.Payments.Where(x => active.Any(c => c.Id == x.ChargeId)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
            var overdue = (await overdueCharges.SumAsync(x => (decimal?)x.Amount, ct) ?? 0)
                - (await db.Payments.Where(x => overdueCharges.Any(c => c.Id == x.ChargeId)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0);
            var received = await db.Payments.SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            return Results.Ok(new { receivable, overdue, received });
        });
        group.MapGet("/daily", async (DateOnly? from, DateOnly? to, EasyCobDbContext db, CancellationToken ct) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (start > end || end.DayNumber - start.DayNumber > 366)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["Período inválido ou maior que 366 dias."] });
            return Results.Ok(await db.DailyBalances.Where(x => x.Date >= start && x.Date <= end).OrderBy(x => x.Date)
                .Select(x => new { x.Date, x.Receivable, x.Overdue, x.Received }).ToListAsync(ct));
        });
    }
}
