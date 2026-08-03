using EasyCob.Core.Tenancy;

namespace EasyCob.Core.Modules.Finance;

public sealed class DailyBalance : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Receivable { get; set; }
    public decimal Overdue { get; set; }
    public decimal Received { get; set; }
}
