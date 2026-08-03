using System.Net.Http.Json;
using System.Text.Json;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Worker;

public sealed class WhatsAppDispatcher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<WhatsAppDispatcher> logger,
    string graphVersion) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await ClaimNext(stoppingToken);
                if (message is null) { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); continue; }
                await Send(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                // An exception after the POST is delivery-ambiguous. Keep Sending to prevent automatic duplicates.
                logger.LogError(exception, "Falha ambígua no dispatcher do WhatsApp; reconciliação manual necessária");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task<Message?> ClaimNext(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var message = await db.Messages
            .FromSqlRaw("SELECT * FROM messages WHERE status = 0 AND scheduled_at <= now() ORDER BY scheduled_at FOR UPDATE SKIP LOCKED LIMIT 1")
            .IgnoreQueryFilters().SingleOrDefaultAsync(ct);
        if (message is not null)
        {
            message.Status = MessageStatus.Sending;
            message.Attempts++;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return message;
    }

    private async Task Send(Message claimed, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        var message = await db.Messages.IgnoreQueryFilters().SingleAsync(x => x.Id == claimed.Id, ct);
        var template = await db.MessageTemplates.IgnoreQueryFilters().SingleAsync(x => x.Id == message.MessageTemplateId, ct);
        var phoneNumberId = await db.Tenants.Where(x => x.Id == message.TenantId).Select(x => x.WhatsAppPhoneNumberId).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            message.Status = MessageStatus.Failed;
            message.FailureCode = "phone-number-id-not-configured";
            await db.SaveChangesAsync(ct);
            return;
        }

        var response = await httpClientFactory.CreateClient("whatsapp").PostAsJsonAsync(
            $"{graphVersion}/{phoneNumberId}/messages",
            new
            {
                messaging_product = "whatsapp",
                to = message.Recipient,
                type = "template",
                template = new { name = template.MetaTemplateId, language = new { code = template.Language } }
            }, ct);
        if (response.IsSuccessStatusCode)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            message.ExternalId = json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();
            message.Status = MessageStatus.Sent;
            message.SentAt = DateTimeOffset.UtcNow;
            message.FailureCode = null;
        }
        else
        {
            message.FailureCode = $"meta-{(int)response.StatusCode}";
            message.Status = message.Attempts >= 5 ? MessageStatus.Failed : MessageStatus.Pending;
            message.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, message.Attempts));
        }
        await db.SaveChangesAsync(ct);
    }
}
