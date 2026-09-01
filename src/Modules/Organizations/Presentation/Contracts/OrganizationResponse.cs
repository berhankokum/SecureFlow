using SecureFlow.Modules.Organizations.Application.Models;

namespace SecureFlow.Modules.Organizations.Presentation.Contracts;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt)
{
    public static OrganizationResponse From(OrganizationSummary organization) => new(
        organization.Id,
        organization.Name,
        organization.Slug,
        organization.LogoUrl,
        organization.Role,
        organization.Permissions,
        organization.CreatedAt,
        organization.ArchivedAt);
}
