namespace EasyCob.Core.Modules.Billing;

public static class InstallmentSchedule
{
    public static IReadOnlyList<Installment> Create(Guid chargeId, decimal total, int count, DateOnly firstDueDate)
    {
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        if (count is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(count));

        var cents = decimal.ToInt64(decimal.Round(total * 100, 0, MidpointRounding.AwayFromZero));
        var baseCents = cents / count;
        var remainder = cents % count;
        return Enumerable.Range(0, count).Select(index => new Installment
        {
            ChargeId = chargeId,
            Number = index + 1,
            Amount = (baseCents + (index < remainder ? 1 : 0)) / 100m,
            DueDate = firstDueDate.AddMonths(index)
        }).ToArray();
    }
}
