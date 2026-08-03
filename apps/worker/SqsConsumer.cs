using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Audit;
using EasyCob.Core.Modules.Finance;
using EasyCob.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Worker;

public sealed class SqsConsumer(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    ILogger<SqsConsumer> logger,
    string queueUrl) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    WaitTimeSeconds = 20,
                    MaxNumberOfMessages = 10,
                    MessageAttributeNames = ["All"]
                }, stoppingToken);
                foreach (var message in response.Messages)
                {
                    try { await Process(message, stoppingToken); }
                    catch (Exception exception) { logger.LogError(exception, "Falha ao consumir evento {MessageId}", message.MessageId); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao receber eventos do SQS");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task Process(Amazon.SQS.Model.Message message, CancellationToken cancellationToken)
    {
        if (!message.MessageAttributes.TryGetValue("event_id", out var eventIdAttribute) ||
            !message.MessageAttributes.TryGetValue("event_type", out var typeAttribute) ||
            !message.MessageAttributes.TryGetValue("tenant_id", out var tenantAttribute) ||
            !Guid.TryParse(tenantAttribute.StringValue, out var tenantId)) return;

        using var scope = scopeFactory.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        var eventId = $"sqs:{eventIdAttribute.StringValue}";
        if (await db.InboxMessages.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.ExternalId == eventId, cancellationToken))
        {
            await Delete(message, cancellationToken);
            return;
        }

        using var payload = JsonDocument.Parse(message.Body);
        switch (typeAttribute.StringValue)
        {
            case "billing.charge-created.v1":
                var dueDate = DateOnly.Parse(payload.RootElement.GetProperty("DueDate").GetString()!);
                var amount = payload.RootElement.GetProperty("Amount").GetDecimal();
                var created = await Balance(db, tenantId, dueDate, cancellationToken);
                created.Receivable += amount;
                if (dueDate < DateOnly.FromDateTime(DateTime.UtcNow)) created.Overdue += amount;
                break;
            case "billing.payment-recorded.v1":
                var paidAt = payload.RootElement.GetProperty("PaidAt").GetDateTimeOffset();
                var received = await Balance(db, tenantId, DateOnly.FromDateTime(paidAt.UtcDateTime), cancellationToken);
                received.Received += payload.RootElement.GetProperty("Amount").GetDecimal();
                break;
        }
        db.InboxMessages.Add(new InboxMessage { ExternalId = eventId });
        await db.SaveChangesAsync(cancellationToken);
        await Delete(message, cancellationToken);
    }

    private static async Task<DailyBalance> Balance(EasyCobDbContext db, Guid tenantId, DateOnly date, CancellationToken ct)
    {
        var balance = await db.DailyBalances.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Date == date, ct);
        if (balance is not null) return balance;
        balance = new DailyBalance { Date = date };
        db.DailyBalances.Add(balance);
        return balance;
    }

    private Task<DeleteMessageResponse> Delete(Amazon.SQS.Model.Message message, CancellationToken ct) =>
        sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, ct);
}
