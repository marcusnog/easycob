using EasyCob.Core.Tenancy;

namespace EasyCob.Core.Modules.Customers;

public sealed class Customer : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Document { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
}

public sealed class Contact : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool WhatsAppOptIn { get; set; }
    public DateTimeOffset? ConsentAt { get; set; }
    public DateTimeOffset? OptOutAt { get; set; }
}
