using System.Text.Json;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Audit;
using EasyCob.Core.Modules.Billing;
using EasyCob.Core.Modules.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Api.Endpoints;

internal static class BillingEndpoints
{
    public static void MapBilling(this WebApplication app)
    {
        var group = app.MapGroup("/charges").RequireAuthorization();
        group.MapGet("/", async (ChargeStatus? status, int? page, EasyCobDbContext db, CancellationToken ct) =>
        {
            var query = db.Charges.AsQueryable();
            if (status.HasValue) query = query.Where(x => x.Status == status);
            return await query.OrderBy(x => x.DueDate).Skip((Math.Max(page ?? 1, 1) - 1) * 50).Take(50)
                .Select(x => new { x.Id, x.CustomerId, x.Description, x.Amount, x.DueDate, x.Status }).ToListAsync(ct);
        });

        group.MapGet("/{id:guid}", async (Guid id, EasyCobDbContext db, CancellationToken ct) =>
        {
            var charge = await db.Charges.Where(x => x.Id == id).Select(x => new
            {
                x.Id,
                x.CustomerId,
                x.Description,
                x.Amount,
                x.DueDate,
                x.Status,
                Installments = db.Installments.Where(i => i.ChargeId == x.Id).OrderBy(i => i.Number).ToArray(),
                Payments = db.Payments.Where(p => p.ChargeId == x.Id).OrderBy(p => p.PaidAt).ToArray()
            }).SingleOrDefaultAsync(ct);
            return charge is null ? Results.NotFound() : Results.Ok(charge);
        });

        group.MapPost("/", async (CreateChargeRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            var roundedAmount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
            if (string.IsNullOrWhiteSpace(request.Description)) errors["description"] = ["Descrição é obrigatória."];
            if (roundedAmount <= 0) errors["amount"] = ["Valor deve ser pelo menos 0,01."];
            if (request.Installments is < 1 or > 120) errors["installments"] = ["Quantidade deve estar entre 1 e 120."];
            if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId && x.ArchivedAt == null, ct)) errors["customerId"] = ["Cliente não encontrado."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);

            var charge = new Charge
            {
                CustomerId = request.CustomerId,
                Description = request.Description.Trim(),
                Amount = roundedAmount,
                DueDate = request.FirstDueDate
            };
            db.Charges.Add(charge);
            db.Installments.AddRange(InstallmentSchedule.Create(charge.Id, charge.Amount, request.Installments, request.FirstDueDate));
            var contact = await db.Contacts.FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId && x.WhatsAppOptIn && x.Phone != null, ct);
            if (contact is not null)
            {
                var conversation = await db.Conversations.FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId, ct);
                if (conversation is null)
                {
                    conversation = new Conversation { CustomerId = request.CustomerId, LastMessageAt = DateTimeOffset.UtcNow };
                    db.Conversations.Add(conversation);
                }
                var rules = await db.CollectionRules.Where(x => x.Active).ToListAsync(ct);
                foreach (var rule in rules)
                {
                    var scheduledDate = request.FirstDueDate.AddDays(rule.DaysOffset).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
                    db.Messages.Add(new Message
                    {
                        ConversationId = conversation.Id,
                        ChargeId = charge.Id,
                        MessageTemplateId = rule.MessageTemplateId,
                        Recipient = contact.Phone!,
                        ScheduledAt = new DateTimeOffset(scheduledDate)
                    });
                }
            }
            AddEvent(db, "billing.charge-created.v1", new { charge.Id, charge.CustomerId, charge.Amount, charge.DueDate });
            db.Audit(http.User, "charge.created", nameof(Charge), charge.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/charges/{charge.Id}", new { charge.Id });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance", "Collector"));

        group.MapPost("/{id:guid}/payments", async (Guid id, PaymentRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0) return Invalid("amount", "Valor deve ser pelo menos 0,01.");
            if (request.PaidAt > DateTimeOffset.UtcNow.AddMinutes(5)) return Invalid("paidAt", "Data do pagamento não pode estar no futuro.");
            var charge = await db.Charges.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (charge is null) return Results.NotFound();
            if (charge.Status == ChargeStatus.Cancelled) return Results.Conflict(new { error = "Cobrança cancelada." });
            if (!string.IsNullOrWhiteSpace(request.ExternalId) && await db.Payments.AnyAsync(x => x.ExternalId == request.ExternalId, ct))
                return Results.Conflict(new { error = "Pagamento já registrado." });
            var paid = await db.Payments.Where(x => x.ChargeId == id).SumAsync(x => x.Amount, ct);
            if (paid + amount > charge.Amount) return Invalid("amount", "Pagamento excede o saldo da cobrança.");

            var payment = new Payment { ChargeId = id, Amount = amount, PaidAt = request.PaidAt.ToUniversalTime(), ExternalId = EmptyToNull(request.ExternalId) };
            db.Payments.Add(payment);
            charge.RecordPayment(paid, amount);
            AddEvent(db, "billing.payment-recorded.v1", new { payment.Id, ChargeId = id, payment.Amount, payment.PaidAt });
            db.Audit(http.User, "payment.recorded", nameof(Payment), payment.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/charges/{id}/payments/{payment.Id}", new { payment.Id, charge.Status });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance"));

        group.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var charge = await db.Charges.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (charge is null) return Results.NotFound();
            var hasPayments = await db.Payments.AnyAsync(x => x.ChargeId == id, ct);
            if (hasPayments) return Results.Conflict(new { error = "Cobrança com pagamento não pode ser cancelada." });
            charge.Cancel(hasPayments);
            AddEvent(db, "billing.charge-cancelled.v1", new { charge.Id });
            db.Audit(http.User, "charge.cancelled", nameof(Charge), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance"));
    }

    private static void AddEvent(EasyCobDbContext db, string type, object payload) =>
        db.OutboxMessages.Add(new OutboxMessage { Type = type, Payload = JsonSerializer.Serialize(payload) });
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult Invalid(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

internal sealed record CreateChargeRequest(Guid CustomerId, string Description, decimal Amount, DateOnly FirstDueDate, int Installments = 1);
internal sealed record PaymentRequest(decimal Amount, DateTimeOffset PaidAt, string? ExternalId);
