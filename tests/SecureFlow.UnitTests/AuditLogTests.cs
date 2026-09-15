using SecureFlow.Modules.Audit.Domain;

namespace SecureFlow.UnitTests;

public class AuditLogTests
{
    [Fact]
    public void Global_events_cannot_be_assigned_to_tenants_or_claim_unverified_actors()
    {
        Assert.Throws<ArgumentException>(() => AuditLog.Create(Guid.NewGuid(), null, "LOGIN_FAILURE", "Identity", null, null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => AuditLog.Create(null, Guid.NewGuid(), "LOGIN_FAILURE", "Identity", null, null, null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => AuditLog.Create(null, null, "WORK_ITEM_UPDATED", "WorkItem", Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
        var log = AuditLog.Create(null, null, "LOGIN_FAILURE", "Identity", null, null, null, DateTimeOffset.UtcNow);
        Assert.Null(log.ActorUserId);
        Assert.Equal("{}", log.Metadata);
    }

    [Fact]
    public void Metadata_is_allow_listed_and_request_information_is_bounded()
    {
        AuditLog Create(IReadOnlyDictionary<string, string?> metadata) => AuditLog.Create(Guid.NewGuid(), Guid.NewGuid(),
            "WORK_ITEM_UPDATED", "WorkItem", Guid.NewGuid(), "invalid", "agent\r\n" + new string('x', 600), DateTimeOffset.UtcNow, metadata);
        Assert.Throws<ArgumentException>(() => Create(new Dictionary<string, string?> { ["password"] = "Secret123" }));
        Assert.Throws<ArgumentException>(() => Create(new Dictionary<string, string?> { ["userId"] = "secret@example.com" }));
        var log = Create(new Dictionary<string, string?> { ["role"] = "ADMIN" });
        Assert.Null(log.IpAddress);
        Assert.True(log.UserAgent!.Length <= 512);
        Assert.DoesNotContain('\n', log.UserAgent);
        Assert.DoesNotContain('\r', log.UserAgent);
    }
}
