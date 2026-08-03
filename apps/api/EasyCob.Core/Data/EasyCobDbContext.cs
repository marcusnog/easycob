using System.Text.RegularExpressions;
using EasyCob.Core.Modules.Audit;
using EasyCob.Core.Modules.Billing;
using EasyCob.Core.Modules.Customers;
using EasyCob.Core.Modules.Finance;
using EasyCob.Core.Modules.Messaging;
using EasyCob.Core.Modules.Tenancy;
using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Core.Data;

public sealed class EasyCobDbContext(DbContextOptions<EasyCobDbContext> options, TenantContext tenant) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<CollectionRule> CollectionRules => Set<CollectionRule>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Contact>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Charge>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Installment>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<MessageTemplate>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<CollectionRule>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Conversation>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<Message>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<DailyBalance>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<AuditEvent>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        modelBuilder.Entity<InboxMessage>().HasQueryFilter(x => x.TenantId == tenant.TenantId);

        modelBuilder.Entity<User>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Customer>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Contact>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Charge>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Installment>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payment>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MessageTemplate>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CollectionRule>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Conversation>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DailyBalance>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AuditEvent>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OutboxMessage>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InboxMessage>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Customer>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Charge>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Conversation>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<MessageTemplate>().HasAlternateKey(x => new { x.TenantId, x.Id });
        modelBuilder.Entity<Contact>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Charge>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Installment>().HasOne<Charge>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChargeId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Payment>().HasOne<Charge>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChargeId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Conversation>().HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConversationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Message>().HasOne<Charge>().WithMany().HasForeignKey(x => new { x.TenantId, x.ChargeId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CollectionRule>().HasOne<MessageTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.MessageTemplateId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>().HasOne<MessageTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.MessageTemplateId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>().HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Document }).IsUnique();
        modelBuilder.Entity<Contact>().HasIndex(x => new { x.TenantId, x.CustomerId });
        modelBuilder.Entity<Charge>().HasIndex(x => new { x.TenantId, x.CustomerId, x.DueDate });
        modelBuilder.Entity<Installment>().HasIndex(x => new { x.TenantId, x.ChargeId, x.Number }).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
        modelBuilder.Entity<MessageTemplate>().HasIndex(x => new { x.TenantId, x.Name, x.Version }).IsUnique();
        modelBuilder.Entity<CollectionRule>().HasIndex(x => new { x.TenantId, x.Active, x.DaysOffset });
        modelBuilder.Entity<Conversation>().HasIndex(x => new { x.TenantId, x.CustomerId });
        modelBuilder.Entity<Message>().HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Message>().HasIndex(x => new { x.TenantId, x.Status, x.ScheduledAt });
        modelBuilder.Entity<DailyBalance>().HasIndex(x => new { x.TenantId, x.Date }).IsUnique();
        modelBuilder.Entity<AuditEvent>().HasIndex(x => new { x.TenantId, x.OccurredAt });
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.PublishedAt, x.OccurredAt });
        modelBuilder.Entity<InboxMessage>().HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
            property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetTableName(Regex.Replace(entity.GetTableName()!, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>().Where(x => x.State == EntityState.Added))
        {
            if (tenant.TenantId == Guid.Empty)
                throw new InvalidOperationException("Tenant não resolvido.");
            entry.Entity.TenantId = tenant.TenantId;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
