using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using System.Security.Cryptography;
using EasyCob.Core.Modules.Audit;
using EasyCob.Core.Tenancy;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyCob.IntegrationTests;

public sealed class BackendApiTests : IClassFixture<EasyCobApiFactory>
{
    private readonly EasyCobApiFactory factory;

    public BackendApiTests(EasyCobApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task CustomerAndCharge_ValidRequests_PersistCompleteFlow()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.FirstTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Role", "Finance");

        var customerResponse = await client.PostAsJsonAsync("/customers", new { name = "Maria", document = "123" });
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var chargeResponse = await client.PostAsJsonAsync("/charges", new
        {
            customerId,
            description = "Mensalidade",
            amount = 100m,
            firstDueDate = "2026-08-10",
            installments = 3
        });
        Assert.Equal(HttpStatusCode.Created, chargeResponse.StatusCode);
        var chargeId = (await chargeResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var detail = await client.GetFromJsonAsync<ChargeResponse>($"/charges/{chargeId}");
        Assert.Equal(3, detail!.Installments.Length);
        Assert.Equal(100m, detail.Installments.Sum(x => x.Amount));
    }

    [Fact]
    public async Task Customers_DifferentTenant_DoesNotLeak()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.SecondTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Role", "Viewer");

        var customers = await client.GetFromJsonAsync<CustomerResponse[]>("/customers");

        Assert.Empty(customers!);
    }

    [Fact]
    public async Task CustomerCreate_Viewer_IsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.FirstTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Role", "Viewer");

        var response = await client.PostAsJsonAsync("/customers", new { name = "Bloqueado" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PrivateEndpoint_AnonymousUser_IsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Payment_RepeatedExternalId_IsRejectedAndChargeIsPaidOnce()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.FirstTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Role", "Finance");
        var document = Guid.NewGuid().ToString("N");
        var customer = await client.PostAsJsonAsync("/customers", new { name = "Pagamento", document });
        var customerId = (await customer.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var charge = await client.PostAsJsonAsync("/charges", new
        {
            customerId,
            description = "Quitação",
            amount = 25m,
            firstDueDate = "2026-08-10",
            installments = 1
        });
        var chargeId = (await charge.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var payment = new { amount = 25m, paidAt = DateTimeOffset.UtcNow, externalId = Guid.NewGuid().ToString("N") };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/charges/{chargeId}/payments", payment)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/charges/{chargeId}/payments", payment)).StatusCode);
        var detail = await client.GetFromJsonAsync<ChargeStatusResponse>($"/charges/{chargeId}");
        Assert.Equal(3, detail!.Status);
        Assert.Single(detail.Payments);
    }

    [Fact]
    public async Task WhatsAppWebhook_InvalidSignature_IsUnauthorized()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", "sha256=invalid");

        var response = await factory.CreateClient().PostAsync("/webhooks/whatsapp", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhatsAppWebhook_RepeatedSignedEvent_IsProcessedOnce()
    {
        var client = factory.CreateClient();
        const string body = "{\"entry\":[{\"changes\":[{\"value\":{\"metadata\":{\"phone_number_id\":\"phone-1\"},\"statuses\":[{\"id\":\"wamid-1\",\"status\":\"delivered\"}]}}]}]}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("CHANGE_ME"), Encoding.UTF8.GetBytes(body)));
        using var first = new StringContent(body, Encoding.UTF8, "application/json");
        first.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/webhooks/whatsapp", first)).StatusCode);
        using var second = new StringContent(body, Encoding.UTF8, "application/json");
        second.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/webhooks/whatsapp", second)).StatusCode);

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = EasyCobApiFactory.FirstTenant;
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        Assert.Equal(1, await db.InboxMessages.CountAsync(x => x.ExternalId == "whatsapp:wamid-1:delivered"));
    }

    [Fact]
    public async Task TenantSettings_OwnerUpdate_PersistsAndViewerIsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.SecondTenant.ToString());
        client.DefaultRequestHeaders.Add("X-Role", "Owner");
        var payload = new { name = "Empresa Atualizada", timeZone = "UTC", currency = "USD", whatsAppPhoneNumberId = "phone-2" };

        var response = await client.PutAsJsonAsync("/tenant/settings", payload);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var tenant = await client.GetFromJsonAsync<TenantResponse>("/tenant");
        Assert.Equal("Empresa Atualizada", tenant!.Name);
        Assert.Equal("UTC", tenant.TimeZone);
        Assert.Equal("USD", tenant.Currency);
        Assert.Equal("phone-2", tenant.WhatsAppPhoneNumberId);

        var viewer = factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Tenant-Id", EasyCobApiFactory.FirstTenant.ToString());
        viewer.DefaultRequestHeaders.Add("X-Role", "Viewer");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync("/tenant/settings", payload)).StatusCode);
    }

    [Fact]
    public async Task OpenApi_AnonymousRequest_ReturnsContract()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record IdResponse(Guid Id);
    private sealed record CustomerResponse(Guid Id, string Name);
    private sealed record ChargeResponse(InstallmentResponse[] Installments);
    private sealed record ChargeStatusResponse(int Status, PaymentResponse[] Payments);
    private sealed record PaymentResponse(decimal Amount);
    private sealed record InstallmentResponse(decimal Amount);
    private sealed record TenantResponse(Guid Id, string Name, string TimeZone, string Currency, string? WhatsAppPhoneNumberId);
}

public sealed class EasyCobApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid FirstTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SecondTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EasyCobDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EasyCobDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<EasyCobDbContext>();
            services.AddDbContext<EasyCobDbContext>(options => options.UseInMemoryDatabase("api-tests"));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        if (!db.Tenants.Any())
        {
            db.Tenants.AddRange(
                new Tenant { Id = FirstTenant, Name = "Tenant 1", WhatsAppPhoneNumberId = "phone-1" },
                new Tenant { Id = SecondTenant, Name = "Tenant 2" });
            db.SaveChanges();
        }
        return host;
    }
}

public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenant = Request.Headers["X-Tenant-Id"].ToString();
        var role = Request.Headers["X-Role"].ToString();
        if (string.IsNullOrWhiteSpace(tenant)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([
            new Claim("sub", $"test-{tenant}-{role}"),
            new Claim("email", "test@example.com"),
            new Claim("tenant_id", tenant),
            new Claim("cognito:groups", role)
        ], Scheme.Name, "name", "cognito:groups");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
