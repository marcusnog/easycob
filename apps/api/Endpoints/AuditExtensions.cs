using System.Security.Claims;
using EasyCob.Core.Data;
using EasyCob.Core.Modules.Audit;

namespace EasyCob.Api.Endpoints;

internal static class AuditExtensions
{
    public static void Audit(this EasyCobDbContext db, ClaimsPrincipal user, string action, string entityType, Guid entityId) =>
        db.AuditEvents.Add(new AuditEvent
        {
            ActorId = user.FindFirstValue("sub") ?? "unknown",
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString()
        });
}
