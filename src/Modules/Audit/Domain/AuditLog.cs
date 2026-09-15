using System.Net;
using System.Text.Json;
using SecureFlow.Contracts.Audit;

namespace SecureFlow.Modules.Audit.Domain;

public sealed class AuditLog
{
    private AuditLog() { }
    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = "";
    public string ResourceType { get; private set; } = "";
    public Guid? ResourceId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string Metadata { get; private set; } = "{}";
    public DateTimeOffset Timestamp { get; private set; }

    public static readonly IReadOnlySet<string> ResourceTypes = new HashSet<string>(StringComparer.Ordinal)
    { "Identity", "OrganizationInvitation", "OrganizationMember", "ProjectMember", "Project", "WorkItem", "Attachment" };
    private static readonly HashSet<string> MetadataKeys = new(StringComparer.Ordinal)
    { "userId", "previousRole", "role", "previousAssigneeId", "assigneeId", "fromStateId", "toStateId" };

    public static AuditLog Create(Guid? organizationId, Guid? actorId, string action, string resourceType,
        Guid? resourceId, string? ip, string? userAgent, DateTimeOffset timestamp,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        if (!AuditActions.All.Contains(action) || !ResourceTypes.Contains(resourceType))
            throw new ArgumentException("Unknown audit event.");
        var global = action is "LOGIN_FAILURE" or "PASSWORD_CHANGED";
        if (global != (organizationId is null) || organizationId == Guid.Empty || actorId == Guid.Empty || resourceId == Guid.Empty)
            throw new ArgumentException("Invalid audit scope.");
        if (global && resourceType != "Identity" || !global && (resourceId is null || resourceType == "Identity")
            || action == "LOGIN_FAILURE" && (actorId is not null || resourceId is not null)
            || action == "PASSWORD_CHANGED" && resourceId is null)
            throw new ArgumentException("Invalid audit resource.");
        if (metadata?.Any(pair => !MetadataKeys.Contains(pair.Key) || pair.Value?.Length > 64) == true)
            throw new ArgumentException("Audit metadata is not allow-listed.");
        if (metadata?.Any(pair => pair.Value is not null && (pair.Key is "role" or "previousRole"
                ? pair.Value is not ("OWNER" or "ADMIN" or "MEMBER" or "MANAGER" or "CONTRIBUTOR" or "VIEWER")
                : !Guid.TryParse(pair.Value, out _))) == true)
            throw new ArgumentException("Audit metadata must contain only identifiers or known roles.");
        var agent = userAgent is null ? null : new string(userAgent.Take(512).Where(c => !char.IsControl(c)).ToArray());
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorUserId = actorId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            IpAddress = IPAddress.TryParse(ip, out var address) ? address.ToString() : null,
            UserAgent = agent,
            Timestamp = timestamp.ToUniversalTime(),
            Metadata = JsonSerializer.Serialize(metadata ?? new Dictionary<string, string?>()),
        };
    }
}
