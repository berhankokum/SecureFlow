export interface TeamSummary {
  id: string;
  organizationId: string;
  name: string;
  description: string | null;
  memberCount: number;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
}

export interface TeamMember {
  userId: string;
  email: string;
  addedAt: string;
  addedBy: string;
}

export interface TeamDetails extends Omit<TeamSummary, "memberCount"> {
  members: TeamMember[];
}

export interface TeamPayload {
  name: string;
  description?: string;
}
