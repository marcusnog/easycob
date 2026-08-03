using EasyCob.Core.Data;
using EasyCob.Core.Modules.Customers;
using EasyCob.Core.Modules.Billing;
using EasyCob.Core.Modules.Messaging;
using System.Security.Cryptography;
using System.Text;
using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.IntegrationTests;

public sealed class TenantIsolationTests
{
    [Fact]
    public void Model_TenantEntities_HaveFilterAndTenantLeadingIndex()
    {
        var options = new DbContextOptionsBuilder<EasyCobDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new EasyCobDbContext(options, new TenantContext { TenantId = Guid.NewGuid() });

        var entities = db.Model.GetEntityTypes()
            .Where(x => typeof(ITenantEntity).IsAssignableFrom(x.ClrType));

        foreach (var entity in entities)
        {
            Assert.NotEmpty(entity.GetDeclaredQueryFilters());
            Assert.Contains(entity.GetIndexes(), index => index.Properties[0].Name == nameof(ITenantEntity.TenantId));
        }
    }

    [Fact]
    public async Task Customers_DifferentTenant_ReturnsOnlyCurrentTenant()
    {
        var options = new DbContextOptionsBuilder<EasyCobDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();

        await using (var first = new EasyCobDbContext(options, new TenantContext { TenantId = firstTenant }))
        {
            first.Customers.Add(new Customer { Name = "Cliente A" });
            await first.SaveChangesAsync();
        }
        await using (var second = new EasyCobDbContext(options, new TenantContext { TenantId = secondTenant }))
        {
            second.Customers.Add(new Customer { Name = "Cliente B" });
            await second.SaveChangesAsync();
        }
        await using var query = new EasyCobDbContext(options, new TenantContext { TenantId = firstTenant });

        var customers = await query.Customers.ToListAsync();

        var customer = Assert.Single(customers);
        Assert.Equal("Cliente A", customer.Name);
        Assert.Equal(firstTenant, customer.TenantId);
    }

    [Fact]
    public async Task SaveChanges_MissingTenant_Throws()
    {
        var options = new DbContextOptionsBuilder<EasyCobDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new EasyCobDbContext(options, new TenantContext());
        db.Customers.Add(new Customer { Name = "Inválido" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void InstallmentSchedule_RoundingRemainder_PreservesTotal()
    {
        var installments = InstallmentSchedule.Create(Guid.NewGuid(), 100m, 3, new DateOnly(2026, 8, 10));

        Assert.Equal([33.34m, 33.33m, 33.33m], installments.Select(x => x.Amount));
        Assert.Equal(100m, installments.Sum(x => x.Amount));
        Assert.Equal(new DateOnly(2026, 10, 10), installments[2].DueDate);
    }

    [Fact]
    public void InstallmentSchedule_EndOfMonth_UsesValidCalendarDates()
    {
        var installments = InstallmentSchedule.Create(Guid.NewGuid(), 30m, 3, new DateOnly(2027, 1, 31));

        Assert.Equal([new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31)], installments.Select(x => x.DueDate));
    }

    [Fact]
    public void WhatsAppSignature_TamperedBody_IsRejected()
    {
        const string body = "{\"event\":\"ok\"}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes(body)));

        Assert.True(WhatsAppSignature.IsValid(body, $"sha256={hash}", "secret"));
        Assert.False(WhatsAppSignature.IsValid(body + "x", $"sha256={hash}", "secret"));
    }

    [Fact]
    public void Charge_PartialThenRemainingPayment_TransitionsToPaid()
    {
        var charge = new Charge { CustomerId = Guid.NewGuid(), Description = "Teste", Amount = 100m };

        charge.RecordPayment(0, 30m);
        Assert.Equal(ChargeStatus.PartiallyPaid, charge.Status);
        charge.RecordPayment(30m, 70m);
        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => charge.RecordPayment(100m, 1m));
    }

    [Fact]
    public void Charge_WithPayment_CannotBeCancelled()
    {
        var charge = new Charge { CustomerId = Guid.NewGuid(), Description = "Teste", Amount = 100m };

        Assert.Throws<InvalidOperationException>(() => charge.Cancel(true));
    }
}
