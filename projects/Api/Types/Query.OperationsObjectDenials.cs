using Api.Data;
using Api.Security;
using HotChocolate.Authorization;

namespace Api.Types;

public sealed partial class Query
{
    private const int MaxObjectAccessDenialsLimit = 500;

    /// <summary>
    /// Returns the most recent object-authorization denials of this server process with the real failure reason
    /// (not_found / not_owned / wrong_account_context / not_available), newest first, plus counts by object type and reason.
    /// Callers never see the reason; only admins do, so enumeration probes become visible without leaking existence.
    /// Read-only and served from memory; requires admin dashboard access.
    /// </summary>
    [Authorize]
    public async Task<ObjectAccessDenialsResult> GetObjectAccessDenials(
        int? limit,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] ObjectDenialRecorder denialRecorder)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HTTP context is required for object access denials.");
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, httpContext.User, httpContext.RequestAborted);

        var summary = denialRecorder.Summarize();
        return new ObjectAccessDenialsResult
        {
            TotalRecorded = summary.TotalRecorded,
            WindowSize = summary.WindowSize,
            ProbeSignals = summary.ProbeSignals,
            ByObjectType = summary.ByObjectType.Select(c => new ObjectDenialCountItem { Key = c.Key, Count = c.Count }).ToList(),
            ByReason = summary.ByReason.Select(c => new ObjectDenialCountItem { Key = c.Key, Count = c.Count }).ToList(),
            Recent = denialRecorder.Recent(Math.Clamp(limit ?? 100, 0, MaxObjectAccessDenialsLimit))
                .Select(e => new ObjectAccessDenialItem
                {
                    OccurredAtUtc = e.OccurredAtUtc,
                    ActorUserId = e.ActorUserId,
                    ApiKeyId = e.ApiKeyId,
                    ObjectType = e.ObjectType,
                    ObjectId = e.ObjectId,
                    Reason = e.Reason,
                    IsProbeSignal = e.IsProbeSignal,
                })
                .ToList(),
        };
    }
}

/// <summary>Recent object-authorization denials for the admin Operations Dashboard.</summary>
public sealed class ObjectAccessDenialsResult
{
    public long TotalRecorded { get; set; }
    public int WindowSize { get; set; }
    public int ProbeSignals { get; set; }
    public List<ObjectDenialCountItem> ByObjectType { get; set; } = [];
    public List<ObjectDenialCountItem> ByReason { get; set; } = [];
    public List<ObjectAccessDenialItem> Recent { get; set; } = [];
}

public sealed class ObjectDenialCountItem
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ObjectAccessDenialItem
{
    public DateTime OccurredAtUtc { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsProbeSignal { get; set; }
}
