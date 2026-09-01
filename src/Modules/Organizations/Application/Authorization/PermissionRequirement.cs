using Microsoft.AspNetCore.Authorization;

namespace SecureFlow.Modules.Organizations.Application.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
