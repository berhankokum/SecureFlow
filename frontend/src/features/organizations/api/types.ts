import type { Permission } from "@/features/authorization/permissions";

export interface Organization {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  role: "OWNER" | "ADMIN" | "MEMBER";
  permissions: Permission[];
  createdAt: string;
  archivedAt: string | null;
}

export interface CreateOrganizationPayload {
  name: string;
  logoUrl?: string;
}
