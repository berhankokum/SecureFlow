export interface Organization {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  role: "OWNER" | "ADMIN" | "MEMBER";
  createdAt: string;
  archivedAt: string | null;
}

export interface CreateOrganizationPayload {
  name: string;
  logoUrl?: string;
}
