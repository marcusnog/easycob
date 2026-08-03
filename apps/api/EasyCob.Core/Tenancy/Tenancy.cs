namespace EasyCob.Core.Tenancy;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public sealed class TenantContext
{
    public Guid TenantId { get; set; }
}
