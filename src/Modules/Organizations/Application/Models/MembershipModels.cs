namespace SecureFlow.Modules.Organizations.Application.Models;

public sealed record OrganizationMemberSummary(
    Guid Id,
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset JoinedAt,
    Guid? InvitedBy);

public sealed record OrganizationInvitationSummary(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string Status);

public sealed record OrganizationInvitationDetails(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    string Status);

public sealed record AcceptedInvitationSummary(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string Role);
