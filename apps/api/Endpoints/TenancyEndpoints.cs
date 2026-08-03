using EasyCob.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Api.Endpoints;

internal static class TenancyEndpoints
{
    public static void MapTenancy(this WebApplication app)
    {
        var group = app.MapGroup("/tenant").RequireAuthorization();
        group.MapGet("/me", async (HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var subject = http.User.FindFirst("sub")?.Value;
            return await db.Users.Where(x => x.ExternalId == subject).Select(x => new { x.Id, x.Email, x.Role }).SingleOrDefaultAsync(ct) is { } user
                ? Results.Ok(user) : Results.NotFound();
        });
        group.MapGet("/users", async (EasyCobDbContext db, CancellationToken ct) =>
            await db.Users.OrderBy(x => x.Email).Select(x => new { x.Id, x.Email, x.Role, x.Active }).ToListAsync(ct))
            .RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
        group.MapGet("/", async (EasyCob.Core.Tenancy.TenantContext tenant, EasyCobDbContext db, CancellationToken ct) =>
            await db.Tenants.Where(x => x.Id == tenant.TenantId)
                .Select(x => new { x.Id, x.Name, x.TimeZone, x.Currency, x.WhatsAppPhoneNumberId }).SingleOrDefaultAsync(ct) is { } result
                ? Results.Ok(result) : Results.NotFound());
        group.MapPut("/settings", async (TenantSettingsRequest request, EasyCob.Core.Tenancy.TenantContext tenant, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TimeZone) || string.IsNullOrWhiteSpace(request.Currency))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["settings"] = ["Nome, timezone e moeda são obrigatórios."] });
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out _))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["timeZone"] = ["Timezone inválido."] });
            if (request.Currency.Trim().Length != 3)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["currency"] = ["Moeda deve usar código ISO de três letras."] });
            var current = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenant.TenantId, ct);
            if (current is null) return Results.NotFound();
            current.Name = request.Name.Trim();
            current.TimeZone = request.TimeZone;
            current.Currency = request.Currency.Trim().ToUpperInvariant();
            current.WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumberId) ? null : request.WhatsAppPhoneNumberId.Trim();
            db.Audit(http.User, "tenant.settings-updated", "Tenant", current.Id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
    }
}

internal sealed record TenantSettingsRequest(string Name, string TimeZone, string Currency, string? WhatsAppPhoneNumberId);
