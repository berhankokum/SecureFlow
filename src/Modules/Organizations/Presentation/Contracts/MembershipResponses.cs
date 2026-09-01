using SecureFlow.Modules.Organizations.Application.Models;

namespace SecureFlow.Modules.Organizations.Presentation.Contracts;

public sealed record OrganizationMemberResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset JoinedAt,
    Guid? InvitedBy)
{
    public static OrganizationMemberResponse From(OrganizationMemberSummary member) =>
        new(member.Id, member.UserId, member.Email, member.Role, member.JoinedAt, member.InvitedBy);
}

public sealed record OrganizationInvitationResponse(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string Status)
{
    public static OrganizationInvitationResponse From(OrganizationInvitationSummary invitation) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.Status);
}

public sealed record OrganizationInvitationDetailsResponse(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    string Status)
{
    public static OrganizationInvitationDetailsResponse From(OrganizationInvitationDetails invitation) =>
        new(
            invitation.Id,
            invitation.OrganizationId,
            invitation.OrganizationName,
            invitation.OrganizationSlug,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt,
            invitation.Status);
}

public sealed record AcceptedInvitationResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string Role)
{
    public static AcceptedInvitationResponse From(AcceptedInvitationSummary invitation) =>
        new(
            invitation.OrganizationId,
            invitation.OrganizationName,
            invitation.OrganizationSlug,
            invitation.Role);
}
