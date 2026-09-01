import type { OrganizationRole } from "@/features/memberships/api/types";

export type InvitationStatus = "PENDING" | "ACCEPTED" | "REVOKED" | "EXPIRED";

export interface OrganizationInvitation {
  id: string;
  email: string;
  role: OrganizationRole;
  expiresAt: string;
  createdAt: string;
  status: InvitationStatus;
}

export interface InvitationDetails {
  id: string;
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  email: string;
  role: OrganizationRole;
  expiresAt: string;
  status: InvitationStatus;
}

export interface AcceptedInvitation {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  role: OrganizationRole;
}

export interface CreateInvitationPayload {
  email: string;
  role: OrganizationRole;
}
