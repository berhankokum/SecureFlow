import { apiClient } from "@/lib/api/apiClient";
import type {
  AcceptedInvitation,
  CreateInvitationPayload,
  InvitationDetails,
  OrganizationInvitation,
} from "./types";

export function getInvitations(organizationId: string): Promise<OrganizationInvitation[]> {
  return apiClient.get(`/organizations/${organizationId}/invitations`);
}

export function createInvitation(
  organizationId: string,
  payload: CreateInvitationPayload,
): Promise<OrganizationInvitation> {
  return apiClient.post(`/organizations/${organizationId}/invitations`, payload);
}

export function revokeInvitation(organizationId: string, invitationId: string): Promise<void> {
  return apiClient.delete(`/organizations/${organizationId}/invitations/${invitationId}`);
}

export function getInvitation(token: string): Promise<InvitationDetails> {
  return apiClient.get(`/invitations/${encodeURIComponent(token)}`);
}

export function acceptInvitation(token: string): Promise<AcceptedInvitation> {
  return apiClient.post(`/invitations/${encodeURIComponent(token)}/accept`);
}
