namespace EasyCob.Core.Modules.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string TimeZone { get; set; } = "America/Sao_Paulo";
    public string Currency { get; set; } = "BRL";
    public string? WhatsAppPhoneNumberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class User : Core.Tenancy.ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string ExternalId { get; set; }
    public required string Email { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool Active { get; set; } = true;
}

public enum UserRole { Owner, Admin, Finance, Collector, Viewer }
