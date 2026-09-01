namespace SecureFlow.Modules.Organizations.Application.Models;

public sealed record OrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt);
