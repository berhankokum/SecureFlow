using System.Security.Claims;
using SecureFlow.Contracts.Audit;

namespace SecureFlow.Api.Persistence;

public sealed class AuditRequestContext(IHttpContextAccessor accessor) : IAuditRequestContext
{
    public Guid? ActorUserId => accessor.HttpContext?.User.Identity?.IsAuthenticated == true
        && Guid.TryParse(accessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
