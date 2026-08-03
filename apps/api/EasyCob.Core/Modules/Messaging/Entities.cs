using EasyCob.Core.Tenancy;

namespace EasyCob.Core.Modules.Messaging;

public sealed class MessageTemplate : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string MetaTemplateId { get; set; }
    public required string Language { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class CollectionRule : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MessageTemplateId { get; set; }
    public required string Name { get; set; }
    public int DaysOffset { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class Conversation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
}

public sealed class Message : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? ChargeId { get; set; }
    public Guid MessageTemplateId { get; set; }
    public required string Recipient { get; set; }
    public string? ExternalId { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? FailureCode { get; set; }
}

public enum MessageStatus { Pending, Sent, Delivered, Read, Failed, Sending }
