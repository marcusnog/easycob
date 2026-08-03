using EasyCob.Core.Tenancy;

namespace EasyCob.Core.Modules.Billing;

public sealed class Charge : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public ChargeStatus Status { get; set; } = ChargeStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void RecordPayment(decimal alreadyPaid, decimal amount)
    {
        if (Status == ChargeStatus.Cancelled) throw new InvalidOperationException("Cobrança cancelada.");
        if (amount <= 0 || alreadyPaid + amount > Amount) throw new ArgumentOutOfRangeException(nameof(amount));
        Status = alreadyPaid + amount == Amount ? ChargeStatus.Paid : ChargeStatus.PartiallyPaid;
    }

    public void Cancel(bool hasPayments)
    {
        if (hasPayments) throw new InvalidOperationException("Cobrança com pagamento não pode ser cancelada.");
        Status = ChargeStatus.Cancelled;
    }
}

public sealed class Installment : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ChargeId { get; set; }
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
}

public sealed class Payment : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ChargeId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public string? ExternalId { get; set; }
}

public enum ChargeStatus { Open, Overdue, PartiallyPaid, Paid, Cancelled }
