using EasyCob.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EasyCob.Api.Endpoints;

internal static class AuditEndpoints
{
    public static void MapAudit(this WebApplication app) =>
        app.MapGet("/audit-events", async (int? page, EasyCobDbContext db, CancellationToken ct) =>
            await db.AuditEvents.OrderByDescending(x => x.OccurredAt).Skip((Math.Max(page ?? 1, 1) - 1) * 100).Take(100)
                .Select(x => new { x.Id, x.ActorId, x.Action, x.EntityType, x.EntityId, x.OccurredAt }).ToListAsync(ct))
            .RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
}
