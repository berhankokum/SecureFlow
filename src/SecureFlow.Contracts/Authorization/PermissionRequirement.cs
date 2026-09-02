using Microsoft.AspNetCore.Authorization;

namespace SecureFlow.Contracts.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
