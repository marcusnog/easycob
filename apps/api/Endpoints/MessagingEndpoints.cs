using EasyCob.Core.Data;
using EasyCob.Core.Modules.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Api.Endpoints;

internal static class MessagingEndpoints
{
    public static void MapMessaging(this WebApplication app)
    {
        var templates = app.MapGroup("/message-templates").RequireAuthorization();
        templates.MapGet("/", async (EasyCobDbContext db, CancellationToken ct) =>
            await db.MessageTemplates.OrderBy(x => x.Name).ThenByDescending(x => x.Version).ToListAsync(ct));
        templates.MapPost("/", async (TemplateRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MetaTemplateId) || string.IsNullOrWhiteSpace(request.Language))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["template"] = ["Nome, template Meta e idioma são obrigatórios."] });
            var version = await db.MessageTemplates.Where(x => x.Name == request.Name.Trim()).MaxAsync(x => (int?)x.Version, ct) ?? 0;
            var template = new MessageTemplate
            {
                Name = request.Name.Trim(),
                MetaTemplateId = request.MetaTemplateId.Trim(),
                Language = request.Language.Trim(),
                Version = version + 1
            };
            db.MessageTemplates.Add(template);
            db.Audit(http.User, "message-template.created", nameof(MessageTemplate), template.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/message-templates/{template.Id}", new { template.Id, template.Version });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));

        var rules = app.MapGroup("/collection-rules").RequireAuthorization();
        rules.MapGet("/", async (EasyCobDbContext db, CancellationToken ct) =>
            await db.CollectionRules.OrderBy(x => x.DaysOffset).ToListAsync(ct));
        rules.MapPost("/", async (RuleRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Invalid("name", "Nome é obrigatório.");
            if (request.DaysOffset is < -365 or > 365) return Invalid("daysOffset", "Intervalo deve estar entre -365 e 365 dias.");
            if (!await db.MessageTemplates.AnyAsync(x => x.Id == request.MessageTemplateId, ct)) return Invalid("messageTemplateId", "Template não encontrado.");
            var rule = new CollectionRule { Name = request.Name.Trim(), MessageTemplateId = request.MessageTemplateId, DaysOffset = request.DaysOffset };
            db.CollectionRules.Add(rule);
            db.Audit(http.User, "collection-rule.created", nameof(CollectionRule), rule.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/collection-rules/{rule.Id}", new { rule.Id });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
        rules.MapPut("/{id:guid}/active", async (Guid id, ActiveRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var rule = await db.CollectionRules.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (rule is null) return Results.NotFound();
            rule.Active = request.Active;
            db.Audit(http.User, request.Active ? "collection-rule.activated" : "collection-rule.deactivated", nameof(CollectionRule), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));

        var messages = app.MapGroup("/messages").RequireAuthorization();
        messages.MapGet("/", async (MessageStatus? status, int? page, EasyCobDbContext db, CancellationToken ct) =>
        {
            var query = db.Messages.AsQueryable();
            if (status.HasValue) query = query.Where(x => x.Status == status);
            return await query.OrderByDescending(x => x.CreatedAt).Skip((Math.Max(page ?? 1, 1) - 1) * 50).Take(50)
                .Select(x => new { x.Id, x.ChargeId, x.Status, x.ScheduledAt, x.SentAt, x.Attempts, x.FailureCode }).ToListAsync(ct);
        });
        messages.MapPost("/{id:guid}/retry", async (Guid id, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (message is null) return Results.NotFound();
            if (message.Status != MessageStatus.Failed) return Results.Conflict(new { error = "Somente mensagens com falha podem ser reenviadas." });
            message.Status = MessageStatus.Pending;
            message.ScheduledAt = DateTimeOffset.UtcNow;
            message.Attempts = 0;
            message.FailureCode = null;
            db.Audit(http.User, "message.retry-requested", nameof(Message), id);
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/messages/{id}");
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Collector"));
        messages.MapPost("/{id:guid}/resolve", async (Guid id, ResolveMessageRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var message = await db.Messages.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (message is null) return Results.NotFound();
            if (message.Status != MessageStatus.Sending) return Results.Conflict(new { error = "Somente entregas ambíguas podem ser reconciliadas." });
            if (request.Sent && string.IsNullOrWhiteSpace(request.ExternalId)) return Invalid("externalId", "ID externo é obrigatório para confirmar envio.");
            message.Status = request.Sent ? MessageStatus.Sent : MessageStatus.Failed;
            message.ExternalId = request.Sent ? request.ExternalId!.Trim() : null;
            message.SentAt = request.Sent ? DateTimeOffset.UtcNow : null;
            message.FailureCode = request.Sent ? null : "delivery-unknown";
            db.Audit(http.User, "message.delivery-reconciled", nameof(Message), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
    }

    private static IResult Invalid(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

internal sealed record TemplateRequest(string Name, string MetaTemplateId, string Language);
internal sealed record RuleRequest(string Name, Guid MessageTemplateId, int DaysOffset);
internal sealed record ActiveRequest(bool Active);
internal sealed record ResolveMessageRequest(bool Sent, string? ExternalId);
