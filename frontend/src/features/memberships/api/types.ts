export type OrganizationRole = "OWNER" | "ADMIN" | "MEMBER";

export interface OrganizationMember {
  id: string;
  userId: string;
  email: string;
  role: OrganizationRole;
  joinedAt: string;
  invitedBy: string | null;
}

export interface ChangeMemberRolePayload {
  role: OrganizationRole;
}
