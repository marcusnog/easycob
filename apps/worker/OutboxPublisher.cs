using Amazon.SQS;
using Amazon.SQS.Model;
using EasyCob.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Worker;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqs,
    ILogger<OutboxPublisher> logger,
    string queueUrl) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatch(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao publicar a outbox");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task PublishBatch(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EasyCobDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .FromSqlRaw("SELECT * FROM outbox_messages WHERE published_at IS NULL ORDER BY occurred_at FOR UPDATE SKIP LOCKED LIMIT 10")
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = message.Payload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["event_id"] = new() { DataType = "String", StringValue = message.Id.ToString() },
                    ["event_type"] = new() { DataType = "String", StringValue = message.Type },
                    ["tenant_id"] = new() { DataType = "String", StringValue = message.TenantId.ToString() }
                }
            }, cancellationToken);
            message.PublishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
