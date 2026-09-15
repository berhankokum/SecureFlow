using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Audit;
using SecureFlow.Modules.Audit.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Audit.Application;

public enum AuditAccess { NoMembership, Forbidden, Allowed }

public interface IAuditDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    Task<AuditAccess> GetAuditAccessAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditWriter(IAuditDbContext db, IAuditRequestContext context, IClock clock) : IAuditWriter
{
    public async Task LoginFailureAsync(CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(AuditLog.Create(null, null, "LOGIN_FAILURE", "Identity", null,
            context.IpAddress, context.UserAgent, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }

    public void StagePasswordChanged(Guid userId) => db.AuditLogs.Add(AuditLog.Create(null,
        context.ActorUserId, "PASSWORD_CHANGED", "Identity", userId, context.IpAddress, context.UserAgent, clock.UtcNow));

    public void StageAttachmentDownloaded(Guid organizationId, Guid attachmentId) => db.AuditLogs.Add(AuditLog.Create(
        organizationId, context.ActorUserId, "ATTACHMENT_DOWNLOADED", "Attachment", attachmentId,
        context.IpAddress, context.UserAgent, clock.UtcNow));
}

public sealed class AuditQuery
{
    public string? Action { get; init; }
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public Guid? ActorUserId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 100000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Action).Must(value => value is null || AuditActions.All.Contains(value));
        RuleFor(query => query.ResourceType).Must(value => value is null || AuditLog.ResourceTypes.Contains(value));
        RuleFor(query => query.To).Must((query, value) => value is null || query.From is null || value >= query.From);
    }
}

public sealed class AuditReadHandler(IAuditDbContext db)
{
    public async Task<Result<IReadOnlyList<AuditLog>>> HandleAsync(Guid org, Guid user, AuditQuery query, CancellationToken ct)
    {
        var access = await db.GetAuditAccessAsync(org, user, ct);
        if (access != AuditAccess.Allowed)
            return Result.Failure<IReadOnlyList<AuditLog>>(new Error(access == AuditAccess.NoMembership ? "Audit.NotFound" : "Audit.Forbidden", "Audit kayıtlarına erişilemiyor."));
        if (!(await new AuditQueryValidator().ValidateAsync(query, ct)).IsValid)
            return Result.Failure<IReadOnlyList<AuditLog>>(new Error("Audit.InvalidQuery", "Geçersiz audit filtresi veya sayfalama."));
        var source = db.AuditLogs.AsNoTracking().Where(log => log.OrganizationId == org);
        if (query.Action is { } action) source = source.Where(log => log.Action == action);
        if (query.ResourceType is { } type) source = source.Where(log => log.ResourceType == type);
        if (query.ResourceId is { } resource) source = source.Where(log => log.ResourceId == resource);
        if (query.ActorUserId is { } actor) source = source.Where(log => log.ActorUserId == actor);
        if (query.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            source = source.Where(log => log.Timestamp >= start);
        }
        if (query.To is { } to && to < DateOnly.MaxValue)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            source = source.Where(log => log.Timestamp < end);
        }
        return Result.Success<IReadOnlyList<AuditLog>>(await source.OrderByDescending(log => log.Timestamp)
            .ThenByDescending(log => log.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct));
    }
}
