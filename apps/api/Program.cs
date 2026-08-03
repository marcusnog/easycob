using System.Security.Claims;
using EasyCob.Api.Endpoints;
using EasyCob.Core.Data;
using EasyCob.Core.Tenancy;
using EasyCob.Core.Modules.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsProduction())
    foreach (var key in new[] { "Authentication:Authority", "Authentication:Audience", "WhatsApp:VerifyToken", "WhatsApp:AppSecret" })
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]) || builder.Configuration[key]!.Contains("CHANGE_ME", StringComparison.Ordinal))
            throw new InvalidOperationException($"{key} não configurado.");
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

builder.Services.AddScoped<TenantContext>();
builder.Services.AddDbContext<EasyCobDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Authentication:Authority"];
    var clientId = builder.Configuration["Authentication:Audience"];
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false,
        RoleClaimType = "cognito:groups"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            if (context.Principal?.FindFirstValue("token_use") != "access" ||
                context.Principal.FindFirstValue("client_id") != clientId)
                context.Fail("Token Cognito invÃ¡lido para este cliente.");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 10_485_760);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("webhooks", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler();
app.UseSwagger();
if (app.Environment.IsDevelopment()) app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    var tenantClaim = context.User.FindFirstValue("tenant_id");
    var tenantHash = tenantClaim is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenantClaim)))[..12];
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Http");
    using (logger.BeginScope(new Dictionary<string, object?> { ["trace_id"] = Activity.Current?.TraceId.ToString(), ["tenant"] = tenantHash }))
    {
        await next();
        logger.LogInformation("{Method} {Path} returned {StatusCode} in {ElapsedMs}ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
});
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    context.RequestServices.GetRequiredService<TenantContext>().TenantId = tenantId;
    var db = context.RequestServices.GetRequiredService<EasyCobDbContext>();
    await using var transaction = db.Database.IsRelational()
        ? await db.Database.BeginTransactionAsync(context.RequestAborted)
        : null;
    if (transaction is not null)
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('easycob.tenant_id', {tenantId.ToString()}, true)", context.RequestAborted);
    if (!await db.Tenants.AnyAsync(x => x.Id == tenantId, context.RequestAborted) || context.User.FindFirstValue("sub") is not { } subject)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (!await db.Users.AnyAsync(x => x.ExternalId == subject, context.RequestAborted))
    {
        db.Users.Add(new User
        {
            ExternalId = subject,
            Email = context.User.FindFirstValue("email") ?? string.Empty,
            Role = ResolveRole(context.User)
        });
        await db.SaveChangesAsync(context.RequestAborted);
    }
    await next();
    if (transaction is not null && context.Response.StatusCode < 500)
        await transaction.CommitAsync(context.RequestAborted);
});
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (EasyCobDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapCustomers();
app.MapBilling();
app.MapMessaging();
app.MapTenancy();
app.MapWhatsAppWebhook();
app.MapFinance();
app.MapAudit();

app.Run();

static UserRole ResolveRole(ClaimsPrincipal user)
{
    foreach (var role in new[] { UserRole.Owner, UserRole.Admin, UserRole.Finance, UserRole.Collector, UserRole.Viewer })
        if (user.IsInRole(role.ToString())) return role;
    return UserRole.Viewer;
}

public partial class Program;
