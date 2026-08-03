using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Audit;
using EasyCob.Core.Modules.Messaging;
using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Http.Features;

namespace EasyCob.Api.Endpoints;

internal static class WhatsAppWebhookEndpoints
{
    public static void MapWhatsAppWebhook(this WebApplication app)
    {
        app.MapGet("/webhooks/whatsapp", (HttpRequest request, IConfiguration configuration) =>
            request.Query["hub.mode"] == "subscribe" &&
            FixedEquals(request.Query["hub.verify_token"].ToString(), configuration["WhatsApp:VerifyToken"])
                ? Results.Text(request.Query["hub.challenge"].ToString())
                : Results.StatusCode(StatusCodes.Status403Forbidden));

        app.MapPost("/webhooks/whatsapp", async (HttpRequest request, EasyCobDbContext db, TenantContext tenant, IConfiguration configuration, CancellationToken ct) =>
        {
            if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
                sizeFeature.MaxRequestBodySize = 1_048_576;
            if (request.ContentLength is > 1_048_576) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            if (!WhatsAppSignature.IsValid(body, request.Headers["X-Hub-Signature-256"].ToString(), configuration["WhatsApp:AppSecret"]))
                return Results.Unauthorized();

            JsonDocument document;
            try { document = JsonDocument.Parse(body); }
            catch (JsonException) { return Results.BadRequest(new { error = "JSON inválido." }); }
            using (document)
            {
                if (!document.RootElement.TryGetProperty("entry", out var entries)) return Results.Ok();
                foreach (var entry in entries.EnumerateArray())
                    if (entry.TryGetProperty("changes", out var changes))
                        foreach (var change in changes.EnumerateArray())
                            if (change.TryGetProperty("value", out var value))
                                await ProcessValue(value, db, tenant, ct);
                return Results.Ok();
            }
        }).DisableAntiforgery().RequireRateLimiting("webhooks");
    }

    private static async Task ProcessValue(JsonElement value, EasyCobDbContext db, TenantContext tenant, CancellationToken ct)
    {
        if (!value.TryGetProperty("metadata", out var metadata) || !metadata.TryGetProperty("phone_number_id", out var phoneElement)) return;
        var tenantId = await db.Tenants.Where(x => x.WhatsAppPhoneNumberId == phoneElement.GetString()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (tenantId is null) return;
        tenant.TenantId = tenantId.Value;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction is not null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('easycob.tenant_id', {tenantId.Value.ToString()}, true)", ct);
        if (value.TryGetProperty("statuses", out var statuses))
            foreach (var status in statuses.EnumerateArray()) await ApplyStatus(status, db, ct);
        if (value.TryGetProperty("messages", out var messages))
            foreach (var message in messages.EnumerateArray()) await ApplyOptOut(message, db, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private static async Task ApplyStatus(JsonElement status, EasyCobDbContext db, CancellationToken ct)
    {
        var externalId = status.GetProperty("id").GetString();
        var name = status.GetProperty("status").GetString();
        if (externalId is null || name is null) return;
        var inboxId = $"whatsapp:{externalId}:{name}";
        if (await db.InboxMessages.AnyAsync(x => x.ExternalId == inboxId, ct)) return;
        db.InboxMessages.Add(new InboxMessage { ExternalId = inboxId });
        var message = await db.Messages.SingleOrDefaultAsync(x => x.ExternalId == externalId, ct);
        if (message is null) return;
        message.Status = name switch
        {
            "sent" => MessageStatus.Sent,
            "delivered" => MessageStatus.Delivered,
            "read" => MessageStatus.Read,
            "failed" => MessageStatus.Failed,
            _ => message.Status
        };
        if (name == "failed" && status.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            message.FailureCode = errors[0].GetProperty("code").ToString();
    }

    private static async Task ApplyOptOut(JsonElement message, EasyCobDbContext db, CancellationToken ct)
    {
        if (!message.TryGetProperty("id", out var idElement) || !message.TryGetProperty("from", out var fromElement) ||
            !message.TryGetProperty("text", out var text) || !text.TryGetProperty("body", out var bodyElement)) return;
        var command = bodyElement.GetString()?.Trim().ToLowerInvariant();
        if (command is not ("sair" or "stop" or "cancelar")) return;
        var inboxId = $"whatsapp:{idElement.GetString()}:opt-out";
        if (await db.InboxMessages.AnyAsync(x => x.ExternalId == inboxId, ct)) return;
        db.InboxMessages.Add(new InboxMessage { ExternalId = inboxId });
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.Phone == fromElement.GetString(), ct);
        if (contact is null) return;
        contact.WhatsAppOptIn = false;
        contact.OptOutAt = DateTimeOffset.UtcNow;
    }

    private static bool FixedEquals(string value, string? expected) =>
        expected is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(expected));
}
