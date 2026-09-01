namespace SecureFlow.Modules.Organizations.Application.CreateOrganization;

public sealed record CreateOrganizationCommand(Guid CreatorUserId, string Name, string? LogoUrl);
