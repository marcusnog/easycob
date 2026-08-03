using Amazon;
using Amazon.SQS;
using EasyCob.Core.Data;
using EasyCob.Core.Tenancy;
using EasyCob.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsProduction())
    foreach (var key in new[] { "AWS:QueueUrl", "WhatsApp:AccessToken" })
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]) || builder.Configuration[key]!.Contains("CHANGE_ME", StringComparison.Ordinal))
            throw new InvalidOperationException($"{key} não configurado.");
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");
var queueUrl = builder.Configuration["AWS:QueueUrl"]
    ?? throw new InvalidOperationException("AWS:QueueUrl não configurada.");
var whatsAppToken = builder.Configuration["WhatsApp:AccessToken"]
    ?? throw new InvalidOperationException("WhatsApp:AccessToken não configurado.");

builder.Services.AddScoped<TenantContext>();
builder.Services.AddDbContext<EasyCobDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"] ?? "sa-east-1")));
builder.Services.AddHttpClient("whatsapp", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Authorization = new("Bearer", whatsAppToken);
});
builder.Services.AddHostedService(sp => new OutboxPublisher(
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<IAmazonSQS>(),
    sp.GetRequiredService<ILogger<OutboxPublisher>>(),
    queueUrl));
builder.Services.AddHostedService(sp => new WhatsAppDispatcher(
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<ILogger<WhatsAppDispatcher>>(),
    builder.Configuration["WhatsApp:GraphVersion"] ?? "v23.0"));
builder.Services.AddHostedService(sp => new SqsConsumer(
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<IAmazonSQS>(),
    sp.GetRequiredService<ILogger<SqsConsumer>>(),
    queueUrl));
builder.Services.AddHostedService<OverdueUpdater>();

var host = builder.Build();
host.Run();
